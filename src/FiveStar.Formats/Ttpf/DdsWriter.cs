// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Buffers.Binary;

namespace FiveStar.Formats.Ttpf;

/// <summary>
/// Writes a one-mip DDS file with a DX10 header (layout from the DirectDraw Surface documentation:
/// <c>DDS_HEADER</c> of 124 bytes after the magic, then <c>DDS_HEADER_DXT10</c> of 20 bytes).
/// </summary>
public static class DdsWriter
{
    private const int HeaderSize = 124;
    private const int Dx10HeaderSize = 20;
    private const int MagicSize = 4;

    // DDS_HEADER field offsets from the start of the file.
    private const int SizeField = 4;
    private const int FlagsField = 8;
    private const int HeightField = 12;
    private const int WidthField = 16;
    private const int PitchField = 20;
    private const int MipCountField = 28;
    private const int PixelFormatSizeField = 76;
    private const int PixelFormatFlagsField = 80;
    private const int FourCcField = 84;
    private const int CapsField = 108;
    private const int Caps2Field = 112;

    // DDSD_CAPS | DDSD_HEIGHT | DDSD_WIDTH | DDSD_PIXELFORMAT | DDSD_LINEARSIZE.
    private const uint HeaderFlags = 0x1 | 0x2 | 0x4 | 0x1000 | 0x80000;
    private const uint PixelFormatSize = 32;
    private const uint FourCcFlag = 0x4;
    private const uint CapsTexture = 0x1000;
    private const uint CapsComplex = 0x8;
    private const uint Caps2AllCubeFaces = 0xFE00;

    // DDS_HEADER_DXT10: format, dimension (3 = 2D), misc flag (4 = cube), array size, misc flags 2.
    private const uint Texture2D = 3;
    private const uint MiscTextureCube = 0x4;
    private const int CubeFaces = 6;
    private const int DimensionField = 4;
    private const int MiscField = 8;
    private const int ArraySizeField = 12;

    public static ReadOnlySpan<byte> Magic => "DDS "u8;

    /// <summary>A DDS file of one mip; for a cube map <paramref name="data"/> holds the six faces one after another.</summary>
    public static byte[] Build(int width, int height, int dxgiFormat, ReadOnlySpan<byte> data, int faces)
    {
        var file = new byte[MagicSize + HeaderSize + Dx10HeaderSize + data.Length];
        var span = file.AsSpan();
        var cube = faces == CubeFaces;

        var caps = cube ? CapsTexture | CapsComplex : CapsTexture;
        var caps2 = cube ? Caps2AllCubeFaces : 0;
        var misc = cube ? MiscTextureCube : 0;
        Magic.CopyTo(span);

        WriteU32(span, SizeField, HeaderSize);
        WriteU32(span, FlagsField, HeaderFlags);
        WriteU32(span, HeightField, (uint)height);
        WriteU32(span, WidthField, (uint)width);
        WriteU32(span, PitchField, (uint)(data.Length / faces));
        WriteU32(span, MipCountField, 1);

        WriteU32(span, PixelFormatSizeField, PixelFormatSize);
        WriteU32(span, PixelFormatFlagsField, FourCcFlag);
        "DX10"u8.CopyTo(span[FourCcField..]);
        WriteU32(span, CapsField, caps);
        WriteU32(span, Caps2Field, caps2);

        var dx10 = MagicSize + HeaderSize;
        WriteU32(span, dx10, (uint)dxgiFormat);
        WriteU32(span, dx10 + DimensionField, Texture2D);
        WriteU32(span, dx10 + MiscField, misc);
        WriteU32(span, dx10 + ArraySizeField, 1);
        data.CopyTo(span[(dx10 + Dx10HeaderSize)..]);

        return file;
    }

    private static void WriteU32(Span<byte> span, int offset, uint value)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(span[offset..], value);
    }
}
