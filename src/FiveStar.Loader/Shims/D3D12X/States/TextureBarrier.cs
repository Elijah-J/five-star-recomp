// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Runtime.InteropServices;

namespace FiveStar.Loader.Shims.D3D12X.States;

/// <summary>D3D12_TEXTURE_BARRIER (d3d12.h) over all subresources.</summary>
[StructLayout(LayoutKind.Explicit, Size = 64)]
internal struct TextureBarrier
{
    /// <summary>D3D12_BARRIER_SUBRESOURCE_RANGE.IndexOrFirstMipLevel for every subresource.</summary>
    private const uint AllSubresources = 0xFFFFFFFF;

    [FieldOffset(0)]
    public int SyncBefore;

    [FieldOffset(4)]
    public int SyncAfter;

    [FieldOffset(8)]
    public int AccessBefore;

    [FieldOffset(12)]
    public int AccessAfter;

    [FieldOffset(16)]
    public int LayoutBefore;

    [FieldOffset(20)]
    public int LayoutAfter;

    [FieldOffset(24)]
    public nint Resource;

    /// <summary>Subresources: IndexOrFirstMipLevel, then mip, array and plane counts (0 with all subresources).</summary>
    [FieldOffset(32)]
    public uint FirstSubresource;

    [FieldOffset(56)]
    public int Flags;

    [HotPath]
    public static TextureBarrier Of(nint resource, BarrierScope before, BarrierScope after)
    {
        return new TextureBarrier
        {
            SyncBefore = before.Sync,
            SyncAfter = after.Sync,
            AccessBefore = before.Access,
            AccessAfter = after.Access,
            LayoutBefore = before.Layout,
            LayoutAfter = after.Layout,
            Resource = resource,
            FirstSubresource = AllSubresources,
        };
    }
}
