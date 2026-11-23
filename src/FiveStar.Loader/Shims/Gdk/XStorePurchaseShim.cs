// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Runtime.InteropServices;
using FiveStar.Loader.Native;

namespace FiveStar.Loader.Shims.Gdk;

/// <summary>
/// XStore.h purchase UI, consumables and user ids. There is no store UI offline, so purchase, product page and redeem
/// end as canceled (E_ABORT); consumable balances, fulfillment and the user collections/purchase ids need the store
/// service and fail with E_GAMESTORE_NETWORK_ERROR.
/// </summary>
internal static unsafe partial class XStorePurchaseShim
{
    /// <summary>HRESULT XStoreShowPurchaseUIAsync(XStoreContextHandle, const char* storeId, const char* name, const char* extendedJsonData, XAsyncBlock*).</summary>
    [Shim(GdkModule.Name, nameof(XStoreShowPurchaseUIAsync))]
    [UnmanagedCallersOnly]
    private static int XStoreShowPurchaseUIAsync(nint context, byte* storeId, byte* name, byte* json, nint async)
    {
        return BeginCanceled(context, async, nameof(XStoreShowPurchaseUIAsync));
    }

    [Shim(GdkModule.Name, "XStoreShowPurchaseUIResult")]
    [UnmanagedCallersOnly]
    private static int XStoreShowPurchaseUIResult(nint async)
    {
        return AsyncCalls.Result(async, nameof(XStoreShowPurchaseUIAsync), 0, 0);
    }

    /// <summary>HRESULT XStoreShowProductPageUIAsync(XStoreContextHandle, const char* storeId, XAsyncBlock*).</summary>
    [Shim(GdkModule.Name, nameof(XStoreShowProductPageUIAsync))]
    [UnmanagedCallersOnly]
    private static int XStoreShowProductPageUIAsync(nint context, byte* storeId, nint async)
    {
        return BeginCanceled(context, async, nameof(XStoreShowProductPageUIAsync));
    }

    [Shim(GdkModule.Name, "XStoreShowProductPageUIResult")]
    [UnmanagedCallersOnly]
    private static int XStoreShowProductPageUIResult(nint async)
    {
        return AsyncCalls.Result(async, nameof(XStoreShowProductPageUIAsync), 0, 0);
    }

    /// <summary>HRESULT XStoreShowRedeemTokenUIAsync(XStoreContextHandle, const char* token, const char** allowedStoreIds, size_t count, bool disallowCsvRedemption, XAsyncBlock*).</summary>
    [Shim(GdkModule.Name, nameof(XStoreShowRedeemTokenUIAsync))]
    [UnmanagedCallersOnly]
    private static int XStoreShowRedeemTokenUIAsync(nint context, byte* token, nint allowedStoreIds, nuint count, byte disallowCsv, nint async)
    {
        return BeginCanceled(context, async, nameof(XStoreShowRedeemTokenUIAsync));
    }

    [Shim(GdkModule.Name, "XStoreShowRedeemTokenUIResult")]
    [UnmanagedCallersOnly]
    private static int XStoreShowRedeemTokenUIResult(nint async)
    {
        return AsyncCalls.Result(async, nameof(XStoreShowRedeemTokenUIAsync), 0, 0);
    }

    /// <summary>HRESULT XStoreQueryConsumableBalanceRemainingAsync(XStoreContextHandle, const char* storeProductId, XAsyncBlock*).</summary>
    [Shim(GdkModule.Name, nameof(XStoreQueryConsumableBalanceRemainingAsync))]
    [UnmanagedCallersOnly]
    private static int XStoreQueryConsumableBalanceRemainingAsync(nint context, byte* storeProductId, nint async)
    {
        return AsyncCalls.BeginStoreOffline(context, async, nameof(XStoreQueryConsumableBalanceRemainingAsync));
    }

    /// <summary>HRESULT XStoreQueryConsumableBalanceRemainingResult(XAsyncBlock*, XStoreConsumableResult*).</summary>
    [Shim(GdkModule.Name, "XStoreQueryConsumableBalanceRemainingResult")]
    [UnmanagedCallersOnly]
    private static int XStoreQueryConsumableBalanceRemainingResult(nint async, uint* consumable)
    {
        return AsyncCalls.Result(async, nameof(XStoreQueryConsumableBalanceRemainingAsync), sizeof(uint), (nint)consumable);
    }

