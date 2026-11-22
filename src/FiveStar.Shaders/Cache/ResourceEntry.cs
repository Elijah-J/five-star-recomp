// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

namespace FiveStar.Shaders.Cache;

/// <summary>A tag 0x42 entry of a shader's metadata.</summary>
/// <param name="Type">The u32 at +8: low byte 1 or 2, byte 1 descriptor class (0 SRV, 1 UAV, 2 CBV, 3 sampler), high 16 bits the PSV0 resource kind.</param>
/// <param name="Register">The u32 at +12: the shader register.</param>
/// <param name="Space">The u32 at +16: the register space.</param>
public readonly record struct ResourceEntry(uint Type, uint Register, uint Space);
