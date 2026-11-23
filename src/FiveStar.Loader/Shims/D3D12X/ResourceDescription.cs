// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Runtime.InteropServices;

namespace FiveStar.Loader.Shims.D3D12X;

/// <summary>D3D12_RESOURCE_DESC (d3d12.h); device slot 133 takes the same 56 bytes.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct ResourceDescription
{
    /// <summary>D3D12_RESOURCE_DIMENSION_BUFFER.</summary>
    public const int BufferDimension = 1;

    public int Dimension;
    public ulong Alignment;
    public ulong Width;
    public uint Height;
    public ushort DepthOrArraySize;
    public ushort MipLevels;
    public int Format;
    public uint SampleCount;
    public uint SampleQuality;
    public int Layout;
    public int Flags;
}
