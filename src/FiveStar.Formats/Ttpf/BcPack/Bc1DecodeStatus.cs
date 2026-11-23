// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

namespace FiveStar.Formats.Ttpf.BcPack;

/// <summary>Outcome of a BCPack BC1 decode.</summary>
public enum Bc1DecodeStatus
{
    /// <summary>All 12 lanes converged: exact symbol counts, legal end states, only zero padding left.</summary>
    Ok,

    /// <summary>Bad arguments (block count, output size).</summary>
    InvalidArgument,

    /// <summary>A fragment header selects an alphabet index above 8, which the hardware does not define.</summary>
    BadHeader,

    /// <summary>No lane made progress for 64 clocks: the stream ran out or is not BCPack BC1.</summary>
    NoProgress,

    /// <summary>The clock budget ran out.</summary>
    CycleLimit,

    /// <summary>Every lane finished, but some ended outside the legal states L, L / 2^8 and L / 2^16.</summary>
    IllegalEndState,

    /// <summary>The stream ended before the chunk did and the rest is not zero padding.</summary>
    TrailingData,
}
