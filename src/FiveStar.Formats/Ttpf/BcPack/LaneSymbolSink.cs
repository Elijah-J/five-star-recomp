// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

namespace FiveStar.Formats.Ttpf.BcPack;

/// <summary>Stores the raw lane symbols, lane major: <c>symbols[lane * blocks + block]</c>. For tests.</summary>
internal readonly unsafe struct LaneSymbolSink : ISymbolVectorSink
{
    private readonly ushort* _symbols;
    private readonly int _blocks;

    public LaneSymbolSink(ushort* symbols, int blocks)
    {
        _symbols = symbols;
        _blocks = blocks;
    }

    [HotPath]
    public void Emit(int block, uint* symbols)
    {
        for (var lane = 0; lane < LaneRegisters.Lanes; lane++)
        {
            _symbols[(lane * _blocks) + block] = (ushort)symbols[lane];
        }
    }
}
