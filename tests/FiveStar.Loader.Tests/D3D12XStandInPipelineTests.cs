// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Runtime.InteropServices;
using FiveStar.Loader.Diagnostics;
using FiveStar.Loader.Native;
using FiveStar.Loader.Shims.D3D12X.Pipelines;
using FiveStar.Loader.Tests.Support;
using Xunit;

namespace FiveStar.Loader.Tests;

/// <summary>
/// Pipeline creations the driver takes too long for: the game gets a stand-in at once, the stand-in turns into the
/// desktop pipeline when the build ends, and its last Release releases that pipeline.
/// </summary>
public sealed unsafe class D3D12XStandInPipelineTests
{
    private const int ReleaseSlot = 2;
    private const int Slots = 3;

    /// <summary>Upper bound for the background build in the test; it ends as soon as the gate opens.</summary>
    private static readonly TimeSpan BuildLimit = TimeSpan.FromSeconds(30);

    [Fact]
    public void A_creation_within_the_budget_returns_the_desktop_pipeline_itself()
    {
        using var memory = new NativeBlocks();
        nint pipeline = FakePipeline(memory);
        var standIns = StandIns(TimeSpan.FromMinutes(1));
        nint result = 0;

        int hr = standIns.Create(() => new PipelineResult(HResult.Ok, pipeline), &result, "fast", null);

        Assert.Equal((HResult.Ok, pipeline), (hr, result));
        Assert.Equal(pipeline, standIns.Desktop(result, out bool pending));
        Assert.False(pending);
    }

    [Fact]
    public void A_slow_creation_hands_out_a_stand_in_that_becomes_the_pipeline_and_releases_it()
    {
        using var memory = new NativeBlocks();
        using var gate = new ManualResetEventSlim();
        nint pipeline = FakePipeline(memory);
        nint seen = 0;
        var standIns = StandIns(TimeSpan.Zero);
        nint result = 0;

        int hr = standIns.Create(() => Slow(gate, pipeline), &result, "slow", built => seen = built);

        Assert.Equal(HResult.Ok, hr);
        Assert.NotEqual(pipeline, result);
        Assert.Equal(0, standIns.Desktop(result, out bool pending));
        Assert.True(pending);

        gate.Set();
        nint standIn = result;
        Assert.True(SpinWait.SpinUntil(() => standIns.Desktop(standIn, out _) != 0, BuildLimit));
        Assert.Equal((pipeline, pipeline), (standIns.Desktop(result, out pending), seen));
        Assert.False(pending);

        uint left = ((delegate* unmanaged<nint, uint>)ComObject.Slot(result, ReleaseSlot))(result);
        Assert.Equal((0u, 1L), (left, *(long*)(pipeline + sizeof(nint))));
    }

    private static StandInPipelines StandIns(TimeSpan budget)
    {
        var builds = new PipelineBuildQueue(1);
        builds.Start();

        return new StandInPipelines(budget, builds, new OnceLog(new LoaderLog(new StringWriter(), null, false)));
    }

    private static PipelineResult Slow(ManualResetEventSlim gate, nint pipeline)
    {
        gate.Wait(BuildLimit);

        return new PipelineResult(HResult.Ok, pipeline);
    }

    /// <summary>A COM object whose Release counts its calls in the word after the vtable.</summary>
    private static nint FakePipeline(NativeBlocks memory)
    {
        var vtable = (nint*)memory.Alloc(Slots * sizeof(nint));
        vtable[ReleaseSlot] = (nint)(delegate* unmanaged<nint, uint>)&CountRelease;
        var pipeline = memory.Alloc(2 * sizeof(nint));
        *(nint*)pipeline = (nint)vtable;

        return (nint)pipeline;
    }

    [UnmanagedCallersOnly]
    private static uint CountRelease(nint self)
    {
        Interlocked.Increment(ref *(long*)(self + sizeof(nint)));

        return 0;
    }
}
