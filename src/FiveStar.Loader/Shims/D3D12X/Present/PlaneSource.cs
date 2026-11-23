// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

namespace FiveStar.Loader.Shims.D3D12X.Present;

/// <summary>
/// What one present shows: the plane's desktop resource, the state it rests in, the part of it to copy, the back-buffer size and format,
/// and whether a plain copy can do it (otherwise the test pattern stands in).
/// </summary>
internal readonly record struct PlaneSource(nint Resource, int State, CopyBox Box, int Format, uint ColorSpace, bool Copyable)
{
    public uint Width => Box.Right - Box.Left;

    public uint Height => Box.Bottom - Box.Top;
}
