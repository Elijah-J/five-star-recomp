// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Diagnostics;
using System.Runtime.InteropServices;
using FiveStar.Loader.Native;

namespace FiveStar.Loader.Shims.D3D12X.Pipelines;

/// <summary>
/// Pipeline creations the driver takes too long for. The game creates pipelines in deferred jobs and waits for
/// all of them before it finishes a frame (docs/d3d12x-pipelines.md); the console builds them at once, the desktop
/// driver compiles some translated programs for minutes. A creation that has not returned within the budget hands the
/// game a <see cref="StandInPipelineObject"/> and goes on in the background; the stand-in becomes the desktop pipeline
/// when the driver is done. Until then the command lists skip the work it would run.
/// </summary>
internal sealed unsafe class StandInPipelines
{
    /// <summary>A build that takes longer than this is named in the log when it ends.</summary>
    private static readonly TimeSpan SlowBuild = TimeSpan.FromMinutes(1);

    private readonly object _gate = new();
    private readonly HashSet<nint> _live = [];
    private readonly Dictionary<nint, Action> _waiting = [];
    private readonly Dictionary<nint, StandInWork> _work = [];
    private readonly TimeSpan _budget;
    private readonly PipelineBuildQueue _builds;
    private readonly PipelineBuildQueue _heavyBuilds;
    private readonly OnceLog _warnings;
    private readonly nint* _vtable;
    private readonly GCHandle _self;
    private int _created;
    private int _ready;
    private int _deferred;
    private int _used;
    private int _onDemand;
    private long _skipped;
    private long _recorded;

    /// <param name="budget">How long a creation may take before the game gets a stand-in.</param>
    /// <param name="builds">The threads that call the driver (started by the caller).</param>
    /// <param name="heavyBuilds">The same queue, or the thread for pipelines of <see cref="PipelineFactory.HeavyShaderBytes"/> or more, which wait for more memory without holding up the others.</param>
    /// <param name="warnings">Where stand-ins and late pipelines are reported, once per name.</param>
    public StandInPipelines(TimeSpan budget, PipelineBuildQueue builds, PipelineBuildQueue heavyBuilds, OnceLog warnings)
    {
        _budget = budget;
        _builds = builds;
        _heavyBuilds = heavyBuilds;
        _warnings = warnings;
        _vtable = StandInPipelineMethods.CreateVtable();
        _self = GCHandle.Alloc(this, GCHandleType.Weak);
    }

    /// <summary>Stand-ins handed out, how many have their pipeline, the work skipped meanwhile and the work recorded.</summary>
    public string Summary => $"{Volatile.Read(ref _created)} stand-in pipelines ({Volatile.Read(ref _deferred)} built on first use, {Volatile.Read(ref _used)} of them used, {Volatile.Read(ref _onDemand)} with programs made on demand, {Volatile.Read(ref _ready)} built since, {Count(queue => queue.Backlog)} queued, {Count(queue => queue.WaitingForMemory)} build threads waiting for memory), {Interlocked.Read(ref _skipped)} draws and dispatches skipped, {Interlocked.Read(ref _recorded)} recorded";

