// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

namespace FiveStar.Formats.Ttpf;

/// <summary>
/// A tile swizzle given as element-address bits, least significant first: bit k of an element's index inside a tile
/// is the next x bit (<c>x</c>) or the next y bit (<c>y</c>) of its position. A pattern with a x bits and b y bits
/// describes a 2^a by 2^b element tile; tiles are laid out row-major.
/// </summary>
public sealed class SwizzlePattern
{
    private readonly int[] _x;
    private readonly int[] _y;

    public SwizzlePattern(string bits)
    {
        Bits = bits;
        var xBits = 0;

        foreach (var bit in bits)
        {
            if (bit == 'x')
            {
                xBits++;
            }
        }

        TileWidth = 1 << xBits;
        TileHeight = 1 << (bits.Length - xBits);
        _x = new int[TileWidth * TileHeight];
        _y = new int[TileWidth * TileHeight];
        FillCoordinates();
    }

    public string Bits { get; }

    /// <summary>Gets the tile width in elements.</summary>
    public int TileWidth { get; }

    /// <summary>Gets the tile height in elements.</summary>
    public int TileHeight { get; }

    /// <summary>Gets the element count of one tile.</summary>
    public int TileElements => TileWidth * TileHeight;

    /// <summary>Gets the x position inside its tile of the element at an index.</summary>
    public int ElementX(int index)
    {
        return _x[index];
    }

    /// <summary>Gets the y position inside its tile of the element at an index.</summary>
    public int ElementY(int index)
    {
        return _y[index];
    }

    /// <summary>
    /// The shortest prefix whose tile covers <paramref name="width"/> by <paramref name="height"/> elements, or the
    /// whole pattern. A texture whose mip 0 fits inside one tile is swizzled with that prefix.
    /// </summary>
    public string FitBits(int width, int height)
    {
        var xBits = 0;

        for (var length = 0; length <= Bits.Length; length++)
        {
            if (length > 0 && Bits[length - 1] == 'x')
            {
                xBits++;
            }

            if ((1L << xBits) >= width && (1L << (length - xBits)) >= height)
            {
                return Bits[..length];
            }
        }

        return Bits;
    }

    /// <summary>Bytes of a width x height element image padded to whole tiles.</summary>
    public long PaddedSize(int width, int height, int bytesPerElement)
    {
        var columns = (width + TileWidth - 1) / TileWidth;
        var rows = (height + TileHeight - 1) / TileHeight;

        return (long)columns * TileWidth * rows * TileHeight * bytesPerElement;
    }

    /// <summary>Turns tiled elements into row-major elements, cropping the tile padding.</summary>
    public byte[] Unswizzle(ReadOnlySpan<byte> tiled, int width, int height, int bytesPerElement)
    {
        var columns = (width + TileWidth - 1) / TileWidth;
        var rows = (height + TileHeight - 1) / TileHeight;
        var linear = new byte[(long)width * height * bytesPerElement];

        for (var tileRow = 0; tileRow < rows; tileRow++)
        {
            for (var tileColumn = 0; tileColumn < columns; tileColumn++)
            {
                var tile = ((long)tileRow * columns) + tileColumn;
                CopyTile(tiled, linear, tile * TileElements * bytesPerElement, tileColumn * TileWidth, tileRow * TileHeight, width, height, bytesPerElement);
            }
        }

        return linear;
    }

    private void CopyTile(ReadOnlySpan<byte> tiled, byte[] linear, long tileStart, int left, int top, int width, int height, int bytesPerElement)
    {
        for (var element = 0; element < TileElements; element++)
        {
            var x = left + _x[element];
            var y = top + _y[element];

            if (x >= width || y >= height)
            {
                continue;
            }

            var source = tiled.Slice((int)(tileStart + ((long)element * bytesPerElement)), bytesPerElement);
            source.CopyTo(linear.AsSpan((int)((((long)y * width) + x) * bytesPerElement)));
        }
    }

    private void FillCoordinates()
    {
        for (var element = 0; element < _x.Length; element++)
        {
            var x = 0;
            var y = 0;
            var xBit = 0;
            var yBit = 0;

            for (var bit = 0; bit < Bits.Length; bit++)
            {
                var value = (element >> bit) & 1;

                if (Bits[bit] == 'x')
                {
                    x |= value << xBit;
                    xBit++;
                }
                else
                {
                    y |= value << yBit;
                    yBit++;
                }
            }

            _x[element] = x;
            _y[element] = y;
        }
    }
}
