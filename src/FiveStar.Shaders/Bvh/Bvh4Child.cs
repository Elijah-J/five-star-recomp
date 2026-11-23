// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

namespace FiveStar.Shaders.Bvh;

/// <summary>A child of a box while building: a box (by index) or a triangle (by position in the leaf order).</summary>
/// <param name="IsTriangle">Whether the child is a triangle.</param>
/// <param name="Index">Box index, or the triangle's position in <see cref="Bvh4Tree.Order"/>.</param>
/// <param name="Bounds">The child's bounds.</param>
internal readonly record struct Bvh4Child(bool IsTriangle, int Index, BvhBounds Bounds);
