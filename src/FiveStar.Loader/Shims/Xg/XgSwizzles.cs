// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

namespace FiveStar.Loader.Shims.Xg;

/// <summary>
/// The block swizzles of the console's 4 KB and 64 KB swizzle modes per element size, as element-index bits from the
/// least significant (<c>x</c> = next x bit, <c>y</c> = next y bit). They are the patterns found on the shipped
/// textures (docs/textures.md); the block shapes are the D3D standard tile shapes. A 256-byte block uses the first
/// bits of the 4 KB pattern.
/// </summary>
internal static class XgSwizzles
{
    public const int LargeBlockBytes = 0x10000;
    public const int SmallBlockBytes = 0x1000;
    public const int MicroBlockBytes = 0x100;

    /// <summary>Element sizes with a pattern: 1, 2, 4, 8 and 16 bytes (index = log2 of the size).</summary>
    public const int ElementSizes = 5;

    private static readonly string[] SmallPatterns = ["xxxxyyyyyxyx", "xxxyyyxyxyx", "xxyyyxyxyx", "xyyxxyxyx", "yyxxyxyx"];
    private static readonly string[] LargePatterns = ["xxxyyyxyyxyxyxyx", "xxxyyyxyxyxyxyx", "xxyyxyyxyxyxyx", "xyxxyyxyxyxyx", "xyxyyxyxyxyx"];
    private static readonly XgSwizzle[] Small = Parse(SmallPatterns);
    private static readonly XgSwizzle[] Large = Parse(LargePatterns);

    /// <summary>The swizzle of a block size (bytes) and element size (log2 of bytes); the 256-byte block is a prefix.</summary>
    [HotPath]
    public static XgSwizzle Find(int blockBytes, int elementLog2)
    {
        if (blockBytes == LargeBlockBytes)
        {
            return Large[elementLog2];
        }

        var small = Small[elementLog2];

        if (blockBytes == SmallBlockBytes)
        {
            return small;
        }

        int microBits = small.Bits - (System.Numerics.BitOperations.Log2(SmallBlockBytes) - System.Numerics.BitOperations.Log2(MicroBlockBytes));

        return new XgSwizzle(small.XMask & ((1u << microBits) - 1), microBits);
    }

    private static XgSwizzle[] Parse(string[] patterns)
    {
        var swizzles = new XgSwizzle[patterns.Length];

        for (int size = 0; size < patterns.Length; size++)
        {
            string bits = patterns[size];
            uint xMask = 0;

            for (int bit = 0; bit < bits.Length; bit++)
            {
                if (bits[bit] == 'x')
                {
                    xMask |= 1u << bit;
                }
            }

            swizzles[size] = new XgSwizzle(xMask, bits.Length);
        }

        return swizzles;
    }
}
