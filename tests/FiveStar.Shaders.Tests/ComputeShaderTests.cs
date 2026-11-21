// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using FiveStar.Shaders.Tests.Cases;
using FiveStar.Shaders.Tests.Toolchain;
using Xunit;

namespace FiveStar.Shaders.Tests;

/// <summary>
/// Handwritten compute programs (thread / group ids, user SGPRs, LDS, wave operations as wave32 and wave64, buffer and
/// global memory, atomics) translated, run on WARP and compared with the ISA reference model per thread.
/// </summary>
public sealed class ComputeShaderTests : IClassFixture<WarpFixture>
{
    private const string FullWaveReason = "needs a PC wave as wide as the AMD wave (32 or 64 lanes); WARP runs 4-lane waves";

    private readonly WarpFixture _fixture;

    public ComputeShaderTests(WarpFixture fixture)
    {
        _fixture = fixture;
    }

    public static TheoryData<string> Cases => [.. ComputeCases.All().Select(testCase => testCase.Name)];

    public static TheoryData<string> FullWaveCases => [.. ComputeCases.FullWave().Select(testCase => testCase.Name)];

    [WarpTheory]
    [MemberData(nameof(Cases))]
    [MemberData(nameof(FullWaveCases), Skip = FullWaveReason)]
    public void A_compute_program_computes_what_the_isa_defines(string name)
    {
        var mismatches = new ComputeCaseRunner(_fixture).Run(ComputeCases.Find(name));

        Assert.True(mismatches.Count == 0, $"{mismatches.Count} mismatches:\n{string.Join("\n", mismatches.Take(16))}");
    }
}
