// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using FiveStar.Loader.Native;

namespace FiveStar.Loader.Shims.Gdk;

/// <summary>
/// XNetworking.h answered offline: the network stack is up, but the game has no connectivity (level None), so titles
/// take their offline paths instead of waiting for Xbox services. The preferred multiplayer port is the Xbox default.
/// </summary>
internal static unsafe class NetworkingService
{
    public const string PortApi = "XNetworkingQueryPreferredLocalUdpMultiplayerPortAsync";

    /// <summary>UDP 3074, the port Xbox titles use for multiplayer traffic.</summary>
    public const ushort PreferredUdpPort = 3074;

    // struct XNetworkingConnectivityHint { level; cost; uint32_t ianaInterfaceType; bool networkInitialized,
    // approachingDataLimit, overDataLimit, roaming; }: 16 bytes.
    public const int HintSize = 16;
    public const int HintLevelOffset = 0;
    public const int HintCostOffset = 4;
    public const int HintInitializedOffset = 12;

    /// <summary>XNetworkingConnectivityLevelHint::None.</summary>
    public const uint LevelNone = 1;

    /// <summary>XNetworkingConnectivityCostHint::Unrestricted.</summary>
    private const uint CostUnrestricted = 1;

    public static int GetConnectivityHint(byte* hint)
    {
        if (hint == null)
        {
            return HResult.Pointer;
        }

        new Span<byte>(hint, HintSize).Clear();
        *(uint*)(hint + HintLevelOffset) = LevelNone;
        *(uint*)(hint + HintCostOffset) = CostUnrestricted;
        hint[HintInitializedOffset] = GdkModule.True;

        return HResult.Ok;
    }

    public static int QueryPreferredPort(ushort* port)
    {
        if (port == null)
        {
            return HResult.Pointer;
        }

        *port = PreferredUdpPort;

        return HResult.Ok;
    }
}
