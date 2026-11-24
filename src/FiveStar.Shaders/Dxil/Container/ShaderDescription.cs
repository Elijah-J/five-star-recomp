// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

namespace FiveStar.Shaders.Dxil.Container;

/// <summary>
/// What the container and the entry metadata say about a compute shader: thread-group size, required wave size, shader
/// flags and the size of the user-data constant buffer at b0 (0: none).
/// </summary>
public sealed record ShaderDescription((int X, int Y, int Z) Threads, int? WaveSize, ShaderFlagBits Flags, int UserDataBytes);
