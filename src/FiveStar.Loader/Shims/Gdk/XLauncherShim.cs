// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Runtime.InteropServices;

namespace FiveStar.Loader.Shims.Gdk;

/// <summary>XLauncher.h and XGame.h launch entry points; the work is in <see cref="LauncherService"/>.</summary>
internal static unsafe class XLauncherShim
{
    /// <summary>HRESULT XLaunchUri(XUserHandle requestingUser, const char* uri).</summary>
    [Shim(GdkModule.Name, "XLaunchUri")]
    [UnmanagedCallersOnly]
    private static int XLaunchUri(nint user, byte* uri)
    {
        var gdk = ShimContext.Runtime.Gdk;

        try
        {
            return gdk.Launcher.LaunchUri(Utf8Text.Read((nint)uri));
        }
        catch (Exception e)
        {
            return gdk.Boundary.Failed(nameof(XLaunchUri), e);
        }
    }

    /// <summary>HRESULT XLaunchRestartOnCrash(const char* args, uint32_t reserved).</summary>
    [Shim(GdkModule.Name, "XLaunchRestartOnCrash")]
    [UnmanagedCallersOnly]
    private static int XLaunchRestartOnCrash(byte* arguments, uint reserved)
    {
        return ShimContext.Runtime.Gdk.Launcher.RestartOnCrash(Utf8Text.Read((nint)arguments));
    }
}
