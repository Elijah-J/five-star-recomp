// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using FiveStar.Graphics;
using FiveStar.Graphics.Programs;
using Xunit;

namespace FiveStar.Cli.Tests;

/// <summary>Each adapter runs the form of a program that gives console results, or the program is reported.</summary>
public sealed class VariantSelectionTests
{
    private static readonly AdapterInfo Nvidia = new(0, "wave32 with mesh shaders", false, "6.8", 32, 32, "Tier1");
    private static readonly AdapterInfo Amd = new(1, "wave32-64 without mesh shaders", false, "6.8", 32, 64, "NotSupported");

    private static readonly ProgramVariants PinnedCompute = new("cs", "cs", "cs.dxil", null, null, 64, "cs.w32.dxil", null);
    private static readonly ProgramVariants Mesh = new("ms", "ms", "ms.dxil", 64, 32, null, null, new MeshFallbackFiles("ms.msfb_cs.dxil", "ms.msfb_vs.dxil"));
    private static readonly ProgramVariants WidePixel = new("ps", "ps", "ps.dxil", 64, 64, null, null, null);

    [Fact]
    public void Wave_pins_mesh_support_and_floors_pick_the_form()
    {
        Assert.Equal((ChoiceOutcome.Variant, "cs.w32.dxil"), Pick(Nvidia, PinnedCompute));
        Assert.Equal((ChoiceOutcome.Native, "cs.dxil"), Pick(Amd, PinnedCompute));

        Assert.Equal((ChoiceOutcome.Native, "ms.dxil"), Pick(Nvidia, Mesh));
        Assert.Equal((ChoiceOutcome.Fallback, "ms.msfb_cs.dxil"), Pick(Amd, Mesh));

        // The AMD driver may run a pixel shader on 32-lane waves, below the floor of 64.
        Assert.Equal(ChoiceOutcome.Incorrect, VariantSelector.Choose(Nvidia, WidePixel).Outcome);
        Assert.Equal(ChoiceOutcome.Incorrect, VariantSelector.Choose(Amd, WidePixel).Outcome);
    }

    private static (ChoiceOutcome Outcome, string File) Pick(AdapterInfo adapter, ProgramVariants program)
    {
        var choice = VariantSelector.Choose(adapter, program);

        return (choice.Outcome, choice.Files[0]);
    }
}
