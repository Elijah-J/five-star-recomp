// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Runtime.InteropServices;

namespace FiveStar.Loader.Shims.Network;

/// <summary>
/// Winsock sealed: name lookups fail with WSAHOST_NOT_FOUND, connects and datagram sends with WSAENETUNREACH,
/// listening with WSAENETDOWN, and WSAIoctl (the way to ConnectEx and WSASendMsg) with WSAEOPNOTSUPP. Sockets can
/// still be created, bound and closed. The executable imports most of ws2_32.dll by ordinal, so the ordinals of
/// connect (4), listen (13), sendto (20), gethostbyaddr (51) and gethostbyname (52) are bound too.
/// </summary>
internal static unsafe partial class WinsockShim
{
    private const string Module = "ws2_32.dll";

    [Shim(Module, "getaddrinfo", SetsLastError = true)]
    [UnmanagedCallersOnly]
    private static int GetAddrInfo(byte* node, byte* service, nint hints, nint* result)
    {
        return NotFound("getaddrinfo", SocketAddressText.Lookup(SocketAddressText.Ansi(node), SocketAddressText.Ansi(service)), result);
    }

    [Shim(Module, "GetAddrInfoW", SetsLastError = true)]
    [UnmanagedCallersOnly]
    private static int GetAddrInfoW(char* node, char* service, nint hints, nint* result)
    {
        return NotFound("GetAddrInfoW", SocketAddressText.Lookup(SocketAddressText.Wide(node), SocketAddressText.Wide(service)), result);
    }

    /// <summary>GetAddrInfoExW(name, service, namespace, nspId, hints, result, timeout, overlapped, completion, handle).</summary>
    [Shim(Module, "GetAddrInfoExW", SetsLastError = true)]
    [UnmanagedCallersOnly]
    private static int GetAddrInfoExW(char* name, char* service, uint space, nint provider, nint hints, nint* result, nint timeout, nint overlapped, nint completion, nint handle)
    {
        return NotFound("GetAddrInfoExW", SocketAddressText.Lookup(SocketAddressText.Wide(name), SocketAddressText.Wide(service)), result);
    }

    [Shim(Module, "GetAddrInfoExA", SetsLastError = true)]
    [UnmanagedCallersOnly]
    private static int GetAddrInfoExA(byte* name, byte* service, uint space, nint provider, nint hints, nint* result, nint timeout, nint overlapped, nint completion, nint handle)
    {
        return NotFound("GetAddrInfoExA", SocketAddressText.Lookup(SocketAddressText.Ansi(name), SocketAddressText.Ansi(service)), result);
    }

    [Shim(Module, "gethostbyname", SetsLastError = true)]
    [Shim(Module, "#52")]
    [UnmanagedCallersOnly]
    private static nint GetHostByName(byte* name)
    {
        return Fail("gethostbyname", SocketAddressText.Ansi(name) ?? "(null)", NetworkErrors.HostNotFound);
    }

    [Shim(Module, "gethostbyaddr", SetsLastError = true)]
    [Shim(Module, "#51")]
    [UnmanagedCallersOnly]
    private static nint GetHostByAddress(byte* address, int length, int type)
    {
        return Fail("gethostbyaddr", "(address)", NetworkErrors.HostNotFound);
    }

    [Shim(Module, "connect", SetsLastError = true)]
    [Shim(Module, "#4")]
    [UnmanagedCallersOnly]
    private static int Connect(nint socket, byte* name, int length)
    {
        return (int)Fail("connect", SocketAddressText.Format(name, length), NetworkErrors.NetworkUnreachable, NetworkErrors.SocketError);
    }

    /// <summary>WSAConnect(s, name, namelen, callerData, calleeData, sqos, gqos).</summary>
    [Shim(Module, "WSAConnect", SetsLastError = true)]
    [UnmanagedCallersOnly]
    private static int WsaConnect(nint socket, byte* name, int length, nint callerData, nint calleeData, nint sqos, nint gqos)
    {
        return (int)Fail("WSAConnect", SocketAddressText.Format(name, length), NetworkErrors.NetworkUnreachable, NetworkErrors.SocketError);
    }

