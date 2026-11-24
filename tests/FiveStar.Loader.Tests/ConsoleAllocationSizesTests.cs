// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using FiveStar.Loader.Shims.D3D12X;
using Xunit;

namespace FiveStar.Loader.Tests;

/// <summary>Allocation info of console-layout textures: the XG size the game's archives were cut to (XgTests).</summary>
public sealed unsafe class ConsoleAllocationSizesTests
{
    private const int Texture2D = 3;
    private const int Bc1 = 71;
    private const int Small4KbRX = 0x100 + 23;
    private const int Rgba16Unorm = 11;
    private const int ConsoleFlags = 0x300000;
    private const int AllowRenderTarget = 1;

    [Fact]
    public void A_console_layout_texture_gets_its_xg_size_and_a_buffer_keeps_the_desktop_answer()
    {
        var descs = stackalloc ResourceDescription[2];
        descs[0] = new ResourceDescription { Dimension = Texture2D, Width = 256, Height = 128, DepthOrArraySize = 1, MipLevels = 8, Format = Bc1, SampleCount = 1, Layout = Small4KbRX };
        descs[1] = new ResourceDescription { Dimension = ResourceDescription.BufferDimension, Width = 0x100, Height = 1, DepthOrArraySize = 1, MipLevels = 1, SampleCount = 1 };
        var infos = stackalloc ResourceAllocationInfo1[1];
        ResourceAllocationInfo result = default;

        Assert.True(ConsoleAllocationSizes.TryApply(descs, 1, &result, infos));
        Assert.Equal((0x6000ul, 0x1000ul), (result.Size, result.Alignment));
        Assert.Equal((0ul, 0x6000ul), (infos[0].Offset, infos[0].Size));

        result = default;
        Assert.False(ConsoleAllocationSizes.TryApply(descs, 2, &result, null));
        Assert.Equal(0ul, result.Size);
    }

    [Fact]
    public void A_streamed_texture_without_a_layout_gets_the_suggested_4_kb_blocks()
    {
        // The texture of the archive chunk at the pad pairing: 128x4 R16G16B16A16 is 0x2000 bytes on the desktop and
        // four 4 KB blocks (32x16 elements each) on the console, the 0x4000 bytes its compressed chunk holds.
        var desc = new ResourceDescription { Dimension = Texture2D, Width = 128, Height = 4, DepthOrArraySize = 1, MipLevels = 1, Format = Rgba16Unorm, SampleCount = 1, Flags = ConsoleFlags };
        ResourceAllocationInfo result = default;

        Assert.True(ConsoleAllocationSizes.TryApply(&desc, 1, &result, null));
        Assert.Equal(0x4000ul, result.Size);

        desc.Flags |= AllowRenderTarget;
        Assert.False(ConsoleAllocationSizes.TryApply(&desc, 1, &result, null));
    }
}
