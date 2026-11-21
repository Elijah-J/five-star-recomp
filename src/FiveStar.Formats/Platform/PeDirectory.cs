// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

namespace FiveStar.Formats.Platform;

/// <summary>Indices of the PE data directories used here (PE/COFF specification, optional header data directories).</summary>
public enum PeDirectory
{
    Export = 0,
    Import = 1,
    Resource = 2,
    Debug = 6,
    DelayImport = 13,
}
