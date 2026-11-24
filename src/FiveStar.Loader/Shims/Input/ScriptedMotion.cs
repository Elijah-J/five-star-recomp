// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

namespace FiveStar.Loader.Shims.Input;

/// <summary>
/// One cursor motion of an input script: from <paramref name="Start"/> for <paramref name="Hold"/> seconds the cursor
/// moves <paramref name="PerSecondX"/>, <paramref name="PerSecondY"/> pixels a second (the right stick of the
/// keyboard-and-mouse gamepad).
/// </summary>
internal readonly record struct ScriptedMotion(double Start, double Hold, double PerSecondX, double PerSecondY)
{
    /// <summary>The seconds of this motion that passed by <paramref name="seconds"/>.</summary>
    public double Elapsed(double seconds)
    {
        return Math.Clamp(seconds - Start, 0, Hold);
    }
}
