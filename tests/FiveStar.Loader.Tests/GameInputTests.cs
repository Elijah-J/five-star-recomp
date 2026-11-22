// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using FiveStar.Loader.Diagnostics;
using FiveStar.Loader.Shims.Input;
using FiveStar.Loader.Tests.Support;
using Xunit;

namespace FiveStar.Loader.Tests;

/// <summary>The GameInput replacement: the keyboard-and-mouse gamepad mapping and the native interfaces.</summary>
public sealed unsafe class GameInputTests
{
    private const float Tolerance = 1e-5f;

    [Fact]
    public void Wasd_drive_the_left_stick_on_the_unit_circle()
    {
        var config = InputConfigFile.Defaults();
        var keys = new PressedKeys();
        keys.Set('W', true);
        keys.Set('D', true);
        keys.Set(' ', true);

        var diagonal = VirtualGamepad.Map(config, keys, 0, 0, 0.016f);
        keys.Set('D', false);
        var forward = VirtualGamepad.Map(config, keys, 0, 0, 0.016f);

        Assert.Equal(MathF.Sqrt(0.5f), diagonal.LeftX, Tolerance);
        Assert.Equal(MathF.Sqrt(0.5f), diagonal.LeftY, Tolerance);
        Assert.Equal(0x4u, diagonal.Buttons);
        Assert.Equal(0f, forward.LeftX);
        Assert.Equal(1f, forward.LeftY);
    }

    [Fact]
    public void Mouse_speed_moves_the_right_stick_and_saturates()
    {
        var config = InputConfigFile.Parse(["mouse-sensitivity 2", "mouse-curve 1"]);

        // 2 x 250 px/s / 1000 px/s = half; 2 x 5000 px/s clamps to the edge; screen down is stick down.
        var half = VirtualGamepad.Mouse(config, 25, 0, 0.1f);
        var fast = VirtualGamepad.Mouse(config, 300, 400, 0.1f);
        var curved = VirtualGamepad.Mouse(config with { MouseCurve = 2 }, 25, 0, 0.1f);

        Assert.Equal(0.5f, half.X, Tolerance);
        Assert.Equal(0.6f, fast.X, Tolerance);
        Assert.Equal(-0.8f, fast.Y, Tolerance);
        Assert.Equal(0.25f, curved.X, Tolerance);
    }

    [Fact]
    public void GetCurrentReading_through_the_vtable_returns_the_source_state()
    {
        using var test = TestRuntime.Create();
        var source = new FakeInputSource { Gamepad = new GamepadState { Buttons = 0x4, LeftTrigger = 0.25f, LeftX = -0.5f, RightY = 1f } };
        var config = InputConfigFile.Defaults() with { VirtualGamepad = false };
        var log = new LoaderLog(TextWriter.Null, null, false);
        using var service = new GameInputService(source, source, config, TimeProvider.System, test.Runtime.Stubs, test.Runtime.CodeHeap, log);

        nint input = service.Create();
        nint* inputTable = *(nint**)input;
        nint reading = 0;
        int hr = ((delegate* unmanaged<nint, uint, nint, nint*, int>)inputTable[4])(input, 0x40000, 0, &reading);
        nint* readingTable = *(nint**)reading;
        GamepadState state;

        byte hasGamepad = ((delegate* unmanaged<nint, GamepadState*, byte>)readingTable[22])(reading, &state);
        MouseState mouse;
        byte hasMouse = ((delegate* unmanaged<nint, MouseState*, byte>)readingTable[16])(reading, &mouse);

        nint device = 0;
        ((delegate* unmanaged<nint, nint*, void>)readingTable[6])(reading, &device);
        nint info = ((delegate* unmanaged<nint, nint>)(*(nint**)device)[3])(device);
        uint released = ((delegate* unmanaged<nint, uint>)readingTable[2])(reading);
        int unimplemented = ((delegate* unmanaged<nint, nint*, int>)inputTable[14])(input, &reading);

        Assert.Equal(0, hr);
        Assert.Equal(1, hasGamepad);
        Assert.Equal(source.Gamepad, state);
        Assert.Equal(0, hasMouse);

        Assert.Equal(0x40000u, *(uint*)(info + 104));
        Assert.Equal(1u, released);
        Assert.Equal(unchecked((int)0x80004001), unimplemented);
    }
}
