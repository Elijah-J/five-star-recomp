// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Collections.Concurrent;

namespace FiveStar.Loader.Shims.D3D12X.Pipelines;

/// <summary>
/// A fixed set of background threads that run desktop pipeline creations in order, so creations the game no longer
/// waits for (<see cref="StandInPipelines"/>) never take more driver compiler threads than these. A thread starts a
/// creation only while the machine has the memory for it: the driver's compiles of a story load's backlog took a
/// 64 GB machine to its commit limit, which ended the process (docs/d3d12x-pipelines.md#stand-ins).
/// </summary>
internal sealed class PipelineBuildQueue
{
    private const int PauseMilliseconds = 2000;

    /// <summary>For <see cref="PipelineBuildQueue(int, Func{bool})"/>: creations start whenever a thread is free.</summary>
    public static readonly Func<bool> AnyTime = () => true;

    private readonly BlockingCollection<PipelineBuild> _builds = [];
    private readonly Func<bool> _room;
    private int _waiting;

    /// <param name="workers">Threads that call the driver; at least one.</param>
    /// <param name="room">Whether a creation may start now; polled until it says so.</param>
    public PipelineBuildQueue(int workers, Func<bool> room)
    {
        Workers = Math.Max(1, workers);
        _room = room;
    }

    /// <summary>The number of threads that run creations.</summary>
    public int Workers { get; }

    /// <summary>Creations queued and not yet started.</summary>
    public int Backlog => _builds.Count;

    /// <summary>Threads waiting for memory before their next creation.</summary>
    public int WaitingForMemory => Volatile.Read(ref _waiting);

    /// <summary>Starts the threads; call once.</summary>
    public void Start()
    {
        for (int i = 0; i < Workers; i++)
        {
            var thread = new Thread(Run) { IsBackground = true, Name = "fivestar pipeline build" };
            thread.Start();
        }
    }

    /// <summary>Queues <paramref name="create"/>; the task ends with its result.</summary>
    public Task<PipelineResult> Enqueue(Func<PipelineResult> create)
    {
        var build = new PipelineBuild(create, new TaskCompletionSource<PipelineResult>(TaskCreationOptions.RunContinuationsAsynchronously));
        _builds.Add(build);

        return build.Done.Task;
    }

    private void Run()
    {
        foreach (var build in _builds.GetConsumingEnumerable())
        {
            WaitForRoom();
            build.Done.SetResult(build.Create());
        }
    }

    private void WaitForRoom()
    {
        if (_room())
        {
            return;
        }

        Interlocked.Increment(ref _waiting);

        while (!_room())
        {
            Thread.Sleep(PauseMilliseconds);
        }

        Interlocked.Decrement(ref _waiting);
    }
}
