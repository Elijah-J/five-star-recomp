// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using FiveStar.Loader.Codegen;
using FiveStar.Loader.Diagnostics;
using FiveStar.Loader.Loading;

namespace FiveStar.Loader.Shims.Input;

/// <summary>
/// GameInputCreate's side of the loader: holds the sources and the input config, and builds the
/// <see cref="GameInputSession"/> on the first call, so a run that never touches GameInput allocates nothing native.
/// </summary>
internal sealed class GameInputService : IDisposable
{
    private readonly object _gate = new();
    private readonly IGamepadSource _gamepads;
    private IDesktopSource _desktop;
    private readonly TimeProvider _time;
    private readonly UnresolvedStubs _stubs;
    private readonly CodeHeap _heap;
    private readonly LoaderLog _log;
    private InputConfig _config;
    private GameInputSession? _session;

    public GameInputService(IGamepadSource gamepads, IDesktopSource desktop, InputConfig config, TimeProvider time, UnresolvedStubs stubs, CodeHeap heap, LoaderLog log)
    {
        _gamepads = gamepads;
        _desktop = desktop;
        _config = config;
        _time = time;
        _stubs = stubs;
        _heap = heap;

        _log = log;
    }

    /// <summary>Replaces the virtual-gamepad config (the --input file).</summary>
    public void Configure(InputConfig config)
    {
        lock (_gate)
        {
            _config = config;
            _session?.Configure(config);
        }
    }

    /// <summary>Adds the presses of an input script (<c>--input-script</c>) to the desktop keys; before GameInputCreate.</summary>
    public void UseScript(InputScript script, System.Diagnostics.Stopwatch clock)
    {
        lock (_gate)
        {
            _desktop = new ScriptedDesktopInput(_desktop, script, clock, _log);
        }
    }

    /// <summary>GameInput use so far, for the thread sampler.</summary>
    public string Summary
    {
        get
        {
            lock (_gate)
            {
                if (_session is null)
                {
                    return "GameInput not created";
                }

                return $"GameInput {_session.Summary}";
            }
        }
    }

    /// <summary>GameInputCreate: the IGameInput object, with one reference for the caller.</summary>
    public nint Create()
    {
        lock (_gate)
        {
            if (_session is null)
            {
                string virtualPad = _config.VirtualGamepad ? "on" : "off";
                _session = new GameInputSession(_gamepads, _desktop, _config, _time, _stubs, _heap, _log);
                _log.Info($"  [input] GameInput created: {_gamepads.SlotCount} gamepad slots, keyboard, mouse, virtual gamepad {virtualPad}");
            }

            return _session.AddInputReference();
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _session?.Dispose();
            _session = null;
        }
    }
}
