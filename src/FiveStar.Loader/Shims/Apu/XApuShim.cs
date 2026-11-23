// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Runtime.InteropServices;

namespace FiveStar.Loader.Shims.Apu;

/// <summary>
/// xapu.dll entry points; the work is in <see cref="XApuDevice"/>. Signatures are inferred from the executable's call
/// sites: XApuConnect and XApuDequeueResult are tested as HRESULTs, XApuGetResultCount as a count.
/// </summary>
internal static unsafe class XApuShim
{
    [Shim(AudioHardwareModules.XApuModule, "XApuConnect")]
    [UnmanagedCallersOnly]
    private static int XApuConnect(nint config, nint* sharedMemory, nint* handle)
    {
        return ShimContext.Runtime.XApu.Connect(sharedMemory, handle);
    }

    [Shim(AudioHardwareModules.XApuModule, "XApuEnqueueCommand")]
    [UnmanagedCallersOnly]
    private static int XApuEnqueueCommand(nint handle, byte* command, uint flags)
    {
        return ShimContext.Runtime.XApu.Enqueue(handle, command);
    }

    [Shim(AudioHardwareModules.XApuModule, "XApuGetResultCount")]
    [UnmanagedCallersOnly]
    private static uint XApuGetResultCount(nint handle, uint flags)
    {
        return ShimContext.Runtime.XApu.ResultCount(handle);
    }

    [Shim(AudioHardwareModules.XApuModule, "XApuDequeueResult")]
    [UnmanagedCallersOnly]
    private static int XApuDequeueResult(nint handle, byte* result)
    {
        return ShimContext.Runtime.XApu.Dequeue(handle, result);
    }

    /// <summary>The result is not read.</summary>
    [Shim(AudioHardwareModules.XApuModule, "XApuDisconnect")]
    [UnmanagedCallersOnly]
    private static int XApuDisconnect(nint handle)
    {
        ShimContext.Runtime.XApu.Disconnect();

        return 0;
    }
}
