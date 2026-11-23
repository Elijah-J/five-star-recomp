// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using FiveStar.Loader.Native;

namespace FiveStar.Loader.Shims.Apu;

/// <summary>
/// chad.dll (XDsp, the audio DSP cores) and xapu.dll (the APU command queue): console audio hardware with no PC
/// counterpart. Neither is in the public GDK headers. Connecting and submitting fail with E_NOTIMPL; the return
/// conventions are read from the executable's call sites: XDspActivate, XDspConnectWithMaximumStreamLimit and
/// XDspSubmitCommand succeed only on 0, XApuConnect and XApuDequeueResult are HRESULTs, XApuGetResultCount is a
/// count (0 means nothing to dequeue), and the disconnect and deactivate results are not read.
/// </summary>
[ShimModule(XDspModule, UnresolvedReturn = HResult.NotImplemented)]
[ShimModule(XApuModule, UnresolvedReturn = HResult.NotImplemented)]
internal static class AudioHardwareModules
{
    public const string XDspModule = "chad.dll";
    public const string XApuModule = "xapu.dll";

    private const long NotImplemented = HResult.NotImplemented;
    private const long Nothing = 0;

    [NotImplementedTable(XDspModule)]
    private static readonly (string Name, long ReturnValue)[] XDsp =
    [
        ("XDspActivate", NotImplemented), ("XDspConnectWithMaximumStreamLimit", NotImplemented), ("XDspSubmitCommand", NotImplemented),
        ("XDspDeactivate", Nothing), ("XDspDisconnect", Nothing),
    ];

    [NotImplementedTable(XApuModule)]
    private static readonly (string Name, long ReturnValue)[] XApu =
    [
        ("XApuConnect", NotImplemented), ("XApuEnqueueCommand", NotImplemented), ("XApuDequeueResult", NotImplemented),
        ("XApuGetResultCount", Nothing), ("XApuDisconnect", Nothing),
    ];
}
