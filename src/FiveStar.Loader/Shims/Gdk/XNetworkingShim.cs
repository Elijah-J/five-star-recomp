// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Runtime.InteropServices;
using FiveStar.Loader.Native;

namespace FiveStar.Loader.Shims.Gdk;

/// <summary>
/// XNetworking.h entry points with offline answers (<see cref="NetworkingService"/>). Change notifications register
/// and never fire. URL security queries and certificate checks fail with HRESULT_FROM_WIN32(ERROR_NETWORK_UNREACHABLE).
/// </summary>
internal static unsafe partial class XNetworkingShim
{
    [Shim(GdkModule.Name, "XNetworkingGetConnectivityHint")]
    [UnmanagedCallersOnly]
    private static int XNetworkingGetConnectivityHint(byte* hint)
    {
        return NetworkingService.GetConnectivityHint(hint);
    }

    /// <summary>HRESULT XNetworkingRegisterConnectivityHintChanged(XTaskQueueHandle, void* context, callback, XTaskQueueRegistrationToken*).</summary>
    [Shim(GdkModule.Name, "XNetworkingRegisterConnectivityHintChanged")]
    [UnmanagedCallersOnly]
    private static int XNetworkingRegisterConnectivityHintChanged(nint queue, nint context, nint callback, ulong* token)
    {
        return ShimContext.Runtime.Gdk.Registrations.Register("XNetworkingRegisterConnectivityHintChanged", callback, token);
    }

    [Shim(GdkModule.Name, "XNetworkingUnregisterConnectivityHintChanged")]
    [Shim(GdkModule.Name, "XNetworkingUnregisterPreferredLocalUdpMultiplayerPortChanged")]
    [UnmanagedCallersOnly]
    private static byte XNetworkingUnregister(ulong token, byte wait)
    {
        return ShimContext.Runtime.Gdk.Registrations.Unregister(token);
    }

    [Shim(GdkModule.Name, "XNetworkingQueryPreferredLocalUdpMultiplayerPort")]
    [UnmanagedCallersOnly]
    private static int XNetworkingQueryPreferredLocalUdpMultiplayerPort(ushort* port)
    {
        return NetworkingService.QueryPreferredPort(port);
    }

    [Shim(GdkModule.Name, NetworkingService.PortApi)]
    [UnmanagedCallersOnly]
    private static int XNetworkingQueryPreferredLocalUdpMultiplayerPortAsync(nint async)
    {
        var gdk = ShimContext.Runtime.Gdk;

        try
        {
            return gdk.Async.BeginManaged(async, NetworkingService.PortApi, new PortQueryOperation());
        }
        catch (Exception e)
        {
            return gdk.Boundary.Failed(NetworkingService.PortApi, e);
        }
    }

    [Shim(GdkModule.Name, "XNetworkingQueryPreferredLocalUdpMultiplayerPortAsyncResult")]
    [UnmanagedCallersOnly]
    private static int XNetworkingQueryPreferredLocalUdpMultiplayerPortAsyncResult(nint async, ushort* port)
    {
        var gdk = ShimContext.Runtime.Gdk;

        if (port == null)
        {
            return HResult.Pointer;
        }

        try
        {
            return gdk.Async.ManagedResult(async, NetworkingService.PortApi, sizeof(ushort), (nint)port);
        }
        catch (Exception e)
        {
            return gdk.Boundary.Failed(nameof(XNetworkingQueryPreferredLocalUdpMultiplayerPortAsyncResult), e);
        }
    }

    [Shim(GdkModule.Name, "XNetworkingRegisterPreferredLocalUdpMultiplayerPortChanged")]
    [UnmanagedCallersOnly]
    private static int XNetworkingRegisterPreferredLocalUdpMultiplayerPortChanged(nint queue, nint context, nint callback, ulong* token)
    {
        return ShimContext.Runtime.Gdk.Registrations.Register("XNetworkingRegisterPreferredLocalUdpMultiplayerPortChanged", callback, token);
    }

    /// <summary>HRESULT XNetworkingQuerySecurityInformationForUrlAsync(const char* url, XAsyncBlock*): the URL's server cannot be reached.</summary>
    [Shim(GdkModule.Name, NetworkingService.SecurityInformationApi)]
    [UnmanagedCallersOnly]
    private static int XNetworkingQuerySecurityInformationForUrlAsync(byte* url, nint async)
    {
        if (url == null)
        {
            return HResult.InvalidArgument;
        }

        return AsyncCalls.Begin(async, NetworkingService.SecurityInformationApi, new OfflineOperation(HResult.NetworkUnreachable));
    }

    /// <summary>HRESULT XNetworkingQuerySecurityInformationForUrlAsyncResultSize(XAsyncBlock*, size_t* securityInformationBufferByteCount).</summary>
    [Shim(GdkModule.Name, "XNetworkingQuerySecurityInformationForUrlAsyncResultSize")]
    [UnmanagedCallersOnly]
    private static int XNetworkingQuerySecurityInformationForUrlAsyncResultSize(nint async, nuint* size)
    {
        return AsyncCalls.EmptyCount(async, size);
    }

    /// <summary>
    /// HRESULT XNetworkingQuerySecurityInformationForUrlAsyncResult(XAsyncBlock*, size_t bufferByteCount, size_t*
    /// bufferByteCountUsed, uint8_t* buffer, XNetworkingSecurityInformation** securityInformation).
    /// </summary>
    [Shim(GdkModule.Name, "XNetworkingQuerySecurityInformationForUrlAsyncResult")]
    [UnmanagedCallersOnly]
    private static int XNetworkingQuerySecurityInformationForUrlAsyncResult(nint async, nuint size, nuint* used, nint buffer, nint* information)
    {
        if (used != null)
        {
            *used = 0;
        }

        if (information != null)
        {
            *information = 0;
        }

        return AsyncCalls.Result(async, NetworkingService.SecurityInformationApi, size, buffer);
    }

    /// <summary>
    /// HRESULT XNetworkingVerifyServerCertificate(void* requestHandle, const XNetworkingSecurityInformation*): the
    /// game's HTTP stack hands its own request handle, and no request can be live offline, so the check fails closed.
    /// </summary>
    [Shim(GdkModule.Name, "XNetworkingVerifyServerCertificate")]
    [UnmanagedCallersOnly]
    private static int XNetworkingVerifyServerCertificate(nint request, nint securityInformation)
    {
        if (request == 0 || securityInformation == 0)
        {
            return HResult.InvalidArgument;
        }

        return HResult.NetworkUnreachable;
    }
}
