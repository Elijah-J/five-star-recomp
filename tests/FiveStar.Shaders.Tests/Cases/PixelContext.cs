// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

namespace FiveStar.Shaders.Tests.Cases;

/// <summary>
/// One pixel of a pixel-shader case and its place in the PC wave (from the probe shader): WARP runs 4-lane waves, one
/// 2x2 quad per wave, so lane equals quad slot (0 top-left, 1 top-right, 2 bottom-left, 3 bottom-right).
/// </summary>
public readonly record struct PixelContext(int X, int Y, int Lane, int Quad, int WaveSize)
{
    /// <summary>The pixel at quad slot <paramref name="slot"/> of this pixel's quad.</summary>
    public (int X, int Y) QuadPixel(int slot)
    {
        return ((X & ~1) | (slot & 1), (Y & ~1) | (slot >> 1));
    }

    /// <summary>The pixel in lane <paramref name="lane"/> of this pixel's PC wave (one quad on WARP).</summary>
    public (int X, int Y) WavePixel(int lane)
    {
        if (WaveSize != 4)
        {
            throw new InvalidOperationException($"expected values assume WARP's 4-lane waves, this device runs {WaveSize}");
        }

        return QuadPixel(lane);
    }
}
