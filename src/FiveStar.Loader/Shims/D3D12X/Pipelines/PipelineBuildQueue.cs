// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Collections.Concurrent;

namespace FiveStar.Loader.Shims.D3D12X.Pipelines;

/// <summary>
/// A fixed set of background threads that run desktop pipeline creations in order, so creations the game no longer
/// waits for (<see cref="StandInPipelines"/>) never take more driver compiler threads than these.
/// </summary>
internal sealed class PipelineBuildQueue
{
    private readonly BlockingCollection<PipelineBuild> _builds = [];

    /// <param name="workers">Threads that call the driver; at least one.</param>
    public PipelineBuildQueue(int workers)
    {
        Workers = Math.Max(1, workers);
    }

    /// <summary>The number of threads that run creations.</summary>
    public int Workers { get; }

    /// <summary>Creations queued and not yet started.</summary>
    public int Backlog => _builds.Count;

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
            build.Done.SetResult(build.Create());
        }
    }
}
