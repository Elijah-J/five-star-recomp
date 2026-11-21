// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Runtime.InteropServices;
using FiveStar.Loader.Native;

namespace FiveStar.Loader.Shims.Gdk;

/// <summary>
/// XSystem.h, XThread.h, XGame.h, XGameEvent.h, XError.h and XPersistentLocalStorage.h entry points; the work is in
/// <see cref="SystemService"/>.
/// </summary>
internal static unsafe class XSystemShim
{
    [Shim(GdkModule.Name, "XSystemGetDeviceType")]
    [UnmanagedCallersOnly]
    private static uint XSystemGetDeviceType()
    {
        return ShimContext.Runtime.Gdk.System.DeviceType;
    }

    [Shim(GdkModule.Name, "XSystemGetAnalyticsInfo")]
    [UnmanagedCallersOnly]
    private static byte* XSystemGetAnalyticsInfo(byte* result)
    {
        return ShimContext.Runtime.Gdk.System.GetAnalyticsInfo(result);
    }

    /// <summary>HRESULT XSystemGetConsoleId(size_t consoleIdSize, char* consoleId, size_t* consoleIdUsed); XSystemConsoleIdBytes = 39.</summary>
    [Shim(GdkModule.Name, "XSystemGetConsoleId")]
    [UnmanagedCallersOnly]
    private static int XSystemGetConsoleId(nuint size, byte* id, nuint* used)
    {
        return SystemService.GetConsoleId(size, id, used);
    }

    [Shim(GdkModule.Name, "XSystemGetXboxLiveSandboxId")]
    [UnmanagedCallersOnly]
    private static int XSystemGetXboxLiveSandboxId(nuint size, byte* id, nuint* used)
    {
        return SystemService.GetSandboxId(size, id, used);
    }

    /// <summary>HRESULT XThreadSetTimeSensitive(bool isTimeSensitiveThread): per thread.</summary>
    [Shim(GdkModule.Name, "XThreadSetTimeSensitive")]
    [UnmanagedCallersOnly]
    private static int XThreadSetTimeSensitive(byte isTimeSensitive)
    {
        ShimContext.Runtime.Gdk.System.SetTimeSensitive(isTimeSensitive != 0);

        return HResult.Ok;
    }

    [Shim(GdkModule.Name, "XThreadIsTimeSensitive")]
    [UnmanagedCallersOnly]
    private static byte XThreadIsTimeSensitive()
    {
        bool sensitive = ShimContext.Runtime.Gdk.System.IsTimeSensitive();
        byte result = sensitive ? GdkModule.True : GdkModule.False;

        return result;
    }

    [Shim(GdkModule.Name, "XThreadAssertNotTimeSensitive")]
    [UnmanagedCallersOnly]
    private static void XThreadAssertNotTimeSensitive()
    {
        ShimContext.Runtime.Gdk.System.AssertNotTimeSensitive();
    }

    [Shim(GdkModule.Name, "XGameGetXboxTitleId")]
    [UnmanagedCallersOnly]
    private static int XGameGetXboxTitleId(uint* titleId)
    {
        return ShimContext.Runtime.Gdk.System.GetTitleId(titleId);
    }

    /// <summary>HRESULT XGameEventWrite(XUserHandle, const char* serviceConfigId, const char* playSessionId, const char* eventName, const char* dimensionsJson, const char* measurementsJson).</summary>
    [Shim(GdkModule.Name, "XGameEventWrite")]
    [UnmanagedCallersOnly]
    private static int XGameEventWrite(nint user, byte* serviceConfigId, byte* playSessionId, byte* eventName, byte* dimensions, byte* measurements)
    {
        return ShimContext.Runtime.Gdk.System.WriteEvent(eventName);
    }

    /// <summary>HRESULT XErrorSetCallback(XErrorCallback*, void* context): accepted, never called.</summary>
    [Shim(GdkModule.Name, "XErrorSetCallback")]
    [UnmanagedCallersOnly]
    private static int XErrorSetCallback(nint callback, nint context)
    {
        return HResult.Ok;
    }

    /// <summary>void XErrorSetOptions(XErrorOptions optionsDebuggerPresent, XErrorOptions optionsDebuggerNotPresent).</summary>
    [Shim(GdkModule.Name, "XErrorSetOptions")]
    [UnmanagedCallersOnly]
    private static void XErrorSetOptions(uint debuggerPresent, uint debuggerNotPresent)
    {
    }

    [Shim(GdkModule.Name, "XPersistentLocalStorageGetPathSize")]
    [UnmanagedCallersOnly]
    private static int XPersistentLocalStorageGetPathSize(nuint* size)
    {
        var gdk = ShimContext.Runtime.Gdk;

        try
        {
            return gdk.System.GetLocalStoragePathSize(size);
        }
        catch (Exception e)
        {
            return gdk.Boundary.Failed(nameof(XPersistentLocalStorageGetPathSize), e);
        }
    }

    [Shim(GdkModule.Name, "XPersistentLocalStorageGetPath")]
    [UnmanagedCallersOnly]
    private static int XPersistentLocalStorageGetPath(nuint size, byte* path, nuint* used)
    {
        var gdk = ShimContext.Runtime.Gdk;

        try
        {
            return gdk.System.GetLocalStoragePath(size, path, used);
        }
        catch (Exception e)
        {
            return gdk.Boundary.Failed(nameof(XPersistentLocalStorageGetPath), e);
        }
    }

    [Shim(GdkModule.Name, "XPersistentLocalStorageGetSpaceInfo")]
    [UnmanagedCallersOnly]
    private static int XPersistentLocalStorageGetSpaceInfo(ulong* info)
    {
        var gdk = ShimContext.Runtime.Gdk;

        try
        {
            return gdk.System.GetLocalStorageSpaceInfo(info);
        }
        catch (Exception e)
        {
            return gdk.Boundary.Failed(nameof(XPersistentLocalStorageGetSpaceInfo), e);
        }
    }
}
