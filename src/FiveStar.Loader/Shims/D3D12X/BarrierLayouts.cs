// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

namespace FiveStar.Loader.Shims.D3D12X;

/// <summary>
/// D3D12_BARRIER_LAYOUT to the legacy D3D12_RESOURCE_STATES a resource is created in (d3d12.h). Resources made with
/// the legacy creation calls still take enhanced barriers, so the layouts the game passes map onto states here.
/// </summary>
internal static class BarrierLayouts
{
    /// <summary>D3D12_RESOURCE_STATE_COMMON, also for D3D12_BARRIER_LAYOUT_UNDEFINED and layouts past the table.</summary>
    public const int CommonState = 0;

    /// <summary>States for layouts 0 (COMMON) to 11 (SHADING_RATE_SOURCE), in layout order.</summary>
    private static readonly int[] States =
    [
        CommonState, 0xAC3, 0x4, 0x8, 0x10, 0x20, 0xC0, 0x800, 0x400, 0x2000, 0x1000, 0x1000000,
    ];

    public static int ToState(int layout)
    {
        if (layout < 0 || layout >= States.Length)
        {
            return CommonState;
        }

        return States[layout];
    }
}
