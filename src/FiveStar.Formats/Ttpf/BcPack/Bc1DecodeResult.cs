// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;

namespace FiveStar.Formats.Ttpf.BcPack;

/// <summary>The result of a BCPack BC1 decode.</summary>
/// <param name="Status">Convergence status; the output is valid only for <see cref="Bc1DecodeStatus.Ok"/>.</param>
/// <param name="Consumed">Stream bytes the lanes read.</param>
/// <param name="ChunkLength">Length of the input chunk.</param>
/// <param name="Cycles">Decoder clocks simulated.</param>
/// <param name="UnfittedBlocks">Blocks whose reference chain reaches an unfitted preset window slot.</param>
/// <param name="Detail">The lane (<see cref="Bc1DecodeStatus.BadHeader"/>) or lane bit mask
/// (<see cref="Bc1DecodeStatus.IllegalEndState"/>).</param>
public readonly record struct Bc1DecodeResult(
    Bc1DecodeStatus Status,
    int Consumed,
    int ChunkLength,
    long Cycles,
    int UnfittedBlocks,
    int Detail)
{
    public bool Success => Status == Bc1DecodeStatus.Ok;

    /// <summary>The status in words, for reports.</summary>
    public string Describe()
    {
        switch (Status)
        {
            case Bc1DecodeStatus.Ok:
                return $"ok ({Consumed} of {ChunkLength} bytes consumed)";
            case Bc1DecodeStatus.InvalidArgument:
                return "invalid block count or output size";
            case Bc1DecodeStatus.BadHeader:
                return $"lane {Detail}: header selects an alphabet index above 8";
            case Bc1DecodeStatus.NoProgress:
                return $"no progress (stream truncated or not BCPack BC1) at byte {Consumed}";
            case Bc1DecodeStatus.CycleLimit:
                return "cycle limit reached";
            case Bc1DecodeStatus.IllegalEndState:
                return $"lanes [{LaneList(Detail)}] ended in an illegal state";
            case Bc1DecodeStatus.TrailingData:
                return $"non-zero bytes after the end of the stream ({Consumed} of {ChunkLength} consumed)";
            default:
                return Status.ToString();
        }
    }

    private static string LaneList(int mask)
    {
        var lanes = new List<string>();

        for (var lane = 0; lane < Bc1Decoder.Lanes; lane++)
        {
            if ((mask & (1 << lane)) != 0)
            {
                lanes.Add(lane.ToString(CultureInfo.InvariantCulture));
            }
        }

        return string.Join(", ", lanes);
    }
}
