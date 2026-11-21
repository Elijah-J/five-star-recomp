// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using Vortice.DXGI;

namespace FiveStar.Shaders.Tests.Warp;

/// <summary>A 2D texture (one mip) at a <c>ResourceDescriptorHeap</c> slot, texels given as raw 32-bit words.</summary>
public sealed record TextureResource(int HeapIndex, Format Format, int Width, int Height, uint[] Words);
