// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Runtime.InteropServices;
using FiveStar.Loader.Native;

namespace FiveStar.Loader.Shims.Gdk;

/// <summary>
/// XGameSave.h and XGameSaveFiles.h entry points; the work is in <see cref="GameSaveService"/>. Callbacks are
/// <c>bool XGameSaveContainerInfoCallback(const XGameSaveContainerInfo*, void* context)</c> and
/// <c>bool XGameSaveBlobInfoCallback(const XGameSaveBlobInfo*, void* context)</c>.
/// </summary>
internal static unsafe class XGameSaveShim
{
    /// <summary>HRESULT XGameSaveInitializeProvider(XUserHandle, const char* configurationId, bool syncOnDemand, XGameSaveProviderHandle*).</summary>
    [Shim(GdkModule.Name, "XGameSaveInitializeProvider")]
    [UnmanagedCallersOnly]
    private static int XGameSaveInitializeProvider(nint user, byte* configId, byte syncOnDemand, nint* provider)
    {
        var gdk = ShimContext.Runtime.Gdk;

        if (provider == null)
        {
            return HResult.Pointer;
        }

        try
        {
            int hr = gdk.Saves.InitializeProvider(user, Utf8Text.Read((nint)configId), out nint created);
            *provider = created;

            return hr;
        }
        catch (Exception e)
        {
            return gdk.Boundary.Failed(nameof(XGameSaveInitializeProvider), e);
        }
    }

    /// <summary>HRESULT XGameSaveInitializeProviderAsync(XUserHandle, const char* configurationId, bool syncOnDemand, XAsyncBlock*).</summary>
    [Shim(GdkModule.Name, "XGameSaveInitializeProviderAsync")]
    [UnmanagedCallersOnly]
    private static int XGameSaveInitializeProviderAsync(nint user, byte* configId, byte syncOnDemand, nint async)
    {
        var gdk = ShimContext.Runtime.Gdk;

        try
        {
            return gdk.Saves.InitializeProviderAsync(user, Utf8Text.Read((nint)configId), async);
        }
        catch (Exception e)
        {
            return gdk.Boundary.Failed(nameof(XGameSaveInitializeProviderAsync), e);
        }
    }

    /// <summary>HRESULT XGameSaveInitializeProviderResult(XAsyncBlock*, XGameSaveProviderHandle*).</summary>
    [Shim(GdkModule.Name, "XGameSaveInitializeProviderResult")]
    [UnmanagedCallersOnly]
    private static int XGameSaveInitializeProviderResult(nint async, nint* provider)
    {
        return ManagedResult(nameof(XGameSaveInitializeProviderResult), async, GameSaveService.InitializeProviderApi, (nuint)sizeof(nint), provider);
    }

    [Shim(GdkModule.Name, "XGameSaveCloseProvider")]
    [UnmanagedCallersOnly]
    private static void XGameSaveCloseProvider(nint provider)
    {
        ShimContext.Runtime.Gdk.Saves.CloseProvider(provider);
    }

    /// <summary>HRESULT XGameSaveGetRemainingQuota(XGameSaveProviderHandle, int64_t* remainingQuota).</summary>
    [Shim(GdkModule.Name, "XGameSaveGetRemainingQuota")]
    [UnmanagedCallersOnly]
    private static int XGameSaveGetRemainingQuota(nint provider, long* remaining)
    {
        var gdk = ShimContext.Runtime.Gdk;

        try
        {
            return gdk.Saves.GetRemainingQuota(provider, remaining);
        }
        catch (Exception e)
        {
            return gdk.Boundary.Failed(nameof(XGameSaveGetRemainingQuota), e);
        }
    }

    [Shim(GdkModule.Name, "XGameSaveGetRemainingQuotaAsync")]
    [UnmanagedCallersOnly]
    private static int XGameSaveGetRemainingQuotaAsync(nint provider, nint async)
    {
        var gdk = ShimContext.Runtime.Gdk;

        try
        {
            return gdk.Saves.GetRemainingQuotaAsync(provider, async);
        }
        catch (Exception e)
        {
            return gdk.Boundary.Failed(nameof(XGameSaveGetRemainingQuotaAsync), e);
        }
    }

    [Shim(GdkModule.Name, "XGameSaveGetRemainingQuotaResult")]
    [UnmanagedCallersOnly]
    private static int XGameSaveGetRemainingQuotaResult(nint async, long* remaining)
    {
        return ManagedResult(nameof(XGameSaveGetRemainingQuotaResult), async, GameSaveService.RemainingQuotaApi, sizeof(long), remaining);
    }

