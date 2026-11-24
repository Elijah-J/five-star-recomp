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
    private volatile InputScript _script;
    private InputScript? _announcedScript;
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
        _announcedScript = script;
    }

    /// <summary>Uses <paramref name="script"/> from the next poll on; any thread.</summary>
    public void Replace(InputScript script)
    {
        _script = script;
    }

    [HotPath]
    public void ReadKeys(PressedKeys keys)
    {
        _inner.ReadKeys(keys);
        double seconds = _clock.Elapsed.TotalSeconds;
        var script = _script;

        if (!ReferenceEquals(script, _announcedScript))
        {
            _announcedScript = script;
            _announced = script.StartedBefore(seconds);
        }

        script.Apply(keys, seconds);

        if (_announced < script.Presses.Count && script.Presses[_announced].Start <= seconds)
        {
            Announce(script, seconds);
        }
    }

    private void Announce(InputScript script, double seconds)
    {
        while (_announced < script.Presses.Count && script.Presses[_announced].Start <= seconds)
        {
            var press = script.Presses[_announced++];
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
