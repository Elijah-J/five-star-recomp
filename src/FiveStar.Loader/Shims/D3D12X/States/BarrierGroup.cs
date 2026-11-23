// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Runtime.InteropServices;

namespace FiveStar.Loader.Shims.D3D12X.States;

/// <summary>D3D12_BARRIER_GROUP (d3d12.h): a barrier type, a count and the barriers of that type.</summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct BarrierGroup
{
    /// <summary>D3D12_BARRIER_TYPE_TEXTURE.</summary>
    public const int TextureType = 1;

    public int Type;
    public uint Count;
    public void* Barriers;
}
