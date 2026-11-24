// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Diagnostics;
using FiveStar.Loader.Diagnostics;

namespace FiveStar.Loader.Shims.Input;

/// <summary>
/// A desktop source with the presses of an <see cref="InputScript"/> added to the keys held for real. The script can
/// be replaced while the game runs (<see cref="Replace"/>); presses of the new script that already started are not
/// announced again.
/// </summary>
internal sealed class ScriptedDesktopInput : IDesktopSource
{
    private readonly IDesktopSource _inner;
    private readonly Stopwatch _clock;
    private readonly LoaderLog _log;
    private volatile ScriptState _state;

    /// <param name="inner">The real keyboard and mouse.</param>
    /// <param name="script">The presses.</param>
    /// <param name="clock">Started when the loader started; the script's times count from it.</param>
    /// <param name="log">Each press is logged when it starts.</param>
    public ScriptedDesktopInput(IDesktopSource inner, InputScript script, Stopwatch clock, LoaderLog log)
    {
        _inner = inner;
        _clock = clock;
        _log = log;
        _state = new ScriptState(script, new bool[script.Presses.Count]);
    }

    /// <summary>Uses <paramref name="script"/> from the next poll on; any thread.</summary>
    public void Replace(InputScript script)
    {
        double seconds = _clock.Elapsed.TotalSeconds;
        var announced = new bool[script.Presses.Count];

        for (int i = 0; i < announced.Length; i++)
        {
            announced[i] = script.Started(i, seconds);
        }

        _state = new ScriptState(script, announced);
    }

    [HotPath]
    public void ReadKeys(PressedKeys keys)
    {
        _inner.ReadKeys(keys);
        double seconds = _clock.Elapsed.TotalSeconds;
        var state = _state;
        state.Script.Apply(keys, seconds);
        Announce(state, seconds);
    }

    /// <summary>Fills <paramref name="keys"/> with the keys held for real, without the script's.</summary>
    public void ReadRealKeys(PressedKeys keys)
    {
        _inner.ReadKeys(keys);
    }

    [HotPath]
    public CursorPosition ReadCursor()
    {
        var cursor = _inner.ReadCursor();
        var offset = _state.Script.CursorOffset(_clock.Elapsed.TotalSeconds);

        return new CursorPosition(cursor.X + offset.X, cursor.Y + offset.Y);
    }

    [HotPath]
    public uint ScanCode(int virtualKey)
    {
        return _inner.ScanCode(virtualKey);
    }

    [HotPath]
    private void Announce(ScriptState state, double seconds)
    {
        for (int i = 0; i < state.Announced.Length; i++)
        {
            if (!state.Announced[i] && state.Script.Started(i, seconds))
            {
                state.Announced[i] = true;
                Log(state.Script.Presses[i], seconds);
            }
        }
    }

    private void Log(ScriptedPress press, double seconds)
    {
        _log.Info($"  [input] script: {press.Name} down at {seconds:F1} s for {press.Hold} s");
    }
}
