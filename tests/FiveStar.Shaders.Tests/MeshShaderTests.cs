// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using FiveStar.Shaders.Tests.Cases;
using FiveStar.Shaders.Tests.Toolchain;
using Xunit;

namespace FiveStar.Shaders.Tests;

/// <summary>
/// An NGG program through the two-phase mesh path, drawn natively and through the mesh-to-compute fallback (descriptor
/// table, root descriptors, and an overflowing dispatch whose extra group must be dropped).
/// </summary>
public sealed class MeshShaderTests : IClassFixture<WarpFixture>
{
    private readonly WarpFixture _fixture;

    public MeshShaderTests(WarpFixture fixture)
    {
        _fixture = fixture;
    }

    public static TheoryData<string> Variants => [.. MeshStageRunner.Variants];

    [WarpTheory]
    [MemberData(nameof(Variants))]
    public void A_mesh_program_draws_the_reference_image(string variant)
    {
        var mismatches = new MeshStageRunner(_fixture).Run(variant);

        Assert.True(mismatches.Count == 0, $"{mismatches.Count} mismatches:\n{string.Join("\n", mismatches.Take(16))}");
    }
}