    /// <summary>HRESULT XGameSaveDeleteContainer(XGameSaveProviderHandle, const char* containerName).</summary>
    [Shim(GdkModule.Name, "XGameSaveDeleteContainer")]
    [UnmanagedCallersOnly]
    private static int XGameSaveDeleteContainer(nint provider, byte* name)
    {
        var gdk = ShimContext.Runtime.Gdk;

        try
        {
            return gdk.Saves.DeleteContainer(provider, name);
        }
        catch (Exception e)
        {
            return gdk.Boundary.Failed(nameof(XGameSaveDeleteContainer), e);
        }
    }

    [Shim(GdkModule.Name, "XGameSaveDeleteContainerAsync")]
    [UnmanagedCallersOnly]
    private static int XGameSaveDeleteContainerAsync(nint provider, byte* name, nint async)
    {
        var gdk = ShimContext.Runtime.Gdk;

        try
        {
            return gdk.Saves.DeleteContainerAsync(provider, name, async);
        }
        catch (Exception e)
        {
            return gdk.Boundary.Failed(nameof(XGameSaveDeleteContainerAsync), e);
        }
    }

    [Shim(GdkModule.Name, "XGameSaveDeleteContainerResult")]
    [UnmanagedCallersOnly]
    private static int XGameSaveDeleteContainerResult(nint async)
    {
        return ManagedResult(nameof(XGameSaveDeleteContainerResult), async, GameSaveService.DeleteContainerApi, 0, (void*)null);
    }

    /// <summary>HRESULT XGameSaveCreateContainer(XGameSaveProviderHandle, const char* containerName, XGameSaveContainerHandle*).</summary>
    [Shim(GdkModule.Name, "XGameSaveCreateContainer")]
    [UnmanagedCallersOnly]
    private static int XGameSaveCreateContainer(nint provider, byte* name, nint* container)
    {
        var gdk = ShimContext.Runtime.Gdk;

        try
        {
            return gdk.Saves.CreateContainer(provider, name, container);
        }
        catch (Exception e)
        {
            return gdk.Boundary.Failed(nameof(XGameSaveCreateContainer), e);
        }
    }

    [Shim(GdkModule.Name, "XGameSaveCloseContainer")]
    [UnmanagedCallersOnly]
    private static void XGameSaveCloseContainer(nint container)
    {
        ShimContext.Runtime.Gdk.Saves.CloseContainer(container);
    }

    /// <summary>HRESULT XGameSaveGetContainerInfo(XGameSaveProviderHandle, const char* containerName, void* context, XGameSaveContainerInfoCallback*).</summary>
    [Shim(GdkModule.Name, "XGameSaveGetContainerInfo")]
    [UnmanagedCallersOnly]
    private static int XGameSaveGetContainerInfo(nint provider, byte* name, nint context, nint callback)
    {
        var gdk = ShimContext.Runtime.Gdk;

        try
        {
            return gdk.Saves.GetContainerInfo(provider, name, context, callback);
        }
        catch (Exception e)
        {
            return gdk.Boundary.Failed(nameof(XGameSaveGetContainerInfo), e);
        }
    }

    [Shim(GdkModule.Name, "XGameSaveEnumerateContainerInfo")]
    [UnmanagedCallersOnly]
    private static int XGameSaveEnumerateContainerInfo(nint provider, nint context, nint callback)
    {
        var gdk = ShimContext.Runtime.Gdk;

        try
        {
            return gdk.Saves.EnumerateContainers(provider, null, context, callback);
        }
        catch (Exception e)
        {
            return gdk.Boundary.Failed(nameof(XGameSaveEnumerateContainerInfo), e);
        }
    }

    [Shim(GdkModule.Name, "XGameSaveEnumerateContainerInfoByName")]
    [UnmanagedCallersOnly]
    private static int XGameSaveEnumerateContainerInfoByName(nint provider, byte* prefix, nint context, nint callback)
    {
        var gdk = ShimContext.Runtime.Gdk;

        try
        {
            return gdk.Saves.EnumerateContainers(provider, Utf8Text.Read((nint)prefix), context, callback);
        }
        catch (Exception e)
        {
            return gdk.Boundary.Failed(nameof(XGameSaveEnumerateContainerInfoByName), e);
        }
    }

