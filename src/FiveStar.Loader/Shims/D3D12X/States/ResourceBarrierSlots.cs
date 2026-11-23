// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Runtime.InteropServices;
using FiveStar.Loader.Native;
using FiveStar.Loader.Shims.D3D12X.Present;

namespace FiveStar.Loader.Shims.D3D12X.States;

/// <summary>
/// Command-list slots 26 (ResourceBarrier, *inferred* from the desktop order of slots 0 to 25; not called in boot) and
/// 9 (Close). A transition of a texture the shim created starts from the state the texture is in on the desktop
/// (its home state, or where an earlier barrier of the list moved it), not from the console state the game names;
/// Close moves every texture the list moved back home, so each list starts and ends with its textures at home.
/// Buffers rest in COMMON with implicit promotion: their transitions become UAV barriers (ordering only).
/// Resources the shim did not create keep the game's states. The first half of a split barrier is dropped and the
/// second does the transition; transitions cover all subresources.
/// </summary>
internal static unsafe class ResourceBarrierSlots
{
    public const int ResourceBarrierSlot = 26;
    public const int CloseSlot = 9;

    private static XboxDeviceFactory D3D12X => ShimContext.Runtime.D3D12X;

    public static void Bind(XboxObjects objects)
    {
        delegate* unmanaged<XboxObject*, uint, TransitionBarrier*, void> barrier = &ResourceBarrier;
        delegate* unmanaged<XboxObject*, int> close = &Close;
        objects.SetHandler(XboxInterfaceKind.CommandList, ResourceBarrierSlot, (nint)barrier);
        objects.SetHandler(XboxInterfaceKind.CommandList, CloseSlot, (nint)close);
    }

    [HotPath]
    [UnmanagedCallersOnly]
    public static void ResourceBarrier(XboxObject* self, uint count, TransitionBarrier* barriers)
    {
        if (barriers == null || count == 0)
        {
            return;
        }

        var d3d12x = D3D12X;
        var states = d3d12x.ListStates.For(self);

        for (uint i = 0; i < count; i++)
        {
            Translate(d3d12x.Objects, self->Inner, states, barriers + i);
        }
    }

    [HotPath]
    [UnmanagedCallersOnly]
    public static int Close(XboxObject* self)
    {
        nint inner = self->Inner;
        var states = D3D12X.ListStates.Find(self);

        if (states is not null && states.Count > 0)
        {
            Restore(inner, states);
        }

        return ((delegate* unmanaged<nint, int>)ComObject.Slot(inner, CloseSlot))(inner);
    }

    [HotPath]
    private static void Translate(XboxObjects objects, nint list, ListResourceStates states, TransitionBarrier* barrier)
    {
        switch (barrier->Type)
        {
            case TransitionBarrier.TransitionType:
                Transition(objects, list, states, barrier);
                break;
            case TransitionBarrier.AliasingType:
                Aliasing(objects, list, barrier);
                break;
            default:
                Uav(list, objects.Unwrap(barrier->Resource));
                break;
        }
    }

    [HotPath]
    private static void Transition(XboxObjects objects, nint list, ListResourceStates states, TransitionBarrier* barrier)
    {
        if ((barrier->Flags & TransitionBarrier.BeginOnlyFlag) != 0)
        {
            return;
        }

        var wrapper = objects.AsResource(barrier->Resource);
        nint resource = objects.Unwrap(barrier->Resource);

        if (wrapper == null || wrapper->Dimension == ResourceStates.UnknownDimension)
        {
            var copy = TransitionBarrier.Of(resource, barrier->StateBefore, barrier->StateAfter);
            copy.Subresource = barrier->Subresource;
            BarrierWriter.Write(list, &copy);

            return;
        }

        if (wrapper->Dimension == ResourceStates.BufferDimension)
        {
            Uav(list, resource);

            return;
        }

        int home = wrapper->HomeState;
        int current = states.Current(resource, home);
        int after = barrier->StateAfter;

        if (current == after)
        {
            return;
        }

        BarrierWriter.Transition(list, resource, current, after);

        if (!states.Set(resource, home, after))
        {
            D3D12X.Warnings.Warn("barrier-table", "[d3d12_xs] ResourceBarrier: a list moved more textures than its table holds; the rest stay where the game left them");
        }
    }

    [HotPath]
    private static void Aliasing(XboxObjects objects, nint list, TransitionBarrier* barrier)
    {
        var copy = new TransitionBarrier
        {
            Type = TransitionBarrier.AliasingType,
            Resource = objects.Unwrap(barrier->Resource),
            AliasingAfter = objects.Unwrap(barrier->AliasingAfter),
        };
        BarrierWriter.Write(list, &copy);
    }

    [HotPath]
    private static void Uav(nint list, nint resource)
    {
        var copy = new TransitionBarrier { Type = TransitionBarrier.UavType, Resource = resource };
        BarrierWriter.Write(list, &copy);
    }

    [HotPath]
    private static void Restore(nint list, ListResourceStates states)
    {
        for (int i = 0; i < states.Count; i++)
        {
            var (resource, home, state) = states.At(i);

            if (state != home)
            {
                BarrierWriter.Transition(list, resource, state, home);
            }
        }

        states.Clear();
    }
}
