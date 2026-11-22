// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

namespace FiveStar.Shaders.Compilation;

/// <summary>How far a corpus run is: programs finished out of the programs it runs.</summary>
public readonly record struct CorpusProgress(int Done, int Total);
