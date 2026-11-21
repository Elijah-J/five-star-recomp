// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later
// Per pixel: wave lane index, wave size and quad slot (read back through QuadReadLaneAt). Lane-dependent expectations
// (v_mbcnt, EXEC from an integer, readfirstlane) are computed from it.
#include "rdna_prelude.hlsli"
uint4 main(float4 pos : SV_Position) : SV_Target0 {
    uint l = WaveGetLaneIndex();
    uint q = 4;
    [unroll] for (uint i = 0; i < 4; i++) if (QuadReadLaneAt(l, i) == l) q = i;
    return uint4(l, WaveGetLaneCount(), q, user_sgpr(0) + user_sgpr(59));
}
