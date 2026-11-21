// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using FiveStar.Loader.Calls;
using FiveStar.Loader.Diagnostics;
using FiveStar.Loader.Loading;

namespace FiveStar.Loader.Host;

/// <summary>Runs one fivestar-loader invocation: load, report, call exports, unload.</summary>
internal sealed class LoaderHost
{
    public const int Success = 0;
    public const int Failure = 1;
    public const int UsageError = 2;

    private readonly HostOptions _options;
    private readonly LoaderRuntime _runtime;
    private readonly LoaderLog _log;
    private readonly int _processId;

    public LoaderHost(HostOptions options, LoaderRuntime runtime, LoaderLog log, int processId)
    {
        _options = options;
        _runtime = runtime;
        _log = log;
        _processId = processId;
    }

    public int Run()
    {
        foreach (string spec in _options.DisabledShims)
        {
            if (!_runtime.Shims.Disable(spec))
            {
                _log.Error($"no such shim {spec}");

                return UsageError;
            }
        }

        string manual = string.Join(", ", _runtime.Loader.ManualModules.Order(StringComparer.Ordinal));
        string listed = manual.Length == 0 ? "(none)" : manual;
        _log.Info($"fivestar-loader pid {_processId}; manual modules: {listed}; unresolved={_options.Unresolved}");

        LoadAbiTable();
        var (targets, rc) = LoadTargets();
        LoadReport.Print(_runtime, targets, _options.ListExports);
        ReportMainImage();

        rc = CallExports(rc);
        Finish();
        _log.Info($"done, exit code {rc}");

        return rc;
    }

    private void LoadAbiTable()
    {
        if (_options.GdkAbiTable is not null)
        {
            _runtime.Gdk.Runtime.LoadAbiTable(_options.GdkAbiTable);
        }
    }

    private (List<LoadedImage> Targets, int Rc) LoadTargets()
    {
        var targets = new List<LoadedImage>();
        int rc = Success;

        foreach (string path in _options.Loads)
        {
            try
            {
                targets.Add(_runtime.Loader.Load(path, true));
            }
            catch (Exception e)
            {
                _log.Error($"load {path} failed: {e.GetType().Name}: {e.Message}");
                rc = Failure;
            }
        }

        return (targets, rc);
    }

    private void ReportMainImage()
    {
        var main = _runtime.Loader.MainImage;

        if (main is null)
        {
            return;
        }

        _log.Info($"== process identity: main image {main.Name} @ 0x{main.Base:x} ({main.Path})");

        if (_options.PatchPeb)
        {
            _runtime.Identity.PatchPeb(main.Base);
        }
    }

    private int CallExports(int rc)
    {
        var caller = new ExportCaller(_runtime, _log);
        int result = rc;

        foreach (string request in _options.Calls)
        {
            var outcomes = caller.Run(request);

            if (outcomes is null || outcomes.Any(o => o.Result is null))
            {
                result = Failure;
            }
        }

        return result;
    }

    private void Finish()
    {
        var stubs = _runtime.Stubs;

        if (stubs.CallCount > 0)
        {
            _log.Warn($"{stubs.CallCount} call(s) hit unresolved-import stubs");
        }

        if (stubs.NotImplementedCallCount > 0)
        {
            _log.Info($"{stubs.NotImplementedCallCount} call(s) hit not-implemented GDK stubs");
        }

        _runtime.Identity.RestorePeb();

        if (_options.Unload)
        {
            _runtime.Loader.UnloadAll();
        }
    }
}
