// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

namespace FiveStar.Formats.Ttpf.BcPack;

/// <summary>
/// Decodes a BCPack page of any codec that has a block decoder to its BC blocks, which replace the page's virtual
/// bytes. BC1 uses the tables of bcpack-rs with our corrections; BC4 and BC5 use tables fitted here, and their output
/// is approximate (<see cref="IsApproximate"/>).
/// </summary>
public static class BcPackPageDecoder
{
    /// <summary>Whether pages of <paramref name="codec"/> decode to blocks (BC4 and BC5 need their embedded tables).</summary>
    public static bool CanDecode(int codec)
    {
        switch ((BcPackCodec)codec)
        {
            case BcPackCodec.Bc1:
                return true;
            case BcPackCodec.Bc4:
                return RgtcTables.Bc4 is not null;
            case BcPackCodec.Bc5:
                return RgtcTables.Bc5 is not null;
            default:
                return false;
        }
    }

    /// <summary>
    /// Whether the blocks of <paramref name="codec"/> rest on tables fitted only against neighbouring mips, with no
    /// structural check of their values: they look right but are not known to be the stored blocks.
    /// </summary>
    public static bool IsApproximate(int codec) => CanDecode(codec) && codec != (int)BcPackCodec.Bc1;

    /// <summary>Decodes one page's stored bytes.</summary>
    /// <param name="codec">The page's codec (<see cref="CanDecode"/>).</param>
    /// <param name="chunk">The stored bytes; trailing zero padding is allowed.</param>
    /// <param name="virtualSize">The page's virtual size: the bytes of blocks it decodes to.</param>
    /// <param name="output">At least <paramref name="virtualSize"/> bytes; valid only when the result is ok.</param>
    /// <param name="engine">The lane engine.</param>
    public static BcPackDecodeResult Decode(int codec, ReadOnlySpan<byte> chunk, long virtualSize, Span<byte> output, BcPackEngine engine)
    {
        if (!CanDecode(codec))
        {
            return new BcPackDecodeResult(BcPackDecodeStatus.InvalidArgument, 0, chunk.Length, 0, 0, 0);
        }

        var count = BcPackDecoder.SymbolsPerLane((BcPackCodec)codec, virtualSize);

        switch ((BcPackCodec)codec)
        {
            case BcPackCodec.Bc1:
                return Bc1Decoder.Decode(chunk, count, output, engine);
            case BcPackCodec.Bc4:
                return RgtcDecoder.Decode(BcPackCodec.Bc4, chunk, count, output, RgtcTables.Bc4!, engine);
            default:
                return RgtcDecoder.Decode(BcPackCodec.Bc5, chunk, count, output, RgtcTables.Bc5!, engine);
        }
    }

    /// <summary>Decodes with the fastest engine this processor runs.</summary>
    public static BcPackDecodeResult Decode(int codec, ReadOnlySpan<byte> chunk, long virtualSize, Span<byte> output) =>
        Decode(codec, chunk, virtualSize, output, BcPackDecoder.DefaultEngine);
}
