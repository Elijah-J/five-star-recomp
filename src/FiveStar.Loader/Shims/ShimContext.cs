// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

namespace FiveStar.Loader.Shims;

/// <summary>
/// The one static handle of the loader. Shim entry points are [UnmanagedCallersOnly] static methods whose addresses
/// are written into the IATs of mapped images and handed to native callbacks; such a method cannot carry an instance,
/// so each one resolves the active runtime here and calls into the services it owns. All state lives in that runtime.
/// </summary>
internal static class ShimContext
{
    private static LoaderRuntime? _runtime;

    /// <summary>The runtime whose images are mapped; the most recently created one.</summary>
    public static LoaderRuntime Runtime => Volatile.Read(ref _runtime) ?? throw new InvalidOperationException("no loader runtime is active");

    public static void Activate(LoaderRuntime runtime)
    {
        Volatile.Write(ref _runtime, runtime);
    }
}
