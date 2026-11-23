// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

namespace FiveStar.Loader.Shims.D3D12X.Pipelines;

/// <summary>
/// Desktop DSVFormats for the console's (dxgiformat.h). The pipeline cache stores DXGI values; one is console-only:
/// 118, D16_UNORM_S8_UINT, which the desktop lacks and the game treats like D32_FLOAT_S8X24_UINT (XgFormats).
/// </summary>
internal static class DepthFormats
{
    /// <summary>The console's DXGI_FORMAT_D16_UNORM_S8_UINT.</summary>
    public const int ConsoleD16S8 = 118;

    /// <summary>DXGI_FORMAT_D32_FLOAT_S8X24_UINT: a desktop depth format with stencil that holds 16-bit depth exactly.</summary>
    public const int D32S8 = 20;

    /// <summary>The desktop format for the console DSVFormat <paramref name="format"/>; others pass unchanged.</summary>
    public static int Desktop(int format)
    {
        if (format == ConsoleD16S8)
        {
            return D32S8;
        }

        return format;
    }
}
