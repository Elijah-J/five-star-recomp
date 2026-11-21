// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

namespace FiveStar.Formats.Ttpf;

/// <summary>Where and how a texture's mip 0 is stored.</summary>
/// <param name="Format">The texture's format.</param>
/// <param name="BlockWidth">Width in elements.</param>
/// <param name="BlockHeight">Height in elements.</param>
/// <param name="Pattern">The swizzle of mip 0 (a prefix of the tile pattern when mip 0 sits in the mip tail).</param>
/// <param name="Size">Bytes of mip 0 padded to whole tiles of <paramref name="Pattern"/>.</param>
/// <param name="Start">Virtual address of mip 0 (of face 0 for a cube map).</param>
/// <param name="Faces">1, or 6 for a cube map.</param>
/// <param name="FaceSpan">Bytes between faces (0 for a 2D texture).</param>
public sealed record TextureLayout(
    TextureFormat Format,
    int BlockWidth,
    int BlockHeight,
    SwizzlePattern Pattern,
    long Size,
    long Start,
    int Faces,
    long FaceSpan);
