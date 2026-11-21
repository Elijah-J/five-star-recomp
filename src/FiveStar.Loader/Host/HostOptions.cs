// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using FiveStar.Loader.Loading;

namespace FiveStar.Loader.Host;

/// <summary>The parsed command line of fivestar-loader.</summary>
internal sealed record HostOptions
{
    public List<string> Loads { get; } = [];

    public List<string> Calls { get; } = [];

    public List<string> ManualModules { get; } = [];

    public List<string> SearchDirectories { get; } = [];

    public List<string> DisabledShims { get; } = [];

    public bool SystemCrt { get; set; }

    public UnresolvedMode Unresolved { get; set; } = UnresolvedMode.Stub;

    public bool CallEntryPoints { get; set; } = true;

    public bool ListExports { get; set; } = true;

    public bool Unload { get; set; }

    public string? WorkDirectory { get; set; }

    public string? LogFile { get; set; }

    public string? MainModule { get; set; }

    public bool PatchPeb { get; set; }

    public string? PackageRoot { get; set; }

    public string? SaveDirectory { get; set; }

    public string? GdkAbiTable { get; set; }

    public uint? DeviceType { get; set; }

    public bool Verbose { get; set; }

    public bool ListSafeCalls { get; set; }

    public bool Help { get; set; }
}
