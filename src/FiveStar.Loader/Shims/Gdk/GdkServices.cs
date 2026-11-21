// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

namespace FiveStar.Loader.Shims.Gdk;

/// <summary>The offline XGameRuntime: every GDK service the shim entry points call into.</summary>
internal sealed class GdkServices : IDisposable
{
    public GdkServices(GdkConfig config, NativeBoundary boundary, TaskQueueService taskQueues, AsyncService async, UserService users, GameSaveService saves, PackageService packages, SystemService system, StoreService store, GameRuntimeService runtime)
    {
        Config = config;
        Boundary = boundary;
        TaskQueues = taskQueues;
        Async = async;
        Users = users;

        Saves = saves;
        Packages = packages;
        System = system;
        Store = store;
        Runtime = runtime;
    }

    public GdkConfig Config { get; }

    public NativeBoundary Boundary { get; }

    public TaskQueueService TaskQueues { get; }

    public AsyncService Async { get; }

    public UserService Users { get; }

    public GameSaveService Saves { get; }

    public PackageService Packages { get; }

    public SystemService System { get; }

    public StoreService Store { get; }

    public GameRuntimeService Runtime { get; }

    public void Dispose()
    {
        System.Dispose();
    }
}
