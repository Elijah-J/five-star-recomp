// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using FiveStar.Loader.Native;
using FiveStar.Loader.Shims.D3D12X.Present;

namespace FiveStar.Loader.Shims.D3D12X.States;

/// <summary>
/// Writes barriers on a desktop list: a texture transition as an enhanced texture barrier
/// (ID3D12GraphicsCommandList7::Barrier) when the list has that interface and both states have a layout, else as a
/// legacy transition (ResourceBarrier); legacy barriers the game wrote unchanged apart from their resources.
/// </summary>
internal static unsafe class BarrierWriter
{
    /// <summary>ID3D12GraphicsCommandList::ResourceBarrier and ID3D12GraphicsCommandList7::Barrier (d3d12.h).</summary>
    public const int ResourceBarrierSlot = 26;
    private const int EnhancedBarrierSlot = 80;

    [HotPath]
    public static void Transition(nint list, nint enhanced, nint resource, int before, int after)
    {
        if (enhanced != 0 && EnhancedBarrierScopes.TryMap(before, out var from) && EnhancedBarrierScopes.TryMap(after, out var to))
        {
            var barrier = TextureBarrier.Of(resource, from, to);
            var group = new BarrierGroup { Type = BarrierGroup.TextureType, Count = 1, Barriers = &barrier };
            ((delegate* unmanaged<nint, uint, BarrierGroup*, void>)ComObject.Slot(enhanced, EnhancedBarrierSlot))(enhanced, 1, &group);

            return;
        }

        var legacy = TransitionBarrier.Of(resource, before, after);
        Legacy(list, &legacy);
    }

    /// <summary>One legacy D3D12_RESOURCE_BARRIER on <paramref name="list"/>.</summary>
    [HotPath]
    public static void Legacy(nint list, TransitionBarrier* barrier)
    {
        ((delegate* unmanaged<nint, uint, TransitionBarrier*, void>)ComObject.Slot(list, ResourceBarrierSlot))(list, 1, barrier);
    }
}
