// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using FiveStar.Loader.Shims.D3D12X.Pipelines;
using FiveStar.Loader.Tests.Support;
using Xunit;

namespace FiveStar.Loader.Tests;

/// <summary>The pipeline descriptions of the D3D12.X device slots, built to the layout the game's code writes.</summary>
public sealed unsafe class D3D12XPipelineTests
{
    [Fact]
    public void A_pipeline_stream_laid_out_like_the_game_reads_into_desktop_state()
    {
        using var memory = new NativeBlocks();
        var stream = GameStream(memory);

        var description = PipelineStreamParser.Parse(stream, 0x2B0);

        Assert.Equal((nint)0x1234, description.GameRootSignature);
        Assert.Equal(["d1", "d2d2"], description.Shaders.Select(shader => Convert.ToHexStringLower(shader.Bytes)));
        Assert.All(description.Shaders, shader => Assert.Equal(ShaderSourceKind.Container, shader.Kind));
        Assert.Equal((1, 5), (description.Blend[0x08], description.Blend[0x10]));
        Assert.Equal((2, 7), (description.Rasterizer[0x04], description.DepthStencil[0x08]));

        Assert.Equal(0xFFFFFFFFu, description.SampleMask);
        Assert.Equal(["POSITION", "TEXCOORD"], description.InputLayout.Select(element => element.Semantic));
        Assert.Equal((12, 1), (description.InputLayout[1].Offset, description.InputLayout[1].InputSlot));
        Assert.Equal((3, 28, 40, 1), (description.TopologyType, description.RenderTargetFormats.Single(), description.DepthStencilFormat, description.SampleCount));
    }

    [Fact]
    public void Derive_overrides_are_read_at_their_stride_with_the_desc_after_the_kind()
    {
        using var memory = new NativeBlocks();
        var entries = memory.Alloc(2 * 0x14C);
        *(int*)(entries + 4 + 4) = 2;
        *(int*)(entries + 0x14C) = 2;
        *(entries + 0x14C + 4 + 8) = 1;

        var overrides = PipelineDescReader.Overrides(2, entries);

        Assert.Equal([0, 2], overrides.Select(entry => entry.Kind));
        Assert.Equal((2, 1), (overrides[0].Payload[4], overrides[1].Payload[8]));
    }

    /// <summary>The 0x2B0-byte stream 0x14086f090 builds: fixed subobject offsets, empty DS/HS/GS slots.</summary>
    private static byte* GameStream(NativeBlocks memory)
    {
        var stream = memory.Alloc(0x2B0);
        (int Offset, int Type)[] types =
        [
            (0x00, 0), (0x10, 1), (0x28, 2), (0x40, 3), (0x58, 4), (0x70, 5), (0x88, 8), (0x1D8, 9), (0x1E0, 10),
            (0x210, 21), (0x250, 17), (0x260, 12), (0x278, 14), (0x280, 15), (0x2A8, 16),
        ];

        foreach (var (offset, type) in types)
        {
            *(int*)(stream + offset) = type;
        }

        *(nint*)(stream + 0x08) = 0x1234;
        memory.Range(stream + 0x18, [0xD1]);
        memory.Range(stream + 0x30, [0xD2, 0xD2]);

        *(int*)(stream + 0x8C + 0x08) = 1;
        *(int*)(stream + 0x8C + 0x10) = 5;
        *(uint*)(stream + 0x1DC) = uint.MaxValue;
        *(int*)(stream + 0x1E4 + 0x04) = 2;
        *(int*)(stream + 0x214 + 0x08) = 7;
        *(int*)(stream + 0x254) = 1;

        *(nint*)(stream + 0x268) = (nint)Elements(memory, ["POSITION", "TEXCOORD"]);
        *(int*)(stream + 0x270) = 2;
        *(int*)(stream + 0x27C) = 3;
        *(int*)(stream + 0x284) = 28;
        *(int*)(stream + 0x2A4) = 1;
        *(int*)(stream + 0x2AC) = 40;

        return stream;
    }

    /// <summary>D3D12_INPUT_ELEMENT_DESCs, element n in slot n at offset 12n.</summary>
    private static byte* Elements(NativeBlocks memory, string[] names)
    {
        var elements = memory.Alloc(names.Length * 0x20);

        for (var index = 0; index < names.Length; index++)
        {
            var element = elements + (index * 0x20);
            *(nint*)element = (nint)memory.Ansi(names[index]);
            *(int*)(element + 0x10) = index;
            *(int*)(element + 0x14) = index * 12;
        }

        return elements;
    }
}
