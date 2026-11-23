// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Runtime.InteropServices;
using FiveStar.Loader.Native;

namespace FiveStar.Loader.Shims.D3D12X.Pipelines;

/// <summary>
/// The native entry points of the five pipeline slots, for the device vtable. Each finds the device's
/// <see cref="PipelineSlots"/> through a GCHandle the device object keeps at <see cref="OwnerOffset"/>.
/// </summary>
/// <remarks>
/// TODO(integration): <see cref="XboxDeviceObject"/> ends at 0x10 and has no owner slot yet. Once the device wrapper
/// keeps a GCHandle of its <see cref="PipelineSlots"/> (or the registration API for replacing stub slots lands), call
/// <see cref="Fill"/> on the device vtable after it is built, or register these five methods through that API. Until
/// then a device without the handle answers E_NOTIMPL, as the stubs do.
/// </remarks>
internal static unsafe class PipelineSlotHandlers
{
    /// <summary>Offset of the GCHandle of <see cref="PipelineSlots"/> in the device object (after Vtable and Inner).</summary>
    public const int OwnerOffset = 0x10;

    /// <summary>Points the five pipeline slots of a device vtable at these handlers.</summary>
    public static void Fill(nint* vtable)
    {
        delegate* unmanaged<nint, nint, uint, byte*, Guid*, nint*, int> derive = &Derive;
        delegate* unmanaged<nint, byte*, Guid*, nint*, int> cachedGraphics = &CachedGraphics;
        delegate* unmanaged<nint, byte*, nint, nint, Guid*, nint*, int> compute = &Compute;
        delegate* unmanaged<nint, byte*, Guid*, nint*, int> cachedCompute = &CachedCompute;
        delegate* unmanaged<nint, byte*, Guid*, nint*, int> stream = &Stream;

        vtable[PipelineLayout.DeriveSlot] = (nint)derive;
        vtable[PipelineLayout.PrecompiledGraphicsSlot] = (nint)cachedGraphics;
        vtable[PipelineLayout.ComputeSlot] = (nint)compute;
        vtable[PipelineLayout.PrecompiledComputeSlot] = (nint)cachedCompute;
        vtable[PipelineLayout.StreamSlot] = (nint)stream;
    }

    [UnmanagedCallersOnly]
    private static int Derive(nint self, nint basePipeline, uint count, byte* entries, Guid* iid, nint* result)
    {
        var slots = Owner(self);

        if (slots is null)
        {
            return HResult.NotImplemented;
        }

        return slots.Derive(basePipeline, count, entries, iid, result);
    }

    [UnmanagedCallersOnly]
    private static int CachedGraphics(nint self, byte* desc, Guid* iid, nint* result)
    {
        var slots = Owner(self);

        if (slots is null)
        {
            return HResult.NotImplemented;
        }

        return slots.CachedGraphics(desc, iid, result);
    }

    [UnmanagedCallersOnly]
    private static int Compute(nint self, byte* desc, nint second, nint third, Guid* iid, nint* result)
    {
        var slots = Owner(self);

        if (slots is null)
        {
            return HResult.NotImplemented;
        }

        return slots.Compute(desc, second, third, iid, result);
    }

    [UnmanagedCallersOnly]
    private static int CachedCompute(nint self, byte* desc, Guid* iid, nint* result)
    {
        var slots = Owner(self);

        if (slots is null)
        {
            return HResult.NotImplemented;
        }

        return slots.CachedCompute(desc, iid, result);
    }

    [UnmanagedCallersOnly]
    private static int Stream(nint self, byte* desc, Guid* iid, nint* result)
    {
        var slots = Owner(self);

        if (slots is null)
        {
            return HResult.NotImplemented;
        }

        return slots.Stream(desc, iid, result);
    }

    private static PipelineSlots? Owner(nint self)
    {
        var handle = *(nint*)(self + OwnerOffset);

        if (handle == 0)
        {
            return null;
        }

        return GCHandle.FromIntPtr(handle).Target as PipelineSlots;
    }
}
