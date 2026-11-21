// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

namespace FiveStar.Formats.Ttpf;

/// <summary>
/// One 16-byte texture descriptor of the <c>ETPF</c> section: <c>d0</c> = width - 1 (bits 0-13), height - 1
/// (bits 14-27), mip count (bits 28-31); <c>d1</c> = DXGI format in bits 3-9, layout mode in bits 24-31;
/// <c>o0</c> bits 0-20 = virtual address / 0x1000; <c>o1</c> unknown.
/// </summary>
/// <param name="Index">Position in the descriptor table.</param>
/// <param name="Width">Width in pixels.</param>
/// <param name="Height">Height in pixels.</param>
/// <param name="MipCount">Mip count from <c>d0</c> (clamped; 0 in many mode-0 textures).</param>
/// <param name="D1">The raw <c>d1</c> word.</param>
/// <param name="Format">The DXGI format number.</param>
/// <param name="VirtualAddress">Start of the texture's span in the virtual address space.</param>
/// <param name="O0">The raw <c>o0</c> word.</param>
/// <param name="O1">The raw <c>o1</c> word.</param>
public sealed record TextureDescriptor(
    int Index,
    int Width,
    int Height,
    int MipCount,
    uint D1,
    int Format,
    long VirtualAddress,
    uint O0,
    uint O1)
{
    private const int ModeShift = 24;

    /// <summary>Gets the layout mode: base in bits 0-2 (0 = 64 KB tiles, 1-3 = 4 KB tiles), bit 3 = cube map.</summary>
    public int LayoutMode => (int)(D1 >> ModeShift);
}
