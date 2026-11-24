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
    public void Pipeline_creations_of_used_stand_ins_overtake_speculative_ones_and_run_once()
    {
        var room = false;
        var order = new ConcurrentQueue<string>();
        var queue = new PipelineBuildQueue(1, _ => Volatile.Read(ref room));
        queue.Start();

        Func<PipelineResult> Named(string name) => () =>
        {
            order.Enqueue(name);

            return default;
        };

        // While no memory is free nothing is taken, so the creations a command list needs go first once there is.
        queue.Queue(Named("journal 1"), urgent: false);
        var promoted = queue.Queue(Named("journal 2, then set"), urgent: false);
        queue.Queue(Named("first use"), urgent: true);
        queue.Promote(promoted);
        queue.Promote(promoted);
        Volatile.Write(ref room, true);

        Assert.True(SpinWait.SpinUntil(() => order.Count == 3, Limit));
        Thread.Sleep(100);
        Assert.Equal(["first use", "journal 2, then set", "journal 1"], order.ToArray());
    }

    [Theory]
    [InlineData(@"?:\pagefile.sys", 64, 18, 41, 25)]
    [InlineData(@"C:\pagefile.sys 0 0", 64, 18, 200, 174)]
    [InlineData(@"C:\pagefile.sys 16384 32768", 64, 32, 200, 0)]
    [InlineData(@"C:\pagefile.sys 16384 65536", 64, 20, 30, 14)]
    public void The_page_files_can_grow_to_their_maximum_within_the_free_disk(string entry, int physical, int pageFiles, int free, int expected)
    {
        const int Gigabyte = 30;
        var headroom = PageFileGrowth.Headroom([entry], (ulong)physical << Gigabyte, (ulong)pageFiles << Gigabyte, _ => (ulong)free << Gigabyte);

        Assert.Equal((ulong)expected << Gigabyte, headroom);
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
