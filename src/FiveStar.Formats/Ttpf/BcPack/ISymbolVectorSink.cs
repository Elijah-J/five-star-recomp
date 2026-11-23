// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

namespace FiveStar.Formats.Ttpf.BcPack;

/// <summary>Receives each released symbol vector: one symbol per lane, which is one block.</summary>
internal unsafe interface ISymbolVectorSink
{
    void Emit(int block, uint* symbols);
}
