// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Numerics;

namespace FiveStar.Formats.Models;

/// <summary>A bone of a drawable's skeleton: its name, parent index (-1 for the root) and transform relative to the parent.</summary>
public sealed record Bone(string Name, int Parent, Quaternion Rotation, Vector3 Translation, Vector3 Scale);
