// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Runtime.InteropServices;
using FiveStar.Loader.Native;

namespace FiveStar.Loader.Shims.Gdk;

/// <summary>
/// XStore.h contexts, licenses and license events; the work is in <see cref="StoreService"/>. Acquiring an add-on or
/// package license needs the store, so it fails with E_GAMESTORE_NETWORK_ERROR and no license handle is issued.
/// </summary>
internal static unsafe class XStoreShim
{
    /// <summary>HRESULT XStoreCreateContext(XUserHandle user, XStoreContextHandle* storeContextHandle).</summary>
    [Shim(GdkModule.Name, "XStoreCreateContext")]
    [UnmanagedCallersOnly]
    private static int XStoreCreateContext(nint user, nint* context)
    {
        return ShimContext.Runtime.Gdk.Store.CreateContext(context);
    }

    [Shim(GdkModule.Name, "XStoreCloseContextHandle")]
    [UnmanagedCallersOnly]
    private static void XStoreCloseContextHandle(nint context)
    {
        ShimContext.Runtime.Gdk.Store.CloseContext(context);
    }

    /// <summary>HRESULT XStoreQueryGameLicenseAsync(const XStoreContextHandle storeContextHandle, XAsyncBlock* async).</summary>
    [Shim(GdkModule.Name, StoreService.GameLicenseApi)]
    [UnmanagedCallersOnly]
    private static int XStoreQueryGameLicenseAsync(nint context, nint async)
    {
        return AsyncCalls.BeginStore(context, async, StoreService.GameLicenseApi, new GameLicenseOperation(ShimContext.Runtime.Gdk.Store));
    }

    /// <summary>HRESULT XStoreQueryGameLicenseResult(XAsyncBlock* async, XStoreGameLicense* license).</summary>
    [Shim(GdkModule.Name, "XStoreQueryGameLicenseResult")]
    [UnmanagedCallersOnly]
    private static int XStoreQueryGameLicenseResult(nint async, byte* license)
    {
        if (license == null)
        {
            return HResult.Pointer;
        }

        return AsyncCalls.Result(async, StoreService.GameLicenseApi, StoreService.GameLicenseSize, (nint)license);
    }

    /// <summary>HRESULT XStoreAcquireLicenseForPackageAsync(XStoreContextHandle, const char* packageIdentifier, XAsyncBlock* async).</summary>
    [Shim(GdkModule.Name, nameof(XStoreAcquireLicenseForPackageAsync))]
    [UnmanagedCallersOnly]
    private static int XStoreAcquireLicenseForPackageAsync(nint context, byte* packageIdentifier, nint async)
    {
        return AsyncCalls.BeginStoreOffline(context, async, nameof(XStoreAcquireLicenseForPackageAsync));
    }

    /// <summary>HRESULT XStoreAcquireLicenseForPackageResult(XAsyncBlock* async, XStoreLicenseHandle* storeLicenseHandle).</summary>
    [Shim(GdkModule.Name, "XStoreAcquireLicenseForPackageResult")]
    [UnmanagedCallersOnly]
    private static int XStoreAcquireLicenseForPackageResult(nint async, nint* license)
    {
        return AsyncCalls.HandleResult(async, nameof(XStoreAcquireLicenseForPackageAsync), license);
    }

    /// <summary>HRESULT XStoreAcquireLicenseForDurablesAsync(XStoreContextHandle, const char* storeId, XAsyncBlock* async).</summary>
    [Shim(GdkModule.Name, nameof(XStoreAcquireLicenseForDurablesAsync))]
    [UnmanagedCallersOnly]
    private static int XStoreAcquireLicenseForDurablesAsync(nint context, byte* storeId, nint async)
    {
        return AsyncCalls.BeginStoreOffline(context, async, nameof(XStoreAcquireLicenseForDurablesAsync));
    }

    /// <summary>HRESULT XStoreAcquireLicenseForDurablesResult(XAsyncBlock* async, XStoreLicenseHandle* storeLicenseHandle).</summary>
    [Shim(GdkModule.Name, "XStoreAcquireLicenseForDurablesResult")]
    [UnmanagedCallersOnly]
    private static int XStoreAcquireLicenseForDurablesResult(nint async, nint* license)
    {
        return AsyncCalls.HandleResult(async, nameof(XStoreAcquireLicenseForDurablesAsync), license);
    }

    /// <summary>bool XStoreIsLicenseValid(XStoreLicenseHandle): no license handle is ever issued, so none is valid.</summary>
    [Shim(GdkModule.Name, "XStoreIsLicenseValid")]
    [UnmanagedCallersOnly]
    private static byte XStoreIsLicenseValid(nint license)
    {
        return GdkModule.False;
    }

    [Shim(GdkModule.Name, "XStoreCloseLicenseHandle")]
    [UnmanagedCallersOnly]
    private static void XStoreCloseLicenseHandle(nint license)
    {
    }

    /// <summary>HRESULT XStoreRegisterGameLicenseChanged(XStoreContextHandle, XTaskQueueHandle, void* context, callback, XTaskQueueRegistrationToken*).</summary>
    [Shim(GdkModule.Name, "XStoreRegisterGameLicenseChanged")]
    [UnmanagedCallersOnly]
    private static int XStoreRegisterGameLicenseChanged(nint store, nint queue, nint context, nint callback, ulong* token)
    {
        var gdk = ShimContext.Runtime.Gdk;

        if (!gdk.Store.IsContext(store))
        {
            return HResult.InvalidArgument;
        }

        return gdk.Registrations.Register("XStoreRegisterGameLicenseChanged", callback, token);
    }

    /// <summary>HRESULT XStoreRegisterPackageLicenseLost(XStoreLicenseHandle, XTaskQueueHandle, void* context, callback, XTaskQueueRegistrationToken*).</summary>
    [Shim(GdkModule.Name, "XStoreRegisterPackageLicenseLost")]
    [UnmanagedCallersOnly]
    private static int XStoreRegisterPackageLicenseLost(nint license, nint queue, nint context, nint callback, ulong* token)
    {
        return ShimContext.Runtime.Gdk.Registrations.Register("XStoreRegisterPackageLicenseLost", callback, token);
    }

    /// <summary>bool XStoreUnregister*(handle, XTaskQueueRegistrationToken token, bool wait).</summary>
    [Shim(GdkModule.Name, "XStoreUnregisterGameLicenseChanged")]
    [Shim(GdkModule.Name, "XStoreUnregisterPackageLicenseLost")]
    [UnmanagedCallersOnly]
    private static byte XStoreUnregister(nint handle, ulong token, byte wait)
    {
        return ShimContext.Runtime.Gdk.Registrations.Unregister(token);
    }

    /// <summary>HRESULT XStoreQueryPackageIdentifier(const char* storeId, size_t size, char* packageIdentifier).</summary>
    [Shim(GdkModule.Name, "XStoreQueryPackageIdentifier")]
    [UnmanagedCallersOnly]
    private static int XStoreQueryPackageIdentifier(byte* storeId, nuint size, byte* packageIdentifier)
    {
        var gdk = ShimContext.Runtime.Gdk;

        try
        {
            return gdk.Store.QueryPackageIdentifier(Utf8Text.Read((nint)storeId), size, packageIdentifier);
        }
        catch (Exception e)
        {
            return gdk.Boundary.Failed(nameof(XStoreQueryPackageIdentifier), e);
        }
    }
}
