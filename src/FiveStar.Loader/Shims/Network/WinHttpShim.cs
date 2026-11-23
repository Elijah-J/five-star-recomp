// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using System.Runtime.InteropServices;

namespace FiveStar.Loader.Shims.Network;

/// <summary>
/// WinHTTP sealed: <c>WinHttpConnect</c> fails as if the server name did not resolve, so no connection handle ever
/// exists, and every call that would move data fails with ERROR_WINHTTP_CANNOT_CONNECT. The game, libHttpClient and
/// XCurl import WinHTTP directly; session, option, header and URL functions stay with the system DLL (no traffic).
/// </summary>
internal static unsafe partial class WinHttpShim
{
    private const string Module = "winhttp.dll";

    /// <summary>HINTERNET WinHttpConnect(HINTERNET session, LPCWSTR serverName, INTERNET_PORT port, DWORD reserved).</summary>
    [Shim(Module, "WinHttpConnect", SetsLastError = true)]
    [UnmanagedCallersOnly]
    private static nint WinHttpConnect(nint session, char* server, ushort port, uint reserved)
    {
        string host = server == null ? "(null)" : new string(server);
        ShimContext.Runtime.Network.Block("WinHttpConnect", $"{host}:{port.ToString(CultureInfo.InvariantCulture)}");
        ShimContext.Runtime.LastError.Set(NetworkErrors.NameNotResolved);

        return 0;
    }

    /// <summary>WinHttpOpenRequest(connect, verb, objectName, version, referrer, acceptTypes, flags).</summary>
    [Shim(Module, "WinHttpOpenRequest", SetsLastError = true)]
    [UnmanagedCallersOnly]
    private static nint WinHttpOpenRequest(nint connect, char* verb, char* objectName, char* version, char* referrer, nint acceptTypes, uint flags)
    {
        string path = objectName == null ? "/" : new string(objectName);

        return Refuse("WinHttpOpenRequest", path);
    }

    /// <summary>WinHttpSendRequest(request, headers, headersLength, optional, optionalLength, totalLength, context).</summary>
    [Shim(Module, "WinHttpSendRequest", SetsLastError = true)]
    [UnmanagedCallersOnly]
    private static int WinHttpSendRequest(nint request, char* headers, uint headersLength, nint optional, uint optionalLength, uint totalLength, nint context)
    {
        return (int)Refuse("WinHttpSendRequest", $"0x{request:x}");
    }

    [Shim(Module, "WinHttpReceiveResponse", SetsLastError = true)]
    [UnmanagedCallersOnly]
    private static int WinHttpReceiveResponse(nint request, nint reserved)
    {
        return (int)Refuse("WinHttpReceiveResponse", $"0x{request:x}");
    }

    [Shim(Module, "WinHttpReadData", SetsLastError = true)]
    [UnmanagedCallersOnly]
    private static int WinHttpReadData(nint request, nint buffer, uint length, uint* read)
    {
        return (int)Refuse("WinHttpReadData", $"0x{request:x}");
    }

    [Shim(Module, "WinHttpWriteData", SetsLastError = true)]
    [UnmanagedCallersOnly]
    private static int WinHttpWriteData(nint request, nint buffer, uint length, uint* written)
    {
        return (int)Refuse("WinHttpWriteData", $"0x{request:x}");
    }

    [Shim(Module, "WinHttpQueryDataAvailable", SetsLastError = true)]
    [UnmanagedCallersOnly]
    private static int WinHttpQueryDataAvailable(nint request, uint* available)
    {
        return (int)Refuse("WinHttpQueryDataAvailable", $"0x{request:x}");
    }

    /// <summary>Records the attempt and fails it: NULL or FALSE with ERROR_WINHTTP_CANNOT_CONNECT.</summary>
    private static nint Refuse(string api, string target)
    {
        ShimContext.Runtime.Network.Block(api, target);
        ShimContext.Runtime.LastError.Set(NetworkErrors.CannotConnect);

        return 0;
    }
}
