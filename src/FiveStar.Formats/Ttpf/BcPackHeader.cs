// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

namespace FiveStar.Formats.Ttpf;

/// <summary>
/// The 48-byte header of a BCPack page: 6 rows of 8 lanes, lane k of row r at byte <c>8 * r + k</c>. Row 0 is the
/// lane's model byte, row 1 its refill budget + 3, rows 2 to 5 its 32-bit rANS start state (row 2 most significant).
/// </summary>
public static class BcPackHeader
{
    /// <summary>Bytes of the header.</summary>
    public const int Size = 48;

    /// <summary>Lanes per page.</summary>
    public const int Lanes = 8;

    private const int BudgetRow = 1;
    private const int FirstStateRow = 2;
    private const int StateRows = 4;
    private const int BitsPerByte = 8;

    public static IReadOnlyList<BcPackLane> Parse(ReadOnlySpan<byte> data)
    {
        if (data.Length < Size)
        {
            throw new InvalidDataException("page shorter than the 48-byte header");
        }

        var lanes = new List<BcPackLane>(Lanes);

        for (var lane = 0; lane < Lanes; lane++)
        {
            uint state = 0;

            for (var row = FirstStateRow; row < FirstStateRow + StateRows; row++)
            {
                state = (state << BitsPerByte) | data[(Lanes * row) + lane];
            }

            lanes.Add(new BcPackLane(lane, data[lane], data[(Lanes * BudgetRow) + lane], state));
        }

        return lanes;
    }
}
