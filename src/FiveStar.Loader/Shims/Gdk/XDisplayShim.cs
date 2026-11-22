// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Runtime.InteropServices;

namespace FiveStar.Loader.Shims.Gdk;

/// <summary>XDisplay.h entry points; the work is in <see cref="DisplayService"/>.</summary>
internal static unsafe class XDisplayShim
{
    [Shim(GdkModule.Name, "XDisplayTryEnableHdrMode")]
    [UnmanagedCallersOnly]
    private static uint XDisplayTryEnableHdrMode(uint preference, float* info)
    {
        var gdk = ShimContext.Runtime.Gdk;

        try
        {
            return gdk.Display.TryEnableHdr(preference, info);
        }
        catch (Exception e)
        {
            gdk.Boundary.Report(nameof(XDisplayTryEnableHdrMode), e);

            return DisplayService.HdrDisabled;
        }
    }

    [Shim(GdkModule.Name, "XDisplayAcquireTimeoutDeferral")]
    [UnmanagedCallersOnly]
    private static int XDisplayAcquireTimeoutDeferral(nint* handle)
    {
        return ShimContext.Runtime.Gdk.Display.AcquireDeferral(handle);
    }

    [Shim(GdkModule.Name, "XDisplayCloseTimeoutDeferralHandle")]
    [UnmanagedCallersOnly]
    private static void XDisplayCloseTimeoutDeferralHandle(nint handle)
    {
        ShimContext.Runtime.Gdk.Display.CloseDeferral(handle);
    }
}
