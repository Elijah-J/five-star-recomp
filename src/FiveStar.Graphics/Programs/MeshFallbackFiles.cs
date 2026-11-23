// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

namespace FiveStar.Graphics.Programs;

/// <summary>The DXIL files of a mesh program's compute + vertex-pulling fallback, relative to the manifest's folder.</summary>
public sealed record MeshFallbackFiles(string Compute, string Vertex);
