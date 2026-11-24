// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Diagnostics;
using FiveStar.Loader.Diagnostics;

namespace FiveStar.Loader.Shims.Input;

/// <summary>
/// Follows the <c>--input-script</c> file while the game runs: every <see cref="Interval"/> it reloads the file when
/// its write time changed (a file that does not parse keeps the old script, warned once per change) and starts the
/// <c>capture</c> lines whose time has come. Captures already due when a script is loaded are skipped.
/// </summary>
internal sealed class InputScriptWatcher : IDisposable
{
    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(250);

    private readonly string _path;
    private readonly ScriptedDesktopInput _input;
    private readonly Stopwatch _clock;
    private readonly Action _capture;
    private readonly LoaderLog _log;
    private readonly Timer _timer;
    private readonly Lock _gate = new();
    private InputScript _script;
    private DateTime _written;
    private int _nextCapture;

    /// <param name="path">The script file.</param>
    /// <param name="script">The script as loaded at start.</param>
    /// <param name="input">The scripted desktop source the presses go to.</param>
    /// <param name="clock">The script's clock.</param>
    /// <param name="capture">Requests a readback of the next present.</param>
    /// <param name="log">Reloads and captures are logged.</param>
    public InputScriptWatcher(string path, InputScript script, ScriptedDesktopInput input, Stopwatch clock, Action capture, LoaderLog log)
    {
        _path = path;
        _script = script;
        _input = input;
        _clock = clock;
        _capture = capture;
        _log = log;
        _written = File.GetLastWriteTimeUtc(path);
        _timer = new Timer(_ => Tick(), null, Interval, Interval);
    }

    public void Dispose()
    {
        _timer.Dispose();
    }

    private void Tick()
    {
        if (!_gate.TryEnter())
        {
            return;
        }

        try
        {
            double seconds = _clock.Elapsed.TotalSeconds;
            Reload(seconds);
            StartCaptures(seconds);
        }
        finally
        {
            _gate.Exit();
        }
    }

    private void Reload(double seconds)
    {
        DateTime written;

        try
        {
            written = File.GetLastWriteTimeUtc(_path);

            if (written == _written)
            {
                return;
            }

            _written = written;
            _script = InputScript.Load(_path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or FormatException)
        {
            _log.Warn($"[input] script not reloaded: {exception.Message}");

            return;
        }

        _input.Replace(_script);
        _nextCapture = _script.Captures.Count(time => time <= seconds);
        _log.Info($"  [input] script reloaded at {seconds:F1} s: {_script.Presses.Count} presses, {_script.Captures.Count} captures");
    }

    private void StartCaptures(double seconds)
    {
        var captures = _script.Captures;

        while (_nextCapture < captures.Count && captures[_nextCapture] <= seconds)
        {
            _nextCapture++;
            _capture();
            _log.Info($"  [input] script: capture at {seconds:F1} s");
        }
    }
}
