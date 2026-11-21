// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

namespace FiveStar.Formats.Ttpf;

/// <summary>
/// Locates and reads the mip 0 of each texture in a page file. A texture's span runs from its descriptor address to
/// the next descriptor's address (or the end of the last page). Mips are stored smallest first, so mip 0 is the last
/// tile-padded block of the span; when mip 0 fits inside one tile it shares that tile with the mip tail and starts
/// half a tile before the span's end. Cube maps hold six faces back to back, each with mip 0 at the end of its sixth.
/// </summary>
public sealed class TextureExtractor
{
    private const int BaseMask = 7;
    private const int CubeFlag = 8;
    private const int CubeFaces = 6;
    private const int LargeTileBase = 0;
    private const int LastSmallTileBase = 3;
    private const int TailDivisor = 2;

    private readonly TexturePageFile _file;
    private readonly Dictionary<long, long> _spanEnd = [];
    private readonly Dictionary<string, SwizzlePattern> _patterns = new(StringComparer.Ordinal);

    public TextureExtractor(TexturePageFile file)
    {
        _file = file;
        var starts = new SortedSet<long>();

        foreach (var descriptor in file.Descriptors)
        {
            starts.Add(descriptor.VirtualAddress);
        }

        long? previous = null;

        foreach (var start in starts)
        {
            if (previous.HasValue)
            {
                _spanEnd[previous.Value] = start;
            }

            previous = start;
        }

        if (file.Pages.Count > 0)
        {
            var last = file.Pages[^1];
            VirtualEnd = last.VirtualAddress + last.EffectiveVirtualSize;
        }
    }

    /// <summary>Gets the end of the last page: the span end of the last texture.</summary>
    public long VirtualEnd { get; }

    /// <summary>The end of a texture's span.</summary>
    public long SpanEnd(TextureDescriptor descriptor)
    {
        return _spanEnd.GetValueOrDefault(descriptor.VirtualAddress, VirtualEnd);
    }

    /// <summary>Where mip 0 is stored, or why that is not known.</summary>
    public (TextureStatus Status, TextureLayout? Layout) Locate(TextureDescriptor descriptor)
    {
        var format = TextureFormats.Find(descriptor.Format);

        if (format is null)
        {
            return (new TextureStatus(TextureStatusKind.UnsupportedFormat, descriptor.Format), null);
        }

        var mode = descriptor.LayoutMode;
        var tileBytes = TileBytes(mode & BaseMask);
        string? bits = null;

        if (tileBytes.HasValue)
        {
            bits = SwizzlePatterns.Find(tileBytes.Value, format.BytesPerElement);
        }

        if (bits is null)
        {
            return (new TextureStatus(TextureStatusKind.UnsupportedMode, mode), null);
        }

        var width = (descriptor.Width + format.BlockDimension - 1) / format.BlockDimension;
        var height = (descriptor.Height + format.BlockDimension - 1) / format.BlockDimension;
        var fitted = Pattern(Pattern(bits).FitBits(width, height));
        var inTail = fitted.Bits.Length < bits.Length;
        var size = fitted.PaddedSize(width, height, format.BytesPerElement);
        var shape = new TextureLayout(format, width, height, fitted, size, 0, 1, 0);

        if ((mode & CubeFlag) != 0)
        {
            return LocateCube(descriptor, shape, inTail, tileBytes!.Value);
        }

        var start = inTail ? SpanEnd(descriptor) - (tileBytes!.Value / TailDivisor) : SpanEnd(descriptor) - size;

        if (start < descriptor.VirtualAddress)
        {
            return (new TextureStatus(TextureStatusKind.Size, 0), null);
        }

        return (TextureStatus.Ok, shape with { Start = start });
    }

    /// <summary>Mip 0 as row-major elements (cube faces one after another), or the reason it cannot be read.</summary>
    public (TextureStatus Status, byte[]? Data, TextureLayout? Layout) ReadMip0(TextureDescriptor descriptor)
    {
        var (status, layout) = Locate(descriptor);

        if (layout is null)
        {
            return (status, null, null);
        }

        var faceBytes = (long)layout.BlockWidth * layout.BlockHeight * layout.Format.BytesPerElement;
        var data = new byte[faceBytes * layout.Faces];

        for (var face = 0; face < layout.Faces; face++)
        {
            var (tiled, missing) = _file.ReadVirtual(layout.Start + (face * layout.FaceSpan), (int)layout.Size);

            if (missing != 0)
            {
                return (new TextureStatus(TextureStatusKind.UnsupportedCodec, 0), null, layout);
            }

            var linear = layout.Pattern.Unswizzle(tiled, layout.BlockWidth, layout.BlockHeight, layout.Format.BytesPerElement);
            linear.CopyTo(data, faceBytes * face);
        }

        return (TextureStatus.Ok, data, layout);
    }

    private (TextureStatus Status, TextureLayout? Layout) LocateCube(TextureDescriptor descriptor, TextureLayout shape, bool inTail, int tileBytes)
    {
        var span = SpanEnd(descriptor) - descriptor.VirtualAddress;

        if (span % CubeFaces != 0)
        {
            return (new TextureStatus(TextureStatusKind.CubeSpan, 0), null);
        }

        var faceSpan = span / CubeFaces;

        if (faceSpan < shape.Size)
        {
            return (new TextureStatus(TextureStatusKind.Size, 0), null);
        }

        // Only single-mip cubes are understood; mipped cubes interleave faces and mips with zero-filled holes.
        if (descriptor.MipCount > 1)
        {
            return (new TextureStatus(TextureStatusKind.CubeMips, 0), null);
        }

        var back = inTail ? tileBytes / TailDivisor : shape.Size;

        return (TextureStatus.Ok, shape with { Start = descriptor.VirtualAddress + faceSpan - back, Faces = CubeFaces, FaceSpan = faceSpan });
    }

    private static int? TileBytes(int layoutBase)
    {
        if (layoutBase == LargeTileBase)
        {
            return SwizzlePatterns.LargeTile;
        }

        if (layoutBase <= LastSmallTileBase)
        {
            return SwizzlePatterns.SmallTile;
        }

        return null;
    }

    private SwizzlePattern Pattern(string bits)
    {
        if (!_patterns.TryGetValue(bits, out var pattern))
        {
            pattern = new SwizzlePattern(bits);
            _patterns[bits] = pattern;
        }

        return pattern;
    }
}
