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

    [Fact]
    public void Capture_lines_are_times_not_presses()
    {
        var script = InputScript.Parse(["90 capture", "15 Space", "30 CAPTURE"]);

        Assert.Equal([30.0, 90.0], script.Captures);
        Assert.Single(script.Presses);
        Assert.Equal(1, script.StartedBefore(20));
    }

    [Fact]
    public void Plus_times_follow_the_previous_line()
    {
        var script = InputScript.Parse(["100 Down", "+4 Down", "+0.5 Space"]);

        Assert.Equal([100.0, 104.0, 104.5], script.Presses.Select(p => p.Start));
    }

    [Fact]
    public void Cursor_lines_move_the_cursor_at_their_speed_while_held()
    {
        var script = InputScript.Parse(["10 cursor 0 500 hold 2 repeat 2 every 10", "11 Space"]);

        Assert.Single(script.Presses);
        Assert.Equal(new CursorPosition(0, 0), script.CursorOffset(9));
        Assert.Equal(new CursorPosition(0, 250), script.CursorOffset(10.5));
        Assert.Equal(new CursorPosition(0, 1000), script.CursorOffset(15));
        Assert.Equal(new CursorPosition(0, 2000), script.CursorOffset(30));
    }
}
