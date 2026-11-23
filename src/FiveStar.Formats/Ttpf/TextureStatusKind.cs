// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

namespace FiveStar.Formats.Ttpf;

/// <summary>Why a texture's mip 0 can or cannot be read.</summary>
public enum TextureStatusKind
{
    /// <summary>Layout understood and every page under mip 0 can be decoded.</summary>
    Ok,

    /// <summary>The DXGI format is not supported (detail = format).</summary>
    UnsupportedFormat,

    /// <summary>The layout mode has no known swizzle (detail = mode).</summary>
    UnsupportedMode,

    /// <summary>A cube map whose span does not split into six faces.</summary>
    CubeSpan,

    /// <summary>Mip 0 does not fit inside the texture's span.</summary>
    Size,

    /// <summary>A cube map with mips (face and mip interleave not known).</summary>
    CubeMips,

    /// <summary>Mip 0 touches a page that cannot be decoded (a BCPack codec other than BC1).</summary>
    UnsupportedCodec,

    /// <summary>Coverage only: mip 0 touches a BCPack page that cannot be decoded (detail = codec).</summary>
    BcPack,

    /// <summary>Coverage only: mip 0 reads, but through a BCPack codec whose decode is approximate (detail = codec).</summary>
    Approximate,
}
