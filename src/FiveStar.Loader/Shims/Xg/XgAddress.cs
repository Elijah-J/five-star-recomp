// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

namespace FiveStar.Loader.Shims.Xg;

/// <summary>Byte offsets of elements inside a resource laid out by <see cref="XgLayoutBuilder"/>.</summary>
internal static class XgAddress
{
    /// <summary>
    /// The offset from the resource start of element (x, y) of a mip of an array slice. Blocks are row-major across
    /// the mip's padded pitch; a tail mip is addressed with the whole block's swizzle from its first element.
    /// </summary>
    [HotPath]
    public static ulong Element(in XgPlane plane, int mipIndex, int slice, uint x, uint y)
    {
        ref readonly XgMip mip = ref plane.Mips[mipIndex];
        ulong sliceStart = plane.Offset + ((ulong)slice * plane.SliceStride);
        ulong bytesPerElement = (ulong)plane.BytesPerElement;

        if (plane.BlockBytes == 0)
        {
            return sliceStart + mip.Offset + ((((ulong)y * mip.PitchElements) + x) * bytesPerElement);
        }

        var swizzle = plane.Swizzle;

        if (mip.InTail != 0)
        {
            return sliceStart + ((ulong)swizzle.Index(mip.TailX + x, mip.TailY + y) * bytesPerElement);
        }

        uint tileWidth = (uint)swizzle.TileWidth;
        uint tileHeight = (uint)swizzle.TileHeight;
        ulong tile = ((ulong)(y / tileHeight) * (mip.PitchElements / tileWidth)) + (x / tileWidth);
        ulong inside = (ulong)swizzle.Index(x % tileWidth, y % tileHeight) * bytesPerElement;

        return sliceStart + mip.Offset + (tile * (ulong)plane.BlockBytes) + inside;
    }
}
