// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Collections.Concurrent;
using FiveStar.Loader.Shims.D3D12X.Pipelines;
using Xunit;

namespace FiveStar.Loader.Tests;

/// <summary>
/// Programs made on demand: work a command list waits for runs before prefetched work, and a prefetched program asked
/// for urgently is queued once more but made once.
/// </summary>
public sealed class ProgramWorkerTests
{
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(30);

    [Fact]
    public void Urgent_work_overtakes_the_prefetch_queue()
    {
        using var gate = new ManualResetEventSlim();
        var order = new ConcurrentQueue<string>();
        var workers = new ProgramWorkers(1, PipelineBuildQueue.AnyTime, PipelineBuildQueue.AnyTime);
        workers.Start();

        workers.Queue(() => gate.Wait(Limit), urgent: false);
        Assert.True(SpinWait.SpinUntil(() => workers.Running == 1, Limit));
        workers.Queue(() => order.Enqueue("journal"), urgent: false);
        workers.Queue(() => order.Enqueue("first use"), urgent: true);
        gate.Set();

        Assert.True(SpinWait.SpinUntil(() => order.Count == 2, Limit));
        Assert.Equal(["first use", "journal"], order.ToArray());
    }

    [Fact]
    public void A_prefetched_job_is_queued_again_when_urgent_and_runs_once()
    {
        var job = new ProgramJob("program", 0);

        Assert.True(job.Queue(urgent: false));
        Assert.False(job.Queue(urgent: false));
        Assert.True(job.Queue(urgent: true));
        Assert.False(job.Queue(urgent: true));

        Assert.True(job.Start());
        Assert.False(job.Start());
        Assert.False(job.Queue(urgent: true));
    }
}
