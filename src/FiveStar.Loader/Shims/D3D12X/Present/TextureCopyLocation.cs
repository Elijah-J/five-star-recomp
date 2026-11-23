// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Runtime.InteropServices;

namespace FiveStar.Loader.Shims.D3D12X.Present;

/// <summary>D3D12_TEXTURE_COPY_LOCATION of type SUBRESOURCE_INDEX (d3d12.h); the union is a placed footprint wide.</summary>
[StructLayout(LayoutKind.Explicit, Size = 48)]
internal struct TextureCopyLocation
{
    [FieldOffset(0)]
    public nint Resource;

    [FieldOffset(8)]
    public int Type;

    [FieldOffset(16)]
    public uint Subresource;
}