    /// <summary>
    /// Runs <paramref name="create"/> and writes its pipeline to <paramref name="result"/>; when it takes longer than
    /// the budget, or when earlier creations still wait for a thread, writes a stand-in instead and returns S_OK.
    /// <paramref name="built"/> is told the desktop pipeline when there is one; <paramref name="heavy"/> builds on the heavy thread.
    /// </summary>
    public int Create(Func<PipelineResult> create, nint* result, string what, Action<nint>? built, bool heavy = false)
    {
        var budget = _budget;
        var queue = _builds;

        if (heavy)
        {
            queue = _heavyBuilds;
        }

        if (queue.Backlog > 0)
        {
            budget = TimeSpan.Zero;
        }

        long started = Stopwatch.GetTimestamp();
        var queued = queue.Queue(create, urgent: false);
        var build = queued.Done.Task;

        if (build.Wait(budget))
        {
            *result = build.Result.Pipeline;

            if (build.Result.Pipeline != 0)
            {
                built?.Invoke(build.Result.Pipeline);
            }

            return build.Result.Hr;
        }

        nint standIn = NewStandIn();
        Track(standIn, new StandInWork { Creation = (queued, queue) });
        build.ContinueWith(task => Complete(standIn, task.Result, what, started, built), CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
        Interlocked.Increment(ref _created);
        _warnings.Warn("standin", $"[d3d12_xs] pipeline {what} not built in time: the game gets a stand-in, its draws and dispatches are skipped until the driver is done (counted in the sample summary)");
        *result = standIn;

        return HResult.Ok;
    }

    /// <summary>
    /// Writes a stand-in whose build starts only when a command list first sets it (<see cref="Desktop"/>): the game
    /// creates tens of thousands of pipelines while loading and draws with a small part of them. <paramref name="used"/>
    /// runs then, before the build is queued.
    /// </summary>
    public int Deferred(Func<PipelineResult> create, nint* result, string what, Action<nint>? built, bool heavy, Action used)
    {
        nint standIn = NewStandIn();
        ((StandInPipelineObject*)standIn)->Deferred = 1;

        lock (_gate)
        {
            _waiting[standIn] = () =>
            {
                used();
                var queue = heavy ? _heavyBuilds : _builds;
                long started = Stopwatch.GetTimestamp();
                queue.Queue(create, urgent: true).Done.Task.ContinueWith(task => Complete(standIn, task.Result, what, started, built), CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
            };
        }

        Interlocked.Increment(ref _created);
        Interlocked.Increment(ref _deferred);
        *result = standIn;

        return HResult.Ok;
    }

    /// <summary>
    /// Writes a stand-in whose programs are made on demand (<see cref="OnDemandPrograms"/>): <paramref name="programs"/>
    /// starts making them (told whether a command list waits) and, once they are ready, <paramref name="prepare"/> gives
    /// the build, queued as in <see cref="Create"/>; null leaves the stand-in unbuilt. With <paramref name="used"/> this
    /// starts when a command list first sets the stand-in (as <see cref="Deferred"/>), else now.
    /// </summary>
    public int Waiting(Func<bool, Task> programs, Func<PendingBuild?> prepare, nint* result, Action? used)
    {
        nint standIn = NewStandIn();
        Track(standIn, new StandInWork { Programs = programs });
        Interlocked.Increment(ref _created);
        Interlocked.Increment(ref _onDemand);

        void Start(bool firstUse)
        {
            long started = Stopwatch.GetTimestamp();
            programs(firstUse).ContinueWith(_ => Queue(standIn, prepare, started), CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
        }

        if (used is null)
        {
            Start(firstUse: false);
        }
        else
        {
            ((StandInPipelineObject*)standIn)->Deferred = 1;
            Interlocked.Increment(ref _deferred);

            lock (_gate)
            {
                _waiting[standIn] = () =>
                {
                    used();
                    Start(firstUse: true);
                };
            }
        }

        *result = standIn;

        return HResult.Ok;
    }

    /// <summary>Queues the build of a stand-in whose programs are ready; logs a build that cannot be made.</summary>
    private void Queue(nint standIn, Func<PendingBuild?> prepare, long started)
    {
        PendingBuild? build;

        try
        {
            build = prepare();
        }
        catch (Exception exception) when (exception is InvalidDataException or NotSupportedException)
        {
            _warnings.Warn($"prepare-{exception.Message}", $"[d3d12_xs] pipeline with programs made on demand not built: {exception.Message}");

            return;
        }

        if (build is null)
        {
            return;
        }

        var queue = build.Heavy ? _heavyBuilds : _builds;
        PipelineBuild queued;

        lock (_gate)
        {
            // A stand-in a command list set already goes first; one set later is promoted then (Promote).
            queued = queue.Queue(build.Create, Volatile.Read(ref ((StandInPipelineObject*)standIn)->Used) == 1);

            if (_work.TryGetValue(standIn, out var work))
            {
                work.Creation = (queued, queue);
            }
        }

        queued.Done.Task.ContinueWith(task => Complete(standIn, task.Result, build.What, started, build.Built), CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
    }

    private void Track(nint standIn, StandInWork work)
    {
        lock (_gate)
        {
            _work[standIn] = work;
        }
    }

    /// <summary>
    /// A command list set <paramref name="standIn"/> for the first time: its programs and its creation go before all
    /// speculative work (pipelines built at creation from the journal, prefetched programs).
    /// </summary>
    private void Promote(nint standIn)
    {
        StandInWork? work;

        lock (_gate)
        {
            _work.TryGetValue(standIn, out work);
        }

        if (work is null)
        {
            return;
        }

        if (work.Programs is not null)
        {
            _ = work.Programs(true);
        }

        lock (_gate)
        {
            var creation = work.Creation;

            if (creation.HasValue)
            {
                creation.Value.Queue.Promote(creation.Value.Build);
            }
        }
    }

    /// <summary>
    /// The desktop pipeline behind <paramref name="pipeline"/>: the built pipeline of a stand-in (0 and
    /// <paramref name="pending"/> while the driver works), any other pointer as it is.
    /// </summary>
    [HotPath]
    public nint Desktop(nint pipeline, out bool pending)
    {
        pending = false;

        if (pipeline == 0 || *(nint*)pipeline != (nint)_vtable)
        {
            return pipeline;
        }

        var standIn = (StandInPipelineObject*)pipeline;
        nint desktop = Volatile.Read(ref standIn->Desktop);
        pending = desktop == 0;

        // Used first, so a deferred stand-in started below queues its creation as urgent.
        if (pending && Volatile.Read(ref standIn->Used) == 0 && Interlocked.Exchange(ref standIn->Used, 1) == 0)
        {
            Promote(pipeline);
        }

        if (pending && Volatile.Read(ref standIn->Deferred) == 1 && Interlocked.Exchange(ref standIn->Deferred, 0) == 1)
        {
            Start(pipeline);
        }

        return desktop;
    }

    /// <summary>Queues the build of a deferred stand-in on its first use.</summary>
    private void Start(nint standIn)
    {
        Action? start;

        lock (_gate)
        {
            _waiting.Remove(standIn, out start);
        }

        Interlocked.Increment(ref _used);
        start?.Invoke();
    }

    /// <summary>Counts a draw or dispatch a command list recorded (draws per frame: its growth over the presents).</summary>
    [HotPath]
    public void Recorded()
    {
        Interlocked.Increment(ref _recorded);
    }

    /// <summary>Counts a dispatch a command list dropped because its pipeline is still a stand-in.</summary>
    [HotPath]
    public void Skipped()
    {
        Interlocked.Increment(ref _skipped);
    }

    /// <summary>The last reference went: releases the desktop pipeline (or lets the build release it) and frees the stand-in.</summary>
    public void Retire(StandInPipelineObject* standIn)
    {
        nint desktop;

        lock (_gate)
        {
            _live.Remove((nint)standIn);
            _waiting.Remove((nint)standIn);
            _work.Remove((nint)standIn);
            desktop = standIn->Desktop;
        }

        ComObject.Release(desktop);
        NativeMemory.Free(standIn);
    }

    /// <summary>
    /// A stand-in that never gets a pipeline, for a creation that failed: the console never fails one and the game
    /// reads a compute shader's pipeline without a null check (0x14248c30d, the shader object's +0x120).
    /// </summary>
    public nint Unbuilt()
    {
        Interlocked.Increment(ref _created);

        return NewStandIn();
    }

    /// <summary>A count over the build queues (one when the heavy pipelines share the others' threads).</summary>
    private int Count(Func<PipelineBuildQueue, int> count)
    {
        if (ReferenceEquals(_builds, _heavyBuilds))
        {
            return count(_builds);
        }

        return count(_builds) + count(_heavyBuilds);
    }

    private nint NewStandIn()
    {
        var standIn = (StandInPipelineObject*)NativeMemory.AllocZeroed((nuint)sizeof(StandInPipelineObject));
        standIn->Vtable = (nint)_vtable;
        standIn->References = 1;
        standIn->Owner = GCHandle.ToIntPtr(_self);

        lock (_gate)
        {
            _live.Add((nint)standIn);
        }

        return (nint)standIn;
    }

    private void Complete(nint standIn, PipelineResult built, string what, long started, Action<nint>? notify)
    {
        var took = Stopwatch.GetElapsedTime(started);

        if (HResult.Failed(built.Hr) || built.Pipeline == 0)
        {
            lock (_gate)
            {
                _work.Remove(standIn);
            }

            _warnings.Warn($"late-{what}", $"[d3d12_xs] pipeline {what} failed after {took.TotalSeconds:F0} s: {HResult.Format(built.Hr)}; its draws and dispatches stay skipped");

            return;
        }

        notify?.Invoke(built.Pipeline);

        lock (_gate)
        {
            _work.Remove(standIn);

            if (_live.Contains(standIn))
            {
                Volatile.Write(ref ((StandInPipelineObject*)standIn)->Desktop, built.Pipeline);
                Interlocked.Increment(ref _ready);

                if (took > SlowBuild)
                {
                    _warnings.Warn($"late-{what}", $"[d3d12_xs] pipeline {what} built after {took.TotalSeconds:F0} s");
                }

                return;
            }
        }

        ComObject.Release(built.Pipeline);
    }
}
