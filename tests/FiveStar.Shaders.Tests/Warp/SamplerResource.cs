// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

namespace FiveStar.Shaders.Tests.Warp;

/// <summary>A clamp-addressed sampler at a <c>SamplerDescriptorHeap</c> slot, point or linear filtered.</summary>
public sealed record SamplerResource(int HeapIndex, bool Linear);
