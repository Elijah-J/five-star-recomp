// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using FiveStar.Loader.Shims.Input;
using Xunit;

namespace FiveStar.Loader.Tests;

/// <summary>The --input-script file: timed presses, repeated, held for their hold time only.</summary>
public sealed class InputScriptTests
{
    [Fact]
    public void Presses_repeat_and_are_held_for_their_hold_time()
    {
        var script = InputScript.Parse(["# title screen", "20 Space hold 0.5 repeat 3 every 10", "1 Enter"]);
        var keys = new PressedKeys();

        script.Apply(keys, 30.2);

        Assert.Equal([1.0, 20.0, 30.0, 40.0], script.Presses.Select(p => p.Start));
        Assert.True(keys.IsDown(KeyNames.Parse("Space")));
        Assert.False(keys.IsDown(KeyNames.Parse("Enter")));
    }
}
