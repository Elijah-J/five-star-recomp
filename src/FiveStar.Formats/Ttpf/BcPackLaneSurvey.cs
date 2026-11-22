// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

namespace FiveStar.Formats.Ttpf;

/// <summary>
/// Statistics over BCPack page headers: how often each lane uses each model, and the twin-lane check. Right after the
/// 48-byte header every lane whose budget byte is not 3 reads one byte, in lane order (budgets 0-2 count: the budget
/// wraps at 256). Two lanes with identical headers code identical data, so their first bytes must be equal; on the
/// game data they are in 96% of 6,878 twin pairs, against 1 in 256 by chance.
/// </summary>
public sealed class BcPackLaneSurvey
{
    /// <summary>The budget byte of a lane that reads no bytes after its header.</summary>
    public const int EmptyBudget = 3;

    /// <summary>Bytes of a page head this survey reads: the header and one byte per lane.</summary>
    public const int HeadBytes = BcPackHeader.Size + BcPackHeader.Lanes;

    private const int NoByte = -1;

    private readonly Dictionary<(int Codec, int Lane, BcPackModel Model), long> _models = [];

    public long Pages { get; private set; }

    /// <summary>Gets the lane pairs with identical headers that read a first byte.</summary>
    public long TwinPairs { get; private set; }

    /// <summary>Gets the twin pairs whose first bytes are equal.</summary>
    public long TwinMatches { get; private set; }

    public IReadOnlyDictionary<(int Codec, int Lane, BcPackModel Model), long> Models => _models;

    /// <summary>The offset of each lane's first byte after the header, or -1 for a lane that reads none.</summary>
    public static int[] FirstByteOffsets(IReadOnlyList<BcPackLane> lanes)
    {
        var offsets = new int[lanes.Count];
        var next = BcPackHeader.Size;

        for (var index = 0; index < lanes.Count; index++)
        {
            offsets[index] = NoByte;

            if (lanes[index].Budget != EmptyBudget)
            {
                offsets[index] = next;
                next++;
            }
        }

        return offsets;
    }

    /// <summary>Adds one page: <paramref name="head"/> holds at least <see cref="HeadBytes"/> bytes of it.</summary>
    public void Add(ReadOnlySpan<byte> head, int codec)
    {
        var lanes = BcPackHeader.Parse(head);
        Pages++;

        foreach (var lane in lanes)
        {
            var key = (codec, lane.Lane, BcPackModel.FromByte(lane.Mode));
            _models[key] = _models.GetValueOrDefault(key) + 1;
        }

        var offsets = FirstByteOffsets(lanes);

        for (var first = 0; first < lanes.Count; first++)
        {
            for (var second = first + 1; second < lanes.Count; second++)
            {
                if (offsets[first] == NoByte || lanes[first] with { Lane = 0 } != lanes[second] with { Lane = 0 })
                {
                    continue;
                }

                if (offsets[second] >= head.Length)
                {
                    continue;
                }

                TwinPairs++;

                if (head[offsets[first]] == head[offsets[second]])
                {
                    TwinMatches++;
                }
            }
        }
    }
}
