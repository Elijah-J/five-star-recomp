// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Runtime.InteropServices;
using FiveStar.Loader.Native;

namespace FiveStar.Loader.Shims.Gdk;

/// <summary>
/// XStore.h package updates and downloads. The local package is the only version there is, so the update query finds
/// none; downloading anything needs the store and fails with E_GAMESTORE_NETWORK_ERROR.
/// </summary>
internal static unsafe class XStorePackageShim
{
    /// <summary>HRESULT XStoreQueryGameAndDlcPackageUpdatesAsync(XStoreContextHandle, XAsyncBlock*).</summary>
    [Shim(GdkModule.Name, nameof(XStoreQueryGameAndDlcPackageUpdatesAsync))]
    [UnmanagedCallersOnly]
    private static int XStoreQueryGameAndDlcPackageUpdatesAsync(nint context, nint async)
    {
        return AsyncCalls.BeginStore(context, async, nameof(XStoreQueryGameAndDlcPackageUpdatesAsync), new OfflineOperation(HResult.Ok));
    }

    /// <summary>HRESULT XStoreQueryGameAndDlcPackageUpdatesResultCount(XAsyncBlock*, uint32_t* count).</summary>
    [Shim(GdkModule.Name, "XStoreQueryGameAndDlcPackageUpdatesResultCount")]
    [Shim(GdkModule.Name, "XStoreDownloadAndInstallPackagesResultCount")]
    [UnmanagedCallersOnly]
    private static int XStoreResultCount(nint async, uint* count)
    {
        return AsyncCalls.EmptyCount(async, count);
    }

    /// <summary>HRESULT XStoreQueryGameAndDlcPackageUpdatesResult(XAsyncBlock*, uint32_t count, XStorePackageUpdate* packageUpdates).</summary>
    [Shim(GdkModule.Name, "XStoreQueryGameAndDlcPackageUpdatesResult")]
    [UnmanagedCallersOnly]
    private static int XStoreQueryGameAndDlcPackageUpdatesResult(nint async, uint count, nint updates)
    {
        return AsyncCalls.Result(async, nameof(XStoreQueryGameAndDlcPackageUpdatesAsync), 0, 0);
    }

    /// <summary>HRESULT XStoreDownloadAndInstallPackageUpdatesAsync(XStoreContextHandle, const char** packageIdentifiers, size_t count, XAsyncBlock*).</summary>
    [Shim(GdkModule.Name, nameof(XStoreDownloadAndInstallPackageUpdatesAsync))]
    [UnmanagedCallersOnly]
    private static int XStoreDownloadAndInstallPackageUpdatesAsync(nint context, nint packageIdentifiers, nuint count, nint async)
    {
        return AsyncCalls.BeginStoreOffline(context, async, nameof(XStoreDownloadAndInstallPackageUpdatesAsync));
    }

    [Shim(GdkModule.Name, "XStoreDownloadAndInstallPackageUpdatesResult")]
    [UnmanagedCallersOnly]
    private static int XStoreDownloadAndInstallPackageUpdatesResult(nint async)
    {
        return AsyncCalls.Result(async, nameof(XStoreDownloadAndInstallPackageUpdatesAsync), 0, 0);
    }

    /// <summary>HRESULT XStoreDownloadAndInstallPackagesAsync(XStoreContextHandle, const char** storeIds, size_t count, XAsyncBlock*).</summary>
    [Shim(GdkModule.Name, nameof(XStoreDownloadAndInstallPackagesAsync))]
    [UnmanagedCallersOnly]
    private static int XStoreDownloadAndInstallPackagesAsync(nint context, nint storeIds, nuint count, nint async)
    {
        return AsyncCalls.BeginStoreOffline(context, async, nameof(XStoreDownloadAndInstallPackagesAsync));
    }

    /// <summary>HRESULT XStoreDownloadAndInstallPackagesResult(XAsyncBlock*, uint32_t count, char packageIdentifiers[][XPACKAGE_IDENTIFIER_MAX_LENGTH]).</summary>
    [Shim(GdkModule.Name, "XStoreDownloadAndInstallPackagesResult")]
    [UnmanagedCallersOnly]
    private static int XStoreDownloadAndInstallPackagesResult(nint async, uint count, nint packageIdentifiers)
    {
        return AsyncCalls.Result(async, nameof(XStoreDownloadAndInstallPackagesAsync), 0, 0);
    }
}
