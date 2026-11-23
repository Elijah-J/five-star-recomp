// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Runtime.InteropServices;
using FiveStar.Loader.Native;

namespace FiveStar.Loader.Shims.Gdk;

/// <summary>XPackage.h entry points; the work is in <see cref="PackageService"/>.</summary>
internal static unsafe class XPackageShim
{
    [Shim(GdkModule.Name, "XPackageIsPackagedProcess")]
    [UnmanagedCallersOnly]
    private static byte XPackageIsPackagedProcess()
    {
        return GdkModule.True;
    }

    /// <summary>HRESULT XPackageGetCurrentProcessPackageIdentifier(size_t bufferSize, char* buffer).</summary>
    [Shim(GdkModule.Name, "XPackageGetCurrentProcessPackageIdentifier")]
    [UnmanagedCallersOnly]
    private static int XPackageGetCurrentProcessPackageIdentifier(nuint bufferSize, byte* buffer)
    {
        var gdk = ShimContext.Runtime.Gdk;

        try
        {
            return gdk.Packages.GetCurrentProcessPackageIdentifier(bufferSize, buffer);
        }
        catch (Exception e)
        {
            return gdk.Boundary.Failed(nameof(XPackageGetCurrentProcessPackageIdentifier), e);
        }
    }

    /// <summary>HRESULT XPackageGetUserLocale(size_t localeSize, char* locale).</summary>
    [Shim(GdkModule.Name, "XPackageGetUserLocale")]
    [UnmanagedCallersOnly]
    private static int XPackageGetUserLocale(nuint size, byte* locale)
    {
        return PackageService.GetUserLocale(size, locale);
    }

    /// <summary>HRESULT XPackageEnumeratePackages(XPackageKind, XPackageEnumerationScope, void* context, XPackageEnumerationCallback*).</summary>
    [Shim(GdkModule.Name, "XPackageEnumeratePackages")]
    [UnmanagedCallersOnly]
    private static int XPackageEnumeratePackages(uint kind, uint scope, nint context, nint callback)
    {
        var gdk = ShimContext.Runtime.Gdk;

        try
        {
            return gdk.Packages.EnumeratePackages(kind, context, callback);
        }
        catch (Exception e)
        {
            return gdk.Boundary.Failed(nameof(XPackageEnumeratePackages), e);
        }
    }

    /// <summary>HRESULT XPackageMount(const char* packageIdentifier, XPackageMountHandle* mount) (deprecated, still exported).</summary>
    [Shim(GdkModule.Name, "XPackageMount")]
    [UnmanagedCallersOnly]
    private static int XPackageMount(byte* id, nint* mount)
    {
        var gdk = ShimContext.Runtime.Gdk;

        if (mount == null)
        {
            return HResult.Pointer;
        }

        try
        {
            int hr = gdk.Packages.Mount(Utf8Text.Read((nint)id), out nint handle);
            *mount = handle;

            return hr;
        }
        catch (Exception e)
        {
            return gdk.Boundary.Failed(nameof(XPackageMount), e);
        }
    }

    /// <summary>HRESULT XPackageMountWithUiAsync(const char* packageIdentifier, XAsyncBlock* async).</summary>
    [Shim(GdkModule.Name, "XPackageMountWithUiAsync")]
    [UnmanagedCallersOnly]
    private static int XPackageMountWithUiAsync(byte* id, nint async)
    {
        var gdk = ShimContext.Runtime.Gdk;

        try
        {
            return gdk.Async.BeginManaged(async, PackageService.MountApi, new MountOperation(gdk.Packages, Utf8Text.Read((nint)id)));
        }
        catch (Exception e)
        {
            return gdk.Boundary.Failed(nameof(XPackageMountWithUiAsync), e);
        }
    }

    /// <summary>HRESULT XPackageMountWithUiResult(XAsyncBlock* async, XPackageMountHandle* mount).</summary>
    [Shim(GdkModule.Name, "XPackageMountWithUiResult")]
    [UnmanagedCallersOnly]
    private static int XPackageMountWithUiResult(nint async, nint* mount)
    {
        var gdk = ShimContext.Runtime.Gdk;

        if (mount == null)
        {
            return HResult.Pointer;
        }

        try
        {
            return gdk.Async.ManagedResult(async, PackageService.MountApi, (nuint)sizeof(nint), (nint)mount);
        }
        catch (Exception e)
        {
            return gdk.Boundary.Failed(nameof(XPackageMountWithUiResult), e);
        }
    }

    [Shim(GdkModule.Name, "XPackageGetMountPathSize")]
    [UnmanagedCallersOnly]
    private static int XPackageGetMountPathSize(nint mount, nuint* size)
    {
        return ShimContext.Runtime.Gdk.Packages.GetMountPathSize(mount, size);
    }

    [Shim(GdkModule.Name, "XPackageGetMountPath")]
    [UnmanagedCallersOnly]
    private static int XPackageGetMountPath(nint mount, nuint size, byte* path)
    {
        return ShimContext.Runtime.Gdk.Packages.GetMountPath(mount, size, path);
    }

