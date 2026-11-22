// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

namespace FiveStar.Graphics;

/// <summary>
/// A DXGI adapter and its D3D12 capabilities. <see cref="ShaderModel"/> is null when no D3D12 device could be
/// created on it; the wave lane counts are D3D12_FEATURE_DATA_D3D12_OPTIONS1.WaveLaneCountMin/Max.
/// </summary>
public sealed record AdapterInfo(
    int Index,
    string Name,
    bool Software,
    string? ShaderModel,
    int WaveLaneCountMin,
    int WaveLaneCountMax,
    string? MeshShaderTier);