    /// <summary>BOOL WSAConnectByNameW(s, nodeName, serviceName, localLength*, local, remoteLength*, remote, timeout, reserved).</summary>
    [Shim(Module, "WSAConnectByNameW", SetsLastError = true)]
    [UnmanagedCallersOnly]
    private static int WsaConnectByNameW(nint socket, char* node, char* service, nint localLength, nint local, nint remoteLength, nint remote, nint timeout, nint reserved)
    {
        return (int)Fail("WSAConnectByNameW", SocketAddressText.Lookup(SocketAddressText.Wide(node), SocketAddressText.Wide(service)), NetworkErrors.HostNotFound);
    }

    [Shim(Module, "WSAConnectByNameA", SetsLastError = true)]
    [UnmanagedCallersOnly]
    private static int WsaConnectByNameA(nint socket, byte* node, byte* service, nint localLength, nint local, nint remoteLength, nint remote, nint timeout, nint reserved)
    {
        return (int)Fail("WSAConnectByNameA", SocketAddressText.Lookup(SocketAddressText.Ansi(node), SocketAddressText.Ansi(service)), NetworkErrors.HostNotFound);
    }

    /// <summary>BOOL WSAConnectByList(s, addressList, localLength*, local, remoteLength*, remote, timeout, reserved).</summary>
    [Shim(Module, "WSAConnectByList", SetsLastError = true)]
    [UnmanagedCallersOnly]
    private static int WsaConnectByList(nint socket, nint list, nint localLength, nint local, nint remoteLength, nint remote, nint timeout, nint reserved)
    {
        return (int)Fail("WSAConnectByList", "(address list)", NetworkErrors.NetworkUnreachable);
    }

    [Shim(Module, "sendto", SetsLastError = true)]
    [Shim(Module, "#20")]
    [UnmanagedCallersOnly]
    private static int SendTo(nint socket, nint buffer, int length, int flags, byte* to, int toLength)
    {
        return (int)Fail("sendto", SocketAddressText.Format(to, toLength), NetworkErrors.NetworkUnreachable, NetworkErrors.SocketError);
    }

    /// <summary>WSASendTo(s, buffers, count, sent*, flags, to, toLength, overlapped, completion).</summary>
    [Shim(Module, "WSASendTo", SetsLastError = true)]
    [UnmanagedCallersOnly]
    private static int WsaSendTo(nint socket, nint buffers, uint count, nint sent, uint flags, byte* to, int toLength, nint overlapped, nint completion)
    {
        return (int)Fail("WSASendTo", SocketAddressText.Format(to, toLength), NetworkErrors.NetworkUnreachable, NetworkErrors.SocketError);
    }

    [Shim(Module, "listen", SetsLastError = true)]
    [Shim(Module, "#13")]
    [UnmanagedCallersOnly]
    private static int Listen(nint socket, int backlog)
    {
        return (int)Fail("listen", $"socket 0x{socket:x}", NetworkErrors.NetworkDown, NetworkErrors.SocketError);
    }

    /// <summary>WSAIoctl(s, code, in, inLength, out, outLength, returned*, overlapped, completion).</summary>
    [Shim(Module, "WSAIoctl", SetsLastError = true)]
    [UnmanagedCallersOnly]
    private static int WsaIoctl(nint socket, uint code, nint input, uint inputLength, nint output, uint outputLength, nint returned, nint overlapped, nint completion)
    {
        return (int)Fail("WSAIoctl", $"code 0x{code:x8}", NetworkErrors.OperationNotSupported, NetworkErrors.SocketError);
    }

    /// <summary>A failed lookup: *result = NULL, the error returned and set as the last error.</summary>
    private static int NotFound(string api, string target, nint* result)
    {
        if (result != null)
        {
            *result = 0;
        }

        _ = Fail(api, target, NetworkErrors.HostNotFound);

        return NetworkErrors.HostNotFound;
    }

    private static nint Fail(string api, string target, int error, nint returnValue = 0)
    {
        ShimContext.Runtime.Network.Block(api, target);
        ShimContext.Runtime.LastError.Set(error);

        return returnValue;
    }
}
