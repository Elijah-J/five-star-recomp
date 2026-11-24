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
    private readonly TimeSpan _budget;
    private readonly PipelineBuildQueue _builds;
    private readonly OnceLog _warnings;
    private readonly nint* _vtable;
    private readonly GCHandle _self;
    private int _created;
    private int _ready;
    private long _skipped;
    private long _recorded;

    /// <param name="budget">How long a creation may take before the game gets a stand-in.</param>
    /// <param name="builds">The threads that call the driver (started by the caller).</param>
    /// <param name="warnings">Where stand-ins and late pipelines are reported, once per name.</param>
    public StandInPipelines(TimeSpan budget, PipelineBuildQueue builds, OnceLog warnings)
    {
        _budget = budget;
        _builds = builds;
        _warnings = warnings;
        _vtable = StandInPipelineMethods.CreateVtable();
        _self = GCHandle.Alloc(this, GCHandleType.Weak);
    }

    /// <summary>Stand-ins handed out, how many have their pipeline, the builds still queued, the work skipped meanwhile and the work recorded.</summary>
    public string Summary => $"{Volatile.Read(ref _created)} stand-in pipelines ({Volatile.Read(ref _ready)} built since, {_builds.Backlog} builds queued), {Interlocked.Read(ref _skipped)} draws and dispatches skipped, {Interlocked.Read(ref _recorded)} recorded";

    /// <summary>
    /// Runs <paramref name="create"/> and writes its pipeline to <paramref name="result"/>; when it takes longer than
    /// the budget, or when earlier creations still wait for a thread, writes a stand-in instead and returns S_OK.
    /// <paramref name="built"/> is told the desktop pipeline when there is one.
    /// </summary>
    public int Create(Func<PipelineResult> create, nint* result, string what, Action<nint>? built)
    {
        var budget = _budget;

        if (_builds.Backlog > 0)
        {
            budget = TimeSpan.Zero;
        }

        long started = Stopwatch.GetTimestamp();
        var build = _builds.Enqueue(create);

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
        build.ContinueWith(task => Complete(standIn, task.Result, what, started, built), CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
        Interlocked.Increment(ref _created);
        _warnings.Warn("standin", $"[d3d12_xs] pipeline {what} not built in time: the game gets a stand-in, its draws and dispatches are skipped until the driver is done (counted in the sample summary)");
        *result = standIn;

        return HResult.Ok;
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

        nint desktop = Volatile.Read(ref ((StandInPipelineObject*)pipeline)->Desktop);
        pending = desktop == 0;

        return desktop;
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
            _warnings.Warn($"late-{what}", $"[d3d12_xs] pipeline {what} failed after {took.TotalSeconds:F0} s: {HResult.Format(built.Hr)}; its draws and dispatches stay skipped");

            return;
        }

        notify?.Invoke(built.Pipeline);

        lock (_gate)
        {
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