    [Shim(GdkModule.Name, "XPackageCloseMountHandle")]
    [UnmanagedCallersOnly]
    private static void XPackageCloseMountHandle(nint mount)
    {
        ShimContext.Runtime.Gdk.Packages.CloseMount(mount);
    }

    /// <summary>HRESULT XPackageFindChunkAvailability(const char* packageIdentifier, uint32_t selectorCount, XPackageChunkSelector*, XPackageChunkAvailability*).</summary>
    [Shim(GdkModule.Name, "XPackageFindChunkAvailability")]
    [UnmanagedCallersOnly]
    private static int XPackageFindChunkAvailability(byte* id, uint selectorCount, nint selectors, uint* availability)
    {
        return ShimContext.Runtime.Gdk.Packages.FindChunkAvailability(id, availability);
    }

    /// <summary>HRESULT XPackageCreateInstallationMonitor(const char* id, uint32_t selectorCount, XPackageChunkSelector*, uint32_t minimumUpdateIntervalMs, XTaskQueueHandle, XPackageInstallationMonitorHandle*).</summary>
    [Shim(GdkModule.Name, "XPackageCreateInstallationMonitor")]
    [UnmanagedCallersOnly]
    private static int XPackageCreateInstallationMonitor(byte* id, uint selectorCount, nint selectors, uint interval, nint queue, nint* monitor)
    {
        var gdk = ShimContext.Runtime.Gdk;

        try
        {
            return gdk.Packages.CreateInstallationMonitor(id, monitor);
        }
        catch (Exception e)
        {
            return gdk.Boundary.Failed(nameof(XPackageCreateInstallationMonitor), e);
        }
    }

    /// <summary>void XPackageGetInstallationProgress(XPackageInstallationMonitorHandle, XPackageInstallationProgress*).</summary>
    [Shim(GdkModule.Name, "XPackageGetInstallationProgress")]
    [UnmanagedCallersOnly]
    private static void XPackageGetInstallationProgress(nint monitor, byte* progress)
    {
        ShimContext.Runtime.Gdk.Packages.GetInstallationProgress(monitor, progress);
    }

    [Shim(GdkModule.Name, "XPackageUpdateInstallationMonitor")]
    [UnmanagedCallersOnly]
    private static void XPackageUpdateInstallationMonitor(nint monitor)
    {
    }

    [Shim(GdkModule.Name, "XPackageCloseInstallationMonitorHandle")]
    [UnmanagedCallersOnly]
    private static void XPackageCloseInstallationMonitorHandle(nint monitor)
    {
        ShimContext.Runtime.Gdk.Packages.CloseMonitor(monitor);
    }

    /// <summary>HRESULT XPackageRegisterInstallationProgressChanged(monitor, void* context, XPackageInstallationProgressCallback*, XTaskQueueRegistrationToken*).</summary>
    [Shim(GdkModule.Name, "XPackageRegisterInstallationProgressChanged")]
    [UnmanagedCallersOnly]
    private static int XPackageRegisterInstallationProgressChanged(nint monitor, nint context, nint callback, ulong* token)
    {
        var runtime = ShimContext.Runtime;
        runtime.Log.Info($"  [gdk] XPackageRegisterInstallationProgressChanged(monitor 0x{monitor:x}, callback {runtime.Images.Describe(callback)}, context 0x{context:x}): never called back");

        return runtime.Gdk.Packages.RegisterProgressChanged(token);
    }

    /// <summary>bool XPackageUnregisterInstallationProgressChanged(monitor, XTaskQueueRegistrationToken token, bool wait).</summary>
    [Shim(GdkModule.Name, "XPackageUnregisterInstallationProgressChanged")]
    [UnmanagedCallersOnly]
    private static byte XPackageUnregisterInstallationProgressChanged(nint monitor, ulong token, byte wait)
    {
        return GdkModule.True;
    }

    /// <summary>HRESULT XPackageEnumerateFeatures(const char* packageIdentifier, void* context, XPackageFeatureEnumerationCallback*).</summary>
    [Shim(GdkModule.Name, "XPackageEnumerateFeatures")]
    [UnmanagedCallersOnly]
    private static int XPackageEnumerateFeatures(byte* id, nint context, nint callback)
    {
        return ShimContext.Runtime.Gdk.Packages.EnumerateFeatures(id, callback);
    }

    /// <summary>HRESULT XPackageEstimateDownloadSize(const char* id, uint32_t selectorCount, XPackageChunkSelector*, uint64_t* downloadSize, bool* shouldPresentUserConfirmation).</summary>
    [Shim(GdkModule.Name, "XPackageEstimateDownloadSize")]
    [UnmanagedCallersOnly]
    private static int XPackageEstimateDownloadSize(byte* id, uint selectorCount, nint selectors, ulong* size, byte* confirm)
    {
        return ShimContext.Runtime.Gdk.Packages.EstimateDownloadSize(id, size, confirm);
    }

