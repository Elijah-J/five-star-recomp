// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

namespace FiveStar.Shaders.Tests.Warp;

/// <summary>A compute dispatch: results come back only through writable buffers.</summary>
public sealed record ComputeJob(byte[] Shader, int GroupsX, int GroupsY, int GroupsZ, Bindings Bindings);
