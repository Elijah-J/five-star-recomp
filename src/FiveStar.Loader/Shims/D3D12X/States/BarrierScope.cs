// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

namespace FiveStar.Loader.Shims.D3D12X.States;

/// <summary>One side of an enhanced barrier: D3D12_BARRIER_SYNC, D3D12_BARRIER_ACCESS and D3D12_BARRIER_LAYOUT (d3d12.h).</summary>
internal readonly record struct BarrierScope(int Sync, int Access, int Layout);
