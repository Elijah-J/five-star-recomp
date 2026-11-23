// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Runtime.InteropServices;
using FiveStar.Loader.Native;

namespace FiveStar.Loader.Shims.Gdk;

/// <summary>
/// XStore.h product queries: the offline user owns no add-ons and the catalog is unreachable, so every query returns
/// an empty, single-page XStoreProductQueryHandle.
/// </summary>
internal static unsafe partial class XStoreProductShim
{
    /// <summary>HRESULT XStoreQueryAssociatedProductsAsync(XStoreContextHandle, XStoreProductKind, uint32_t maxItemsToRetrievePerPage, XAsyncBlock*).</summary>
    [Shim(GdkModule.Name, nameof(XStoreQueryAssociatedProductsAsync))]
    [UnmanagedCallersOnly]
    private static int XStoreQueryAssociatedProductsAsync(nint context, uint kinds, uint pageSize, nint async)
    {
        return BeginQuery(context, async, nameof(XStoreQueryAssociatedProductsAsync));
    }

    /// <summary>HRESULT XStoreQueryAssociatedProductsResult(XAsyncBlock*, XStoreProductQueryHandle*).</summary>
    [Shim(GdkModule.Name, "XStoreQueryAssociatedProductsResult")]
    [UnmanagedCallersOnly]
    private static int XStoreQueryAssociatedProductsResult(nint async, nint* query)
    {
        return AsyncCalls.HandleResult(async, nameof(XStoreQueryAssociatedProductsAsync), query);
    }

    /// <summary>HRESULT XStoreQueryEntitledProductsAsync(XStoreContextHandle, XStoreProductKind, uint32_t maxItemsToRetrievePerPage, XAsyncBlock*).</summary>
    [Shim(GdkModule.Name, nameof(XStoreQueryEntitledProductsAsync))]
    [UnmanagedCallersOnly]
    private static int XStoreQueryEntitledProductsAsync(nint context, uint kinds, uint pageSize, nint async)
    {
        return BeginQuery(context, async, nameof(XStoreQueryEntitledProductsAsync));
    }

    /// <summary>HRESULT XStoreQueryEntitledProductsResult(XAsyncBlock*, XStoreProductQueryHandle*).</summary>
    [Shim(GdkModule.Name, "XStoreQueryEntitledProductsResult")]
    [UnmanagedCallersOnly]
    private static int XStoreQueryEntitledProductsResult(nint async, nint* query)
    {
        return AsyncCalls.HandleResult(async, nameof(XStoreQueryEntitledProductsAsync), query);
    }

    /// <summary>HRESULT XStoreEnumerateProductsQuery(XStoreProductQueryHandle, void* context, XStoreProductQueryCallback*): no products, so no callback.</summary>
    [Shim(GdkModule.Name, "XStoreEnumerateProductsQuery")]
    [UnmanagedCallersOnly]
    private static int XStoreEnumerateProductsQuery(nint query, nint context, nint callback)
    {
        if (callback == 0 || !ShimContext.Runtime.Gdk.Store.IsProductQuery(query))
        {
            return HResult.InvalidArgument;
        }

        return HResult.Ok;
    }

    [Shim(GdkModule.Name, "XStoreProductsQueryHasMorePages")]
    [UnmanagedCallersOnly]
    private static byte XStoreProductsQueryHasMorePages(nint query)
    {
        return GdkModule.False;
    }

    /// <summary>HRESULT XStoreProductsQueryNextPageAsync(XStoreProductQueryHandle, XAsyncBlock*): there is no next page to fetch.</summary>
    [Shim(GdkModule.Name, nameof(XStoreProductsQueryNextPageAsync))]
    [UnmanagedCallersOnly]
    private static int XStoreProductsQueryNextPageAsync(nint query, nint async)
    {
        if (!ShimContext.Runtime.Gdk.Store.IsProductQuery(query))
        {
            return HResult.InvalidArgument;
        }

        return AsyncCalls.Begin(async, nameof(XStoreProductsQueryNextPageAsync), new OfflineOperation(StoreService.NetworkError));
    }

    /// <summary>HRESULT XStoreProductsQueryNextPageResult(XAsyncBlock*, XStoreProductQueryHandle*).</summary>
    [Shim(GdkModule.Name, "XStoreProductsQueryNextPageResult")]
    [UnmanagedCallersOnly]
    private static int XStoreProductsQueryNextPageResult(nint async, nint* query)
    {
        return AsyncCalls.HandleResult(async, nameof(XStoreProductsQueryNextPageAsync), query);
    }

    [Shim(GdkModule.Name, "XStoreCloseProductsQueryHandle")]
    [UnmanagedCallersOnly]
    private static void XStoreCloseProductsQueryHandle(nint query)
    {
        ShimContext.Runtime.Gdk.Store.CloseProductQuery(query);
    }

    private static int BeginQuery(nint context, nint async, string api)
    {
        return AsyncCalls.BeginStore(context, async, api, new ProductQueryOperation(ShimContext.Runtime.Gdk.Store));
    }
}
