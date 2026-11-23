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

    /// <summary>Patch files and folders of patch files, in order.</summary>
    public List<string> Patches { get; } = [];

    /// <summary>Mod folders, highest priority first.</summary>
    public List<string> ModFolders { get; } = [];

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

    public string? InputConfig { get; set; }

    /// <summary>Timed key presses added to the desktop input (<c>--input-script</c>).</summary>
    public string? InputScript { get; set; }

    /// <summary>Extracted FSSM programs (`fivestar shader extract`) for pipeline creation.</summary>
    public string? ShaderDirectory { get; set; }

    /// <summary>Translated DXIL programs (`fivestar shader translate`) for pipeline creation.</summary>
    public string? TranslatedDirectory { get; set; }

    /// <summary>The PC FidelityFX API DLL the console FidelityFX calls are forwarded to; null leaves FidelityFX off.</summary>
    public string? FidelityFxLibrary { get; set; }

    /// <summary>Run the main executable's entry point after loading, with the call trace and fault reporter on.</summary>
    public bool RunMain { get; set; }

    /// <summary>How long --run lets the entry point run before reporting the main thread and exiting; null waits.</summary>
    public TimeSpan? RunTimeout { get; set; }

    /// <summary>With --run, log call counts and every thread's stack at this interval (<c>--sample-threads</c>).</summary>
    public TimeSpan? SampleThreads { get; set; }

    /// <summary>With --sample-threads, also a profile burst of the running threads per sample (<c>--profile-threads</c>).</summary>
    public bool ProfileThreads { get; set; }

    /// <summary>With --run, also trace calls into these mapped images (<c>--trace-module</c>).</summary>
    public List<string> TracedModules { get; } = [];

    /// <summary>With --run, also trace calls to System32 imports.</summary>
    public bool TraceSystemCalls { get; set; }

    /// <summary>The D3D12 debug layer, its messages and DRED in the loader log (<c>--d3d12-debug</c>).</summary>
    public bool D3D12Debug { get; set; }

    /// <summary>With the debug layer, also GPU-based validation (<c>--d3d12-gpu-validation</c>).</summary>
    public bool D3D12GpuValidation { get; set; }

    /// <summary>D3D12.X slots that do nothing, to bisect GPU faults (<c>--d3d12-skip list:51,queue:35</c>).</summary>
    public List<string> D3D12Skips { get; } = [];

    /// <summary>Queue submissions, signals and waits in the log (<c>--d3d12-trace-queues</c>).</summary>
    public bool D3D12TraceQueues { get; set; }

    /// <summary>Present a test pattern instead of the game's frame (<c>--present-test</c>).</summary>
    public bool PresentTest { get; set; }

    /// <summary>XboxBvhBuilder.dll's CPU paths: console sizes and BLAS rehydration (<c>--raytracing</c>).</summary>
    public bool RayTracing { get; set; }

    /// <summary>Run the XDsp convolution effects on the CPU instead of returning silence (<c>--xdsp-convolution</c>).</summary>
    public bool XDspConvolution { get; set; }

    public bool Verbose { get; set; }

    public bool ListSafeCalls { get; set; }

    /// <summary>File of <c>module!symbol</c> lines to classify through the shim table (the <c>--classify</c> mode).</summary>
    public string? ClassifyInput { get; set; }

    /// <summary>Where the classification goes; null writes it to standard output.</summary>
    public string? ClassifyOutput { get; set; }

    public bool Help { get; set; }
}
