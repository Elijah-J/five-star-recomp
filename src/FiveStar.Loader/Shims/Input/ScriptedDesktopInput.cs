// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Diagnostics;
using FiveStar.Loader.Diagnostics;

namespace FiveStar.Loader.Shims.Input;

/// <summary>A desktop source with the presses of an <see cref="InputScript"/> added to the keys held for real.</summary>
internal sealed class ScriptedDesktopInput : IDesktopSource
{
    private readonly IDesktopSource _inner;
    private readonly InputScript _script;
    private readonly Stopwatch _clock;
    private readonly LoaderLog _log;
    private int _announced;

    /// <param name="inner">The real keyboard and mouse.</param>
    /// <param name="script">The presses.</param>
    /// <param name="clock">Started when the loader started; the script's times count from it.</param>
    /// <param name="log">Each press is logged when it starts.</param>
    public ScriptedDesktopInput(IDesktopSource inner, InputScript script, Stopwatch clock, LoaderLog log)
    {
        _inner = inner;
        _script = script;
        _clock = clock;
        _log = log;
    }

    [HotPath]
    public void ReadKeys(PressedKeys keys)
    {
        _inner.ReadKeys(keys);
        double seconds = _clock.Elapsed.TotalSeconds;
        _script.Apply(keys, seconds);

        if (_announced < _script.Presses.Count && _script.Presses[_announced].Start <= seconds)
        {
            Announce(seconds);
        }
    }

    private void Announce(double seconds)
    {
        while (_announced < _script.Presses.Count && _script.Presses[_announced].Start <= seconds)
        {
            var press = _script.Presses[_announced++];
            _log.Info($"  [input] script: {press.Name} down at {seconds:F1} s for {press.Hold} s");
        }
    }

    [HotPath]
    public CursorPosition ReadCursor()
    {
        return _inner.ReadCursor();
    }

    [HotPath]
    public uint ScanCode(int virtualKey)
    {
        return _inner.ScanCode(virtualKey);
    }
}
