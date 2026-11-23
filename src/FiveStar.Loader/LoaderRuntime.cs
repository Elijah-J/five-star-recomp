// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using FiveStar.Loader.Calls;
using FiveStar.Loader.Codegen;
using FiveStar.Loader.Diagnostics;
using FiveStar.Loader.Loading;
using FiveStar.Loader.Mods;
using FiveStar.Loader.Shims;
using FiveStar.Loader.Shims.Gdk;
using FiveStar.Loader.Shims.Input;

namespace FiveStar.Loader;

/// <summary>
/// The composition root of one loader: the image loader, the shim table and every service the shims call. Creating it
/// makes it the runtime the shim entry points reach through <see cref="ShimContext"/>.
/// </summary>
internal sealed class LoaderRuntime : IDisposable
{
    public LoaderRuntime(LoaderOptions options, GdkOptions gdk, LoaderEnvironment environment, LoaderLog log)
    {
        Log = log;
        Counters = new LoaderCounters();
        CodeHeap = new CodeHeap();
        LastError = new LastErrorBridge(CodeHeap, log);
        Shims = new ShimTable(LastError, CodeHeap);
        Stubs = new UnresolvedStubs(CodeHeap, options.Unresolved, log, environment.FailFast);

        Images = new ImageTable();
        VirtualModules = new VirtualModules(log);
        Patcher = new ImagePatcher(Shims, log);
        Trace = new CallTrace(CodeHeap);
        Faults = new FaultReporter(Images, Trace, log);

        Loader = new ImageLoader(options, Shims, Stubs, Images, CodeHeap, new TlsHostAttacher(options.WorkDirectory, options.ProcessId, log), Patcher, Trace, log);
        Router = new RuntimeLibraryRouter(Loader, VirtualModules, Shims, Images, log);
        Identity = new ProcessIdentity(log);
        ModuleIdentity = new ModuleIdentity(Loader, Router, Images, VirtualModules, Shims, Stubs, LastError, Counters, log, environment.SystemDirectory);

        XMem = new XMemAllocator(Images, log);
        D3D12 = new D3D12Forwarder(log);
        Gdk = CreateGdk(gdk, environment.Time);
        Overlay = new FileOverlay(Gdk.Config, log);
        RecipeAllocator = new RecipeAllocator();
        Input = new GameInputService(new XInputGamepads(), new DesktopInput(), InputConfigFile.Defaults(), environment.Time, Stubs, CodeHeap, log);

        ShimContext.Activate(this);
    }

    /// <summary>The allocator the export-call recipes hand to libHttpClient.</summary>
    public RecipeAllocator RecipeAllocator { get; }

    public LoaderLog Log { get; }

    public LoaderCounters Counters { get; }

    public CodeHeap CodeHeap { get; }

    public LastErrorBridge LastError { get; }

    public ShimTable Shims { get; }

    public UnresolvedStubs Stubs { get; }

    public ImageTable Images { get; }

    public VirtualModules VirtualModules { get; }

    public ImageLoader Loader { get; }

    /// <summary>The ring of recent calls through shims and unresolved stubs, when enabled.</summary>
    public CallTrace Trace { get; }

    /// <summary>The vectored exception handler that reports faults in mapped code.</summary>
    public FaultReporter Faults { get; }

    /// <summary>The code patches applied to images as they are mapped.</summary>
    public ImagePatcher Patcher { get; }

    /// <summary>The mod folders the file shims consult.</summary>
    public FileOverlay Overlay { get; }

    public RuntimeLibraryRouter Router { get; }

    public ProcessIdentity Identity { get; }

    public ModuleIdentity ModuleIdentity { get; }

    public XMemAllocator XMem { get; }

    public D3D12Forwarder D3D12 { get; }

    public GdkServices Gdk { get; }

    /// <summary>GameInputCreate and the gamepad, keyboard and mouse sources behind it.</summary>
    public GameInputService Input { get; }

    public void Dispose()
    {
        Identity.RestorePeb();
        Gdk.Dispose();
        Input.Dispose();
        LastError.Dispose();
    }

    private GdkServices CreateGdk(GdkOptions options, TimeProvider time)
    {
        var config = new GdkConfig(options, Loader, Log);
        var boundary = new NativeBoundary(Log);
        var taskQueues = new TaskQueueService(boundary);
        var async = new AsyncService(taskQueues, boundary);
        var users = new UserService();
        var saves = new GameSaveService(config, new SaveStore(config, boundary, time), users, async, boundary);

        var packages = new PackageService(config);
        var system = new SystemService(config, boundary);
        var store = new StoreService(config);
        var runtime = new GameRuntimeService(config, Shims, Stubs, CodeHeap, Log);
        var platform = new GdkPlatformServices(new EventRegistrations(), new DisplayService(boundary), new AccessibilityService(), new LauncherService(boundary));

        return new GdkServices(config, boundary, taskQueues, async, users, saves, packages, system, store, runtime, platform);
    }
}
