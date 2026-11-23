// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using FiveStar.Loader.Native;

namespace FiveStar.Loader.Shims.Apu;

/// <summary>
/// chad.dll (XDsp, the audio DSP cores) and xapu.dll (the APU command queue, <see cref="XApuShim"/>). Neither is in
/// the public GDK headers. XDsp has no CPU version: connecting and submitting fail with E_NOTIMPL. The return
/// conventions are read from the executable's call sites: XDspActivate, XDspConnectWithMaximumStreamLimit and
/// XDspSubmitCommand succeed only on 0, and the disconnect and deactivate results are not read.
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
}
