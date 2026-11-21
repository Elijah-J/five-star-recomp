// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Runtime.InteropServices;

namespace FiveStar.Loader.Shims.Gdk;

/// <summary>XAsync.h and XAsyncProvider.h entry points; the work is in <see cref="AsyncService"/>.</summary>
internal static unsafe class XAsyncShim
{
    [HotPath]
    [Shim(GdkModule.Name, "XAsyncBegin")]
    [UnmanagedCallersOnly]
    private static int XAsyncBegin(nint asyncBlock, nint context, nint identity, byte* identityName, nint provider)
    {
        var gdk = ShimContext.Runtime.Gdk;

        try
        {
            return gdk.Async.Begin(asyncBlock, context, identity, Utf8Text.Read((nint)identityName), provider);
        }
        catch (Exception e)
        {
            return gdk.Boundary.Failed(nameof(XAsyncBegin), e);
        }
    }

    [HotPath]
    [Shim(GdkModule.Name, "XAsyncSchedule")]
    [UnmanagedCallersOnly]
    private static int XAsyncSchedule(nint asyncBlock, uint delayInMs)
    {
        var gdk = ShimContext.Runtime.Gdk;

        try
        {
            return gdk.Async.Schedule(asyncBlock, delayInMs);
        }
        catch (Exception e)
        {
            return gdk.Boundary.Failed(nameof(XAsyncSchedule), e);
        }
    }

    [HotPath]
    [Shim(GdkModule.Name, "XAsyncComplete")]
    [UnmanagedCallersOnly]
    private static void XAsyncComplete(nint asyncBlock, int result, nuint requiredBufferSize)
    {
        var gdk = ShimContext.Runtime.Gdk;

        try
        {
            gdk.Async.Complete(asyncBlock, result, requiredBufferSize);
        }
        catch (Exception e)
        {
            gdk.Boundary.Report(nameof(XAsyncComplete), e);
        }
    }

    [HotPath]
    [Shim(GdkModule.Name, "XAsyncGetStatus")]
    [UnmanagedCallersOnly]
    private static int XAsyncGetStatus(nint asyncBlock, byte wait)
    {
        var gdk = ShimContext.Runtime.Gdk;

        try
        {
            return gdk.Async.GetStatus(asyncBlock, wait != 0);
        }
        catch (Exception e)
        {
            return gdk.Boundary.Failed(nameof(XAsyncGetStatus), e);
        }
    }

    [HotPath]
    [Shim(GdkModule.Name, "XAsyncGetResultSize")]
    [UnmanagedCallersOnly]
    private static int XAsyncGetResultSize(nint asyncBlock, nuint* bufferSize)
    {
        var gdk = ShimContext.Runtime.Gdk;

        try
        {
            return gdk.Async.GetResultSize(asyncBlock, bufferSize);
        }
        catch (Exception e)
        {
            return gdk.Boundary.Failed(nameof(XAsyncGetResultSize), e);
        }
    }

    [HotPath]
    [Shim(GdkModule.Name, "XAsyncGetResult")]
    [UnmanagedCallersOnly]
    private static int XAsyncGetResult(nint asyncBlock, nint identity, nuint bufferSize, nint buffer, nuint* bufferUsed)
    {
        var gdk = ShimContext.Runtime.Gdk;

        try
        {
            return gdk.Async.GetResult(asyncBlock, identity, bufferSize, buffer, bufferUsed);
        }
        catch (Exception e)
        {
            return gdk.Boundary.Failed(nameof(XAsyncGetResult), e);
        }
    }

    [HotPath]
    [Shim(GdkModule.Name, "XAsyncCancel")]
    [UnmanagedCallersOnly]
    private static void XAsyncCancel(nint asyncBlock)
    {
        var gdk = ShimContext.Runtime.Gdk;

        try
        {
            gdk.Async.Cancel(asyncBlock);
        }
        catch (Exception e)
        {
            gdk.Boundary.Report(nameof(XAsyncCancel), e);
        }
    }

    [HotPath]
    [Shim(GdkModule.Name, "XAsyncRun")]
    [UnmanagedCallersOnly]
    private static int XAsyncRun(nint asyncBlock, nint work)
    {
        var gdk = ShimContext.Runtime.Gdk;

        try
        {
            return gdk.Async.Run(asyncBlock, work);
        }
        catch (Exception e)
        {
            return gdk.Boundary.Failed(nameof(XAsyncRun), e);
        }
    }
}
