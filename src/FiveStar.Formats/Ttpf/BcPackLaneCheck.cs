// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

namespace FiveStar.Formats.Ttpf;

/// <summary>The result of checking a near-constant BCPack lane.</summary>
/// <param name="Status"><c>verified</c>, <c>not_constant</c>, <c>budget_mismatch</c> or <c>lookahead_mismatch</c>.</param>
/// <param name="Symbols">The symbol count whose encoding matched the lane's state; null for <c>not_constant</c>.</param>
public sealed record BcPackLaneCheck(string Status, long? Symbols);