    /// <summary>
    /// HRESULT XStoreReportConsumableFulfillmentAsync(XStoreContextHandle, const char* storeProductId, uint32_t quantity,
    /// GUID trackingId, XAsyncBlock*); the 16-byte GUID travels by reference (x64 ABI).
    /// </summary>
    [Shim(GdkModule.Name, nameof(XStoreReportConsumableFulfillmentAsync))]
    [UnmanagedCallersOnly]
    private static int XStoreReportConsumableFulfillmentAsync(nint context, byte* storeProductId, uint quantity, nint trackingId, nint async)
    {
        return AsyncCalls.BeginStoreOffline(context, async, nameof(XStoreReportConsumableFulfillmentAsync));
    }

    /// <summary>HRESULT XStoreReportConsumableFulfillmentResult(XAsyncBlock*, XStoreConsumableResult*).</summary>
    [Shim(GdkModule.Name, "XStoreReportConsumableFulfillmentResult")]
    [UnmanagedCallersOnly]
    private static int XStoreReportConsumableFulfillmentResult(nint async, uint* consumable)
    {
        return AsyncCalls.Result(async, nameof(XStoreReportConsumableFulfillmentAsync), sizeof(uint), (nint)consumable);
    }

    /// <summary>HRESULT XStoreGetUserCollectionsIdAsync(XStoreContextHandle, const char* serviceTicket, const char* publisherUserId, XAsyncBlock*).</summary>
    [Shim(GdkModule.Name, nameof(XStoreGetUserCollectionsIdAsync))]
    [UnmanagedCallersOnly]
    private static int XStoreGetUserCollectionsIdAsync(nint context, byte* serviceTicket, byte* publisherUserId, nint async)
    {
        return AsyncCalls.BeginStoreOffline(context, async, nameof(XStoreGetUserCollectionsIdAsync));
    }

    /// <summary>HRESULT XStoreGetUserPurchaseIdAsync(XStoreContextHandle, const char* serviceTicket, const char* publisherUserId, XAsyncBlock*).</summary>
    [Shim(GdkModule.Name, nameof(XStoreGetUserPurchaseIdAsync))]
    [UnmanagedCallersOnly]
    private static int XStoreGetUserPurchaseIdAsync(nint context, byte* serviceTicket, byte* publisherUserId, nint async)
    {
        return AsyncCalls.BeginStoreOffline(context, async, nameof(XStoreGetUserPurchaseIdAsync));
    }

    /// <summary>HRESULT XStoreGetUser{Collections,Purchase}IdResultSize(XAsyncBlock*, size_t* size).</summary>
    [Shim(GdkModule.Name, "XStoreGetUserCollectionsIdResultSize")]
    [Shim(GdkModule.Name, "XStoreGetUserPurchaseIdResultSize")]
    [UnmanagedCallersOnly]
    private static int XStoreGetUserIdResultSize(nint async, nuint* size)
    {
        return AsyncCalls.EmptyCount(async, size);
    }

    /// <summary>HRESULT XStoreGetUserCollectionsIdResult(XAsyncBlock*, size_t size, char* result).</summary>
    [Shim(GdkModule.Name, "XStoreGetUserCollectionsIdResult")]
    [UnmanagedCallersOnly]
    private static int XStoreGetUserCollectionsIdResult(nint async, nuint size, byte* result)
    {
        return AsyncCalls.Result(async, nameof(XStoreGetUserCollectionsIdAsync), size, (nint)result);
    }

    /// <summary>HRESULT XStoreGetUserPurchaseIdResult(XAsyncBlock*, size_t size, char* result).</summary>
    [Shim(GdkModule.Name, "XStoreGetUserPurchaseIdResult")]
    [UnmanagedCallersOnly]
    private static int XStoreGetUserPurchaseIdResult(nint async, nuint size, byte* result)
    {
        return AsyncCalls.Result(async, nameof(XStoreGetUserPurchaseIdAsync), size, (nint)result);
    }

    private static int BeginCanceled(nint context, nint async, string api)
    {
        ShimContext.Runtime.Gdk.Boundary.Info($"{api}: no store UI offline; ends as canceled");

        return AsyncCalls.BeginStore(context, async, api, new OfflineOperation(HResult.Abort));
    }
}
