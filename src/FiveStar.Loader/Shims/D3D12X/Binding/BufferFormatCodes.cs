// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

namespace FiveStar.Loader.Shims.D3D12X.Binding;

/// <summary>
/// The format code a typed buffer view's V# carries in dword 3 for translated programs: layout × 8 + numeric type in
/// GCN BUF_DATA_FORMAT / BUF_NUM_FORMAT numbering, which the prelude's FmtLoad and FmtStore decode (rdna_prelude.hlsli).
/// Not the hardware dword 3: FmtLoad reads the whole dword as the code, so no destination swizzle is carried and
/// B8G8R8A8 reads in memory order.
/// </summary>
internal static class BufferFormatCodes
{
    /// <summary>No format: FmtLoad reads zeros.</summary>
    public const uint None = 0;

    private const uint LayoutShift = 8;

    // Layouts.
    private const uint Layout8 = 1;
    private const uint Layout16 = 2;
    private const uint Layout8x2 = 3;
    private const uint Layout32 = 4;
    private const uint Layout16x2 = 5;
    private const uint Layout10x11x11 = 6;
    private const uint Layout2x10x10x10 = 9;
    private const uint Layout8x4 = 10;
    private const uint Layout32x2 = 11;
    private const uint Layout16x4 = 12;
    private const uint Layout32x3 = 13;
    private const uint Layout32x4 = 14;

    // Numeric types.
    private const uint Unorm = 0;
    private const uint Snorm = 1;
    private const uint Uint = 4;
    private const uint Sint = 5;
    private const uint Float = 7;

    /// <summary>32 UINT: one dword as stored, for raw and structured views.</summary>
    public const uint Dword = (Layout32 * LayoutShift) + Uint;

    /// <summary>32_32_32_32 FLOAT: a constant buffer's float4 rows.</summary>
    public const uint Float4 = (Layout32x4 * LayoutShift) + Float;

    /// <summary>DXGI_FORMAT (dxgiformat.h) and its layout and numeric type.</summary>
    private static readonly (int Format, uint Layout, uint Numeric)[] Formats =
    [
        (2, Layout32x4, Float), (3, Layout32x4, Uint), (4, Layout32x4, Sint),
        (6, Layout32x3, Float), (7, Layout32x3, Uint), (8, Layout32x3, Sint),
        (10, Layout16x4, Float), (11, Layout16x4, Unorm), (12, Layout16x4, Uint), (13, Layout16x4, Snorm), (14, Layout16x4, Sint),
        (16, Layout32x2, Float), (17, Layout32x2, Uint), (18, Layout32x2, Sint),
        (24, Layout2x10x10x10, Unorm), (25, Layout2x10x10x10, Uint), (26, Layout10x11x11, Float),
        (28, Layout8x4, Unorm), (29, Layout8x4, Unorm), (30, Layout8x4, Uint), (31, Layout8x4, Snorm), (32, Layout8x4, Sint),
        (34, Layout16x2, Float), (35, Layout16x2, Unorm), (36, Layout16x2, Uint), (37, Layout16x2, Snorm), (38, Layout16x2, Sint),
        (39, Layout32, Uint), (40, Layout32, Float), (41, Layout32, Float), (42, Layout32, Uint), (43, Layout32, Sint),
        (49, Layout8x2, Unorm), (50, Layout8x2, Uint), (51, Layout8x2, Snorm), (52, Layout8x2, Sint),
        (54, Layout16, Float), (55, Layout16, Unorm), (56, Layout16, Unorm), (57, Layout16, Uint), (58, Layout16, Snorm), (59, Layout16, Sint),
        (61, Layout8, Unorm), (62, Layout8, Uint), (63, Layout8, Snorm), (64, Layout8, Sint),
        (87, Layout8x4, Unorm), (91, Layout8x4, Unorm),
    ];

    private static readonly uint[] ByFormat = Table();

    /// <summary>The code of a DXGI_FORMAT; <see cref="None"/> for a format no buffer load decodes.</summary>
    [HotPath]
    public static uint Of(int format)
    {
        if ((uint)format >= (uint)ByFormat.Length)
        {
            return None;
        }

        return ByFormat[format];
    }

    private static uint[] Table()
    {
        var table = new uint[Formats.Max(entry => entry.Format) + 1];

        foreach (var (format, layout, numeric) in Formats)
        {
            table[format] = (layout * LayoutShift) + numeric;
        }

        return table;
    }
}
