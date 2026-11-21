// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Runtime.InteropServices;
using FiveStar.Loader.Native;

namespace FiveStar.Loader.Shims.Gdk;

/// <summary>XStore.h entry points; the work is in <see cref="StoreService"/>.</summary>
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
    [Shim(GdkModule.Name, "XStoreQueryGameLicenseAsync")]
    [UnmanagedCallersOnly]
    private static int XStoreQueryGameLicenseAsync(nint context, nint async)
    {
        var gdk = ShimContext.Runtime.Gdk;

        try
        {
            if (!gdk.Store.IsContext(context))
            {
                return HResult.InvalidArgument;
            }

            return gdk.Async.BeginManaged(async, StoreService.GameLicenseApi, new GameLicenseOperation(gdk.Store));
        }
        catch (Exception e)
        {
            return gdk.Boundary.Failed(nameof(XStoreQueryGameLicenseAsync), e);
        }
    }

    /// <summary>HRESULT XStoreQueryGameLicenseResult(XAsyncBlock* async, XStoreGameLicense* license).</summary>
    [Shim(GdkModule.Name, "XStoreQueryGameLicenseResult")]
    [UnmanagedCallersOnly]
    private static int XStoreQueryGameLicenseResult(nint async, byte* license)
    {
        var gdk = ShimContext.Runtime.Gdk;

        if (license == null)
        {
            return HResult.Pointer;
        }

        try
        {
            return gdk.Async.ManagedResult(async, StoreService.GameLicenseApi, StoreService.GameLicenseSize, (nint)license);
        }
        catch (Exception e)
        {
            return gdk.Boundary.Failed(nameof(XStoreQueryGameLicenseResult), e);
        }
    }
}