    /// <summary>HRESULT XPackageInstallChunks(const char* id, uint32_t selectorCount, XPackageChunkSelector*, uint32_t minimumUpdateIntervalMs, bool suppressUserConfirmation, XTaskQueueHandle, XPackageInstallationMonitorHandle*).</summary>
    [Shim(GdkModule.Name, "XPackageInstallChunks")]
    [UnmanagedCallersOnly]
    private static int XPackageInstallChunks(byte* id, uint selectorCount, nint selectors, uint interval, byte suppressConfirmation, nint queue, nint* monitor)
    {
        var gdk = ShimContext.Runtime.Gdk;

        try
        {
            return gdk.Packages.CreateInstallationMonitor(id, monitor);
        }
        catch (Exception e)
        {
            return gdk.Boundary.Failed(nameof(XPackageInstallChunks), e);
        }
    }

    /// <summary>HRESULT XPackageInstallChunksAsync(const char* id, uint32_t selectorCount, XPackageChunkSelector*, uint32_t minimumUpdateIntervalMs, bool suppressUserConfirmation, XAsyncBlock*).</summary>
    [Shim(GdkModule.Name, nameof(XPackageInstallChunksAsync))]
    [UnmanagedCallersOnly]
    private static int XPackageInstallChunksAsync(byte* id, uint selectorCount, nint selectors, uint interval, byte suppressConfirmation, nint async)
    {
        var gdk = ShimContext.Runtime.Gdk;

        try
        {
            var operation = new InstallChunksOperation(gdk.Packages, Utf8Text.Read((nint)id));

            return gdk.Async.BeginManaged(async, nameof(XPackageInstallChunksAsync), operation);
        }
        catch (Exception e)
        {
            return gdk.Boundary.Failed(nameof(XPackageInstallChunksAsync), e);
        }
    }

    /// <summary>HRESULT XPackageInstallChunksResult(XAsyncBlock*, XPackageInstallationMonitorHandle*).</summary>
    [Shim(GdkModule.Name, "XPackageInstallChunksResult")]
    [UnmanagedCallersOnly]
    private static int XPackageInstallChunksResult(nint async, nint* monitor)
    {
        var gdk = ShimContext.Runtime.Gdk;

        if (monitor == null)
        {
            return HResult.Pointer;
        }

        try
        {
            return gdk.Async.ManagedResult(async, nameof(XPackageInstallChunksAsync), (nuint)sizeof(nint), (nint)monitor);
        }
        catch (Exception e)
        {
            return gdk.Boundary.Failed(nameof(XPackageInstallChunksResult), e);
        }
    }

    /// <summary>HRESULT XPackageChangeChunkInstallOrder(const char* id, uint32_t selectorCount, XPackageChunkSelector*).</summary>
    [Shim(GdkModule.Name, "XPackageChangeChunkInstallOrder")]
    [UnmanagedCallersOnly]
    private static int XPackageChangeChunkInstallOrder(byte* id, uint selectorCount, nint selectors)
    {
        return ShimContext.Runtime.Gdk.Packages.ChangeChunkInstallOrder(id);
    }

    /// <summary>HRESULT XPackageUninstallChunks(const char* id, uint32_t selectorCount, XPackageChunkSelector*): the loader never deletes game files.</summary>
    [Shim(GdkModule.Name, "XPackageUninstallChunks")]
    [UnmanagedCallersOnly]
    private static int XPackageUninstallChunks(byte* id, uint selectorCount, nint selectors)
    {
        ShimContext.Runtime.Gdk.Boundary.Info($"XPackageUninstallChunks({Utf8Text.Read((nint)id)}) not supported");

        return HResult.NotSupported;
    }

    /// <summary>bool XPackageUninstallPackage(const char* packageIdentifier): false, nothing is uninstalled.</summary>
    [Shim(GdkModule.Name, "XPackageUninstallPackage")]
    [UnmanagedCallersOnly]
    private static byte XPackageUninstallPackage(byte* id)
    {
        ShimContext.Runtime.Gdk.Boundary.Info($"XPackageUninstallPackage({Utf8Text.Read((nint)id)}) not supported");

        return GdkModule.False;
    }

    [Shim(GdkModule.Name, "XPackageGetWriteStats")]
    [UnmanagedCallersOnly]
    private static int XPackageGetWriteStats(byte* stats)
    {
        return PackageService.GetWriteStats(stats);
    }

    /// <summary>HRESULT XPackageRegisterPackageInstalled(XTaskQueueHandle, void* context, XPackageInstalledCallback*, XTaskQueueRegistrationToken*): no DLC ever installs.</summary>
    [Shim(GdkModule.Name, "XPackageRegisterPackageInstalled")]
    [UnmanagedCallersOnly]
    private static int XPackageRegisterPackageInstalled(nint queue, nint context, nint callback, ulong* token)
    {
        return ShimContext.Runtime.Gdk.Registrations.Register("XPackageRegisterPackageInstalled", callback, token);
    }

    [Shim(GdkModule.Name, "XPackageUnregisterPackageInstalled")]
    [UnmanagedCallersOnly]
    private static byte XPackageUnregisterPackageInstalled(ulong token, byte wait)
    {
        return ShimContext.Runtime.Gdk.Registrations.Unregister(token);
    }
}
