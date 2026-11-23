// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

namespace FiveStar.Loader.Shims.Apu;

/// <summary>
/// The chad.dll (XDsp) records. chad.dll is not in the public GDK headers: every offset is read from the executable's
/// effect code (docs/audio-runtime.md), which fills an activation config, submits 0x20-byte commands and polls a
/// status record the activation hands back.
/// </summary>
internal static class XDspLayout
{
    /// <summary>Activation config: the effect kind (<see cref="XDspEffectKind"/>).</summary>
    public const int ConfigKindOffset = 0x00;

    /// <summary>Activation config: frames per processed block (the game always asks for 1024).</summary>
    public const int ConfigFramesOffset = 0x08;

    /// <summary>Activation config: 1 or 2 for a convolution (the channels, inferred); 1 for the other kind.</summary>
    public const int ConfigChannelsOffset = 0x0C;

    /// <summary>Convolution config: impulse response length in floats, then its address (unaligned u64).</summary>
    public const int ConfigResponseLengthOffset = 0x10;
    public const int ConfigResponseAddressOffset = 0x14;

    /// <summary>Bytes of one command record.</summary>
    public const int CommandSize = 0x20;

    /// <summary>Command: input block address, then output block address (u64 each); +0x00 and +0x04 hold 1.0f.</summary>
    public const int CommandInputOffset = 0x08;
    public const int CommandOutputOffset = 0x10;

    /// <summary>
    /// The status record of one stream. The game reads the u32 at <see cref="StatusCompletedOffset"/>, compares it
    /// with the id the last submit returned, and takes results only while the u32 at <see cref="StatusErrorOffset"/>
    /// is 0.
    /// </summary>
    public const int StatusSize = 0x40;
    public const int StatusCompletedOffset = 0x04;
    public const int StatusErrorOffset = 0x0C;

    /// <summary>The most streams one connection holds; the game asks for 0x18 or 0x1E.</summary>
    public const uint MaxStreams = 0x100;
}
