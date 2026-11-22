// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using FiveStar.Loader.Loading;

namespace FiveStar.Loader.Host;

/// <summary>Parses the fivestar-loader command line; <see cref="HostUsage"/> lists the options.</summary>
internal sealed class CommandLineParser
{
    private readonly string[] _args;
    private readonly HostOptions _options = new();
    private int _index;

    public CommandLineParser(string[] args)
    {
        _args = args;
    }

    /// <summary>Parses every argument; stops early at --help and --list-safe-calls.</summary>
    public HostOptions Parse()
    {
        for (_index = 0; _index < _args.Length; _index++)
        {
            if (!Apply(_args[_index]))
            {
                break;
            }
        }

        return _options;
    }

    /// <summary>Applies one option; false ends parsing.</summary>
    private bool Apply(string option)
    {
        switch (option)
        {
            case "--load":
                return Add(_options.Loads);
            case "--call":
                return Add(_options.Calls);
            case "--manual":
                return AddManual();
            case "--system-crt":
                return SetSystemCrt();
            case "--search":
                return AddSearchDirectory();
            case "--unresolved":
                return SetUnresolved();
            case "--no-entry":
                return DisableEntryPoints();
            case "--no-shim":
                return Add(_options.DisabledShims);
            case "--no-exports":
                return DisableExportListing();
            case "--unload":
                return SetUnload();
            case "--work-dir":
                return SetWorkDirectory();
            case "--log":
                return SetLogFile();
            case "--main":
                return SetMainModule();
            case "--patch-peb":
                return SetPatchPeb();
            case "--package-root":
                return SetPackageRoot();
            case "--save-dir":
                return SetSaveDirectory();
            case "--gdk-abi":
                return SetGdkAbi();
            case "--device-type":
                return SetDeviceType();
            case "--patches":
                return AddFullPath(_options.Patches);
            case "--mods":
                return AddFullPath(_options.ModFolders);
            case "-v":
                return SetVerbose();
            case "--list-safe-calls":
                return StopWithListSafeCalls();
            case "-h":
            case "--help":
                return StopWithHelp();
            default:
                throw new CommandLineException($"unknown argument {option}");
        }
    }

    private string Next()
    {
        if (_index + 1 >= _args.Length)
        {
            throw new CommandLineException($"{_args[_index]} needs a value");
        }

        _index++;

        return _args[_index];
    }

    private bool Add(List<string> values)
    {
        values.Add(Next());

        return true;
    }

    private bool AddFullPath(List<string> values)
    {
        values.Add(Path.GetFullPath(Next()));

        return true;
    }

    private bool AddManual()
    {
        foreach (string module in Next().Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            _options.ManualModules.Add(ModuleNames.NormalizeImport(module));
        }

        return true;
    }

    private bool SetSystemCrt()
    {
        _options.SystemCrt = true;

        return true;
    }

    private bool AddSearchDirectory()
    {
        _options.SearchDirectories.Add(Path.GetFullPath(Next()));

        return true;
    }

    private bool SetUnresolved()
    {
        _options.Unresolved = Enum.Parse<UnresolvedMode>(Next(), true);

        return true;
    }

    private bool DisableEntryPoints()
    {
        _options.CallEntryPoints = false;

        return true;
    }

    private bool DisableExportListing()
    {
        _options.ListExports = false;

        return true;
    }

    private bool SetUnload()
    {
        _options.Unload = true;

        return true;
    }

    private bool SetWorkDirectory()
    {
        _options.WorkDirectory = Path.GetFullPath(Next());

        return true;
    }

    private bool SetLogFile()
    {
        _options.LogFile = Next();

        return true;
    }

    private bool SetMainModule()
    {
        _options.MainModule = Path.GetFileName(Next()).ToLowerInvariant();

        return true;
    }

    private bool SetPatchPeb()
    {
        _options.PatchPeb = true;

        return true;
    }

    private bool SetPackageRoot()
    {
        _options.PackageRoot = Path.GetFullPath(Next());

        return true;
    }

    private bool SetSaveDirectory()
    {
        _options.SaveDirectory = Path.GetFullPath(Next());

        return true;
    }

    private bool SetGdkAbi()
    {
        _options.GdkAbiTable = Next();

        return true;
    }

    private bool SetDeviceType()
    {
        _options.DeviceType = uint.Parse(Next(), CultureInfo.InvariantCulture);

        return true;
    }

    private bool SetVerbose()
    {
        _options.Verbose = true;

        return true;
    }

    private bool StopWithListSafeCalls()
    {
        _options.ListSafeCalls = true;

        return false;
    }

    private bool StopWithHelp()
    {
        _options.Help = true;

        return false;
    }
}