    [Shim(GdkModule.Name, "XGameSaveEnumerateBlobInfo")]
    [UnmanagedCallersOnly]
    private static int XGameSaveEnumerateBlobInfo(nint container, nint context, nint callback)
    {
        var gdk = ShimContext.Runtime.Gdk;

        try
        {
            return gdk.Saves.EnumerateBlobs(container, null, context, callback);
        }
        catch (Exception e)
        {
            return gdk.Boundary.Failed(nameof(XGameSaveEnumerateBlobInfo), e);
        }
    }

    [Shim(GdkModule.Name, "XGameSaveEnumerateBlobInfoByName")]
    [UnmanagedCallersOnly]
    private static int XGameSaveEnumerateBlobInfoByName(nint container, byte* prefix, nint context, nint callback)
    {
        var gdk = ShimContext.Runtime.Gdk;

        try
        {
            return gdk.Saves.EnumerateBlobs(container, Utf8Text.Read((nint)prefix), context, callback);
        }
        catch (Exception e)
        {
            return gdk.Boundary.Failed(nameof(XGameSaveEnumerateBlobInfoByName), e);
        }
    }

    /// <summary>HRESULT XGameSaveReadBlobData(XGameSaveContainerHandle, const char** blobNames, uint32_t* countOfBlobs, size_t blobsSize, XGameSaveBlob* blobData).</summary>
    [Shim(GdkModule.Name, "XGameSaveReadBlobData")]
    [UnmanagedCallersOnly]
    private static int XGameSaveReadBlobData(nint container, byte** blobNames, uint* countOfBlobs, nuint blobsSize, byte* blobData)
    {
        var gdk = ShimContext.Runtime.Gdk;

        try
        {
            return gdk.Saves.ReadBlobData(container, blobNames, countOfBlobs, blobsSize, blobData);
        }
        catch (Exception e)
        {
            return gdk.Boundary.Failed(nameof(XGameSaveReadBlobData), e);
        }
    }

    /// <summary>HRESULT XGameSaveReadBlobDataAsync(XGameSaveContainerHandle, const char** blobNames, uint32_t countOfBlobs, XAsyncBlock*).</summary>
    [Shim(GdkModule.Name, "XGameSaveReadBlobDataAsync")]
    [UnmanagedCallersOnly]
    private static int XGameSaveReadBlobDataAsync(nint container, byte** blobNames, uint countOfBlobs, nint async)
    {
        var gdk = ShimContext.Runtime.Gdk;

        try
        {
            return gdk.Saves.ReadBlobDataAsync(container, blobNames, countOfBlobs, async);
        }
        catch (Exception e)
        {
            return gdk.Boundary.Failed(nameof(XGameSaveReadBlobDataAsync), e);
        }
    }

    /// <summary>HRESULT XGameSaveReadBlobDataResult(XAsyncBlock*, size_t blobsSize, XGameSaveBlob* blobData, uint32_t* countOfBlobs).</summary>
    [Shim(GdkModule.Name, "XGameSaveReadBlobDataResult")]
    [UnmanagedCallersOnly]
    private static int XGameSaveReadBlobDataResult(nint async, nuint blobsSize, byte* blobData, uint* countOfBlobs)
    {
        var gdk = ShimContext.Runtime.Gdk;

        try
        {
            return gdk.Saves.ReadBlobDataResult(async, blobsSize, blobData, countOfBlobs);
        }
        catch (Exception e)
        {
            return gdk.Boundary.Failed(nameof(XGameSaveReadBlobDataResult), e);
        }
    }

    /// <summary>HRESULT XGameSaveCreateUpdate(XGameSaveContainerHandle, const char* containerDisplayName, XGameSaveUpdateHandle*).</summary>
    [Shim(GdkModule.Name, "XGameSaveCreateUpdate")]
    [UnmanagedCallersOnly]
    private static int XGameSaveCreateUpdate(nint container, byte* displayName, nint* update)
    {
        var gdk = ShimContext.Runtime.Gdk;

        try
        {
            return gdk.Saves.CreateUpdate(container, displayName, update);
        }
        catch (Exception e)
        {
            return gdk.Boundary.Failed(nameof(XGameSaveCreateUpdate), e);
        }
    }

    /// <summary>HRESULT XGameSaveSubmitBlobWrite(XGameSaveUpdateHandle, const char* blobName, const uint8_t* data, size_t byteCount).</summary>
    [Shim(GdkModule.Name, "XGameSaveSubmitBlobWrite")]
    [UnmanagedCallersOnly]
    private static int XGameSaveSubmitBlobWrite(nint update, byte* blobName, byte* data, nuint byteCount)
    {
        var gdk = ShimContext.Runtime.Gdk;

        try
        {
            return gdk.Saves.SubmitBlobWrite(update, blobName, data, byteCount);
        }
        catch (Exception e)
        {
            return gdk.Boundary.Failed(nameof(XGameSaveSubmitBlobWrite), e);
        }
    }

