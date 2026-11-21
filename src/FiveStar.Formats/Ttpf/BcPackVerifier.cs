// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

namespace FiveStar.Formats.Ttpf;

/// <summary>
/// Checks the near-constant lanes of BCPack pages. Their rANS coder renormalises byte-wise with 16-bit probabilities
/// (M = 2^16) and lower bound L = 2^24. Model bytes 0xDB, 0xDC, 0xDD and 0xDE give one symbol the frequency
/// 65536 - E (E = 2, 4, 8, 32) at cumulative start 1; a lane coding only that symbol N times has a start state that
/// depends only on N. N is the block count of the page (8-byte blocks) or twice it (16-byte blocks; BC6H/BC7 lanes
/// may also code one symbol per block). After the header, the first refill byte of each lane with a budget above 3
/// follows in lane order.
/// </summary>
public sealed class BcPackVerifier
{
    private const ulong LowerBound = 1UL << 24;
    private const ulong ProbabilityScale = 1UL << 16;
    private const int ProbabilityBits = 16;
    private const int BitsPerByte = 8;
    private const ulong ByteMask = 0xFF;
    private const int BudgetBias = 3;
    private const int ModeDb = 0xDB;
    private const int ModeDc = 0xDC;
    private const int ModeDd = 0xDD;
    private const int ModeDe = 0xDE;
    private const int EscapeDb = 2;
    private const int EscapeDc = 4;
    private const int EscapeDd = 8;
    private const int EscapeDe = 32;
    private const int EightByteBlocks = 8;
    private const int SixteenByteBlocks = 16;
    private const int TwoSymbols = 2;

    // Codecs by BC format: 1 BC1, 3 BC3, 4 BC4, 5 BC5, 6 BC6H, 7 BC7.
    private const int CodecBc1 = 1;
    private const int CodecBc3 = 3;
    private const int CodecBc4 = 4;
    private const int CodecBc5 = 5;
    private const int CodecBc6 = 6;
    private const int CodecBc7 = 7;

    private readonly Dictionary<(int Mode, long Count), (ulong State, byte[] Refills)> _encoded = [];

    /// <summary>True for a model byte of a near-constant model.</summary>
    public static bool IsNearConstant(int mode)
    {
        return Escape(mode) != 0;
    }

    /// <summary>Bytes per BC block of a codec, or 0 for an unknown codec.</summary>
    public static int BlockBytes(int codec)
    {
        switch (codec)
        {
            case CodecBc1:
            case CodecBc4:
                return EightByteBlocks;
            case CodecBc3:
            case CodecBc5:
            case CodecBc6:
            case CodecBc7:
                return SixteenByteBlocks;
            default:
                return 0;
        }
    }

    /// <summary>The near-constant lanes of a page and how each checks out.</summary>
    public IReadOnlyDictionary<int, BcPackLaneCheck> CheckPage(ReadOnlySpan<byte> data, int codec, long virtualSize)
    {
        var lanes = BcPackHeader.Parse(data);
        var blocks = (long)Math.Floor((double)virtualSize / BlockBytes(codec));
        var lookAhead = new Dictionary<int, int>();
        var position = BcPackHeader.Size;

        foreach (var lane in lanes)
        {
            if (lane.Budget > BudgetBias)
            {
                lookAhead[lane.Lane] = position;
                position++;
            }
        }

        var results = new SortedDictionary<int, BcPackLaneCheck>();

        foreach (var lane in lanes)
        {
            if (IsNearConstant(lane.Mode))
            {
                results[lane.Lane] = CheckLane(data, lane, blocks, codec, lookAhead);
            }
        }

        return results;
    }

    /// <summary>Encodes <paramref name="count"/> dominant symbols: the decoder start state and refill bytes in read order.</summary>
    public (ulong State, byte[] Refills) EncodeConstant(int mode, long count)
    {
        if (_encoded.TryGetValue((mode, count), out var cached))
        {
            return cached;
        }

        var frequency = ProbabilityScale - (ulong)Escape(mode);
        var state = LowerBound;
        var output = new List<byte>();
        var limit = (LowerBound >> ProbabilityBits) * (1UL << BitsPerByte) * frequency;

        for (long symbol = 0; symbol < count; symbol++)
        {
            while (state >= limit)
            {
                output.Add((byte)(state & ByteMask));
                state >>= BitsPerByte;
            }

            state = ((state / frequency) * ProbabilityScale) + (state % frequency) + 1;
        }

        output.Reverse();
        var result = (state, output.ToArray());
        _encoded[(mode, count)] = result;

        return result;
    }

    /// <summary>True when decoding <paramref name="count"/> symbols yields only the dominant one and ends at L.</summary>
    public static bool DecodeConstant(int mode, ulong state, long count, ReadOnlySpan<byte> refills)
    {
        var frequency = ProbabilityScale - (ulong)Escape(mode);
        var next = 0;

        for (long symbol = 0; symbol < count; symbol++)
        {
            var slot = state % ProbabilityScale;

            if (slot < 1 || slot > frequency)
            {
                return false;
            }

            state = (frequency * (state / ProbabilityScale)) + slot - 1;

            while (state < LowerBound)
            {
                if (next >= refills.Length)
                {
                    return false;
                }

                state = (state << BitsPerByte) | refills[next];
                next++;
            }
        }

        return state == LowerBound && next == refills.Length;
    }

    private BcPackLaneCheck CheckLane(ReadOnlySpan<byte> data, BcPackLane lane, long blocks, int codec, Dictionary<int, int> lookAhead)
    {
        foreach (var symbolsPerBlock in SymbolsPerBlock(codec))
        {
            var count = blocks * symbolsPerBlock;
            var (state, refills) = EncodeConstant(lane.Mode, count);

            if (state != lane.State)
            {
                continue;
            }

            if (lane.Budget != refills.Length + BudgetBias)
            {
                return new BcPackLaneCheck("budget_mismatch", count);
            }

            if (refills.Length > 0 && lookAhead.TryGetValue(lane.Lane, out var at) && data[at] != refills[0])
            {
                return new BcPackLaneCheck("lookahead_mismatch", count);
            }

            return new BcPackLaneCheck("verified", count);
        }

        return new BcPackLaneCheck("not_constant", null);
    }

    private static int[] SymbolsPerBlock(int codec)
    {
        switch (codec)
        {
            case CodecBc3:
            case CodecBc5:
                return [TwoSymbols];
            case CodecBc6:
            case CodecBc7:
                return [TwoSymbols, 1];
            default:
                return [1];
        }
    }

    private static int Escape(int mode)
    {
        switch (mode)
        {
            case ModeDb:
                return EscapeDb;
            case ModeDc:
                return EscapeDc;
            case ModeDd:
                return EscapeDd;
            case ModeDe:
                return EscapeDe;
            default:
                return 0;
        }
    }
}
