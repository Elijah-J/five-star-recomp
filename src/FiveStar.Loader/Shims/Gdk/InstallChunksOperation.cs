// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using FiveStar.Loader.Native;

namespace FiveStar.Loader.Shims.Gdk;

/// <summary>XPackageInstallChunksAsync: nothing to install; the result is an installation monitor already at 100%.</summary>
internal sealed class InstallChunksOperation : ManagedAsyncOperation
{
    private readonly PackageService _packages;
    private readonly string? _id;
    private nint _monitor;

    public InstallChunksOperation(PackageService packages, string? id)
    {
        _packages = packages;
        _id = id;
    }

    public override unsafe AsyncWorkResult Work()
    {
        int hr = _packages.CreateMonitor(_id, out _monitor);

        return new AsyncWorkResult(hr, (nuint)sizeof(nint));
    }

    public override unsafe int Write(byte* buffer, nuint size)
    {
        *(nint*)buffer = _monitor;

        return HResult.Ok;
    }
}