    [Shim(GdkModule.Name, "XGameSaveSubmitBlobDelete")]
    [UnmanagedCallersOnly]
    private static int XGameSaveSubmitBlobDelete(nint update, byte* blobName)
    {
        var gdk = ShimContext.Runtime.Gdk;

        try
        {
            return gdk.Saves.SubmitBlobDelete(update, blobName);
        }
        catch (Exception e)
        {
            return gdk.Boundary.Failed(nameof(XGameSaveSubmitBlobDelete), e);
        }
    }

    [Shim(GdkModule.Name, "XGameSaveSubmitUpdate")]
    [UnmanagedCallersOnly]
    private static int XGameSaveSubmitUpdate(nint update)
    {
        var gdk = ShimContext.Runtime.Gdk;

        try
        {
            return gdk.Saves.SubmitUpdate(update);
        }
        catch (Exception e)
        {
            return gdk.Boundary.Failed(nameof(XGameSaveSubmitUpdate), e);
        }
    }

    [Shim(GdkModule.Name, "XGameSaveSubmitUpdateAsync")]
    [UnmanagedCallersOnly]
    private static int XGameSaveSubmitUpdateAsync(nint update, nint async)
    {
        var gdk = ShimContext.Runtime.Gdk;

        try
        {
            return gdk.Saves.SubmitUpdateAsync(update, async);
        }
        catch (Exception e)
        {
            return gdk.Boundary.Failed(nameof(XGameSaveSubmitUpdateAsync), e);
        }
    }

    [Shim(GdkModule.Name, "XGameSaveSubmitUpdateResult")]
    [UnmanagedCallersOnly]
    private static int XGameSaveSubmitUpdateResult(nint async)
    {
        return ManagedResult(nameof(XGameSaveSubmitUpdateResult), async, GameSaveService.SubmitUpdateApi, 0, (void*)null);
    }

    [Shim(GdkModule.Name, "XGameSaveCloseUpdate")]
    [UnmanagedCallersOnly]
    private static void XGameSaveCloseUpdate(nint update)
    {
        ShimContext.Runtime.Gdk.Saves.CloseUpdate(update);
    }

    /// <summary>HRESULT XGameSaveFilesGetFolderWithUiAsync(XUserHandle, const char* configurationId, XAsyncBlock*).</summary>
    [Shim(GdkModule.Name, "XGameSaveFilesGetFolderWithUiAsync")]
    [UnmanagedCallersOnly]
    private static int XGameSaveFilesGetFolderWithUiAsync(nint user, byte* configId, nint async)
    {
        var gdk = ShimContext.Runtime.Gdk;

        try
        {
            return gdk.Saves.GetFolderWithUiAsync(user, configId, async);
        }
        catch (Exception e)
        {
            return gdk.Boundary.Failed(nameof(XGameSaveFilesGetFolderWithUiAsync), e);
        }
    }

    /// <summary>HRESULT XGameSaveFilesGetFolderWithUiResult(XAsyncBlock*, size_t folderSize, char* folderResult); callers pass MAX_PATH.</summary>
    [Shim(GdkModule.Name, "XGameSaveFilesGetFolderWithUiResult")]
    [UnmanagedCallersOnly]
    private static int XGameSaveFilesGetFolderWithUiResult(nint async, nuint folderSize, byte* folder)
    {
        var gdk = ShimContext.Runtime.Gdk;

        try
        {
            return gdk.Async.ManagedResult(async, GameSaveService.SaveFolderApi, folderSize, (nint)folder);
        }
        catch (Exception e)
        {
            return gdk.Boundary.Failed(nameof(XGameSaveFilesGetFolderWithUiResult), e);
        }
    }

    /// <summary>The *Result half of an async call whose result is a fixed-size value; a null destination is E_POINTER when one is expected.</summary>
    private static int ManagedResult(string api, nint async, string asyncApi, nuint size, void* destination)
    {
        var gdk = ShimContext.Runtime.Gdk;

        if (size != 0 && destination == null)
        {
            return HResult.Pointer;
        }

        try
        {
            return gdk.Async.ManagedResult(async, asyncApi, size, (nint)destination);
        }
        catch (Exception e)
        {
            return gdk.Boundary.Failed(api, e);
        }
    }
}
