// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

namespace FiveStar.Formats.Ttpf;

/// <summary>One of the eight lanes of a BCPack page header.</summary>
/// <param name="Lane">Lane number 0 to 7.</param>
/// <param name="Mode">Model byte (row 0).</param>
/// <param name="Budget">Refill bytes the lane reads, plus 3 (row 1).</param>
/// <param name="State">rANS decoder start state (rows 2 to 5, big-endian).</param>
public sealed record BcPackLane(int Lane, int Mode, int Budget, uint State);
