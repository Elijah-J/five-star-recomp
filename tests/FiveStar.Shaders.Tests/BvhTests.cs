// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using FiveStar.Shaders.Tests.Cases;
using FiveStar.Shaders.Tests.Toolchain;
using Xunit;

namespace FiveStar.Shaders.Tests;

/// <summary>
/// Ray intersection: box32 / box16 / triangle nodes, sorting, box grow, return modes, bvh64, a16, partial EXEC and the
/// NSA form against a handcrafted BVH, compared with the RT IP 1.1 reference model.
/// </summary>
public sealed class BvhTests : IClassFixture<WarpFixture>
{
    private readonly WarpFixture _fixture;

    public BvhTests(WarpFixture fixture)
    {
        _fixture = fixture;
    }

    public static TheoryData<string> Cases => [.. BvhCases.All().Select(testCase => testCase.Name)];

    [WarpTheory]
    [MemberData(nameof(Cases))]
    public void A_ray_intersection_returns_what_the_hardware_returns(string name)
    {
        var mismatches = new ComputeCaseRunner(_fixture).Run(BvhCases.Find(name));

        Assert.True(mismatches.Count == 0, $"{mismatches.Count} mismatches:\n{string.Join("\n", mismatches.Take(16))}");
    }
}
