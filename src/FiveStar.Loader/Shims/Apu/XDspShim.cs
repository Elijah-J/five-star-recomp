// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Runtime.InteropServices;
using FiveStar.Loader.Native;

namespace FiveStar.Loader.Shims.Apu;

/// <summary>
/// chad.dll entry points; the work is in <see cref="XDspDevice"/>. Signatures are inferred from the executable's call
/// sites: connect, activate and submit succeed only on 0; the deactivate and disconnect results are not read.
/// </summary>
internal static unsafe class XDspShim
{
    [Shim(AudioHardwareModules.XDspModule, "XDspConnectWithMaximumStreamLimit")]
    [UnmanagedCallersOnly]
    private static int XDspConnectWithMaximumStreamLimit(nint memory, uint bytes, uint maxStreams, uint commandLimit, uint flags, nint* handle)
    {
        return ShimContext.Runtime.XDsp.Connect(memory, bytes, maxStreams, handle);
    }

    [Shim(AudioHardwareModules.XDspModule, "XDspActivate")]
    [UnmanagedCallersOnly]
    private static int XDspActivate(nint handle, byte* config, nint* status, nint* stream)
    {
        return ShimContext.Runtime.XDsp.Activate(handle, config, status, stream);
    }

    [Shim(AudioHardwareModules.XDspModule, "XDspSubmitCommand")]
    [UnmanagedCallersOnly]
    private static int XDspSubmitCommand(nint stream, byte* command, uint* id)
    {
        return ShimContext.Runtime.XDsp.Submit(stream, command, id);
    }

    [Shim(AudioHardwareModules.XDspModule, "XDspDeactivate")]
    [UnmanagedCallersOnly]
    private static int XDspDeactivate(nint stream)
    {
        ShimContext.Runtime.XDsp.Deactivate(stream);

        return HResult.Ok;
    }

    [Shim(AudioHardwareModules.XDspModule, "XDspDisconnect")]
    [UnmanagedCallersOnly]
    private static int XDspDisconnect(nint handle)
    {
        ShimContext.Runtime.XDsp.Disconnect();

        return HResult.Ok;
    }
}
