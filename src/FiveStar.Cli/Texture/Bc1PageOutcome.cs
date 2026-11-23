// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using FiveStar.Formats.Ttpf.BcPack;

namespace FiveStar.Cli.Texture;

/// <summary>How one BC1 page decoded in a scan.</summary>
/// <param name="Result">The decoder's result.</param>
/// <param name="Ticks">Stopwatch ticks of the decode alone.</param>
/// <param name="Hash">SHA-256 of the decoded blocks when asked for and the page converged.</param>
internal sealed record Bc1PageOutcome(Bc1DecodeResult Result, long Ticks, string? Hash);
