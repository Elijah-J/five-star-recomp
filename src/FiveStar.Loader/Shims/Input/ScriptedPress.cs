// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

namespace FiveStar.Loader.Shims.Input;

/// <summary>One key press of an input script: held from <paramref name="Start"/> for <paramref name="Hold"/> seconds.</summary>
internal readonly record struct ScriptedPress(double Start, double Hold, int Key, string Name)
{
    public bool IsDown(double seconds)
    {
        return seconds >= Start && seconds < Start + Hold;
    }
}
