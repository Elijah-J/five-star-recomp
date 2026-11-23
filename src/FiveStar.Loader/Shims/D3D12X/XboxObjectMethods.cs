// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Runtime.InteropServices;
using FiveStar.Loader.Native;

namespace FiveStar.Loader.Shims.D3D12X;

/// <summary>
/// Slots every wrapper shares, and the getters whose D3D12.X form differs from the desktop one. The wrapper's
/// reference count is the desktop object's: Release frees the wrapper when the desktop object goes.
/// </summary>
internal static unsafe class XboxObjectMethods
{
    /// <summary>ID3D12DeviceChild::GetDevice and IUnknown slots in the desktop vtable (d3d12.h, unknwn.h).</summary>
    private const int DesktopGetDeviceSlot = 7;

    /// <summary>ID3D12Resource::GetGPUVirtualAddress (d3d12.h).</summary>
    private const int DesktopGetGpuAddressSlot = 11;

    /// <summary>ID3D12DescriptorHeap::GetCPUDescriptorHandleForHeapStart and ::GetGPUDescriptorHandleForHeapStart.</summary>
    private const int DesktopCpuStartSlot = 9;
    private const int DesktopGpuStartSlot = 10;

    [HotPath]
    [UnmanagedCallersOnly]
    public static uint Release(XboxObject* self)
    {
        nint inner = self->Inner;
        uint left = ((delegate* unmanaged<nint, uint>)ComObject.Slot(inner, ComObject.ReleaseSlot))(inner);

        if (left == 0)
        {
            NativeMemory.Free(self);
        }

        return left;
    }

    /// <summary>QueryInterface on the desktop object; an answer that is the desktop object itself becomes the wrapper.</summary>
    [UnmanagedCallersOnly]
    public static int QueryInterface(XboxObject* self, Guid* iid, nint* result)
    {
        nint inner = self->Inner;
        int hr = ((delegate* unmanaged<nint, Guid*, nint*, int>)ComObject.Slot(inner, ComObject.QueryInterfaceSlot))(inner, iid, result);

        if (!HResult.Failed(hr) && result != null && *result == inner)
        {
            *result = (nint)self;
        }

        return hr;
    }

    /// <summary>GetDevice returns the game's device wrapper, not the desktop device.</summary>
    [UnmanagedCallersOnly]
    public static int GetDevice(XboxObject* self, Guid* iid, nint* device)
    {
        nint inner = self->Inner;
        int hr = ((delegate* unmanaged<nint, Guid*, nint*, int>)ComObject.Slot(inner, DesktopGetDeviceSlot))(inner, iid, device);

        if (!HResult.Failed(hr) && device != null)
        {
            *device = ShimContext.Runtime.D3D12X.Objects.DeviceFor(*device);
        }

        return hr;
    }

    /// <summary>Resource slot 11: the console address for a resource the game placed (slot 133), else the desktop one.</summary>
    [HotPath]
    [UnmanagedCallersOnly]
    public static ulong GetGpuVirtualAddress(XboxResourceObject* self)
    {
        if (self->GameAddress != 0)
        {
            return self->GameAddress;
        }

        nint inner = self->Header.Inner;

        return ((delegate* unmanaged<nint, ulong>)ComObject.Slot(inner, DesktopGetGpuAddressSlot))(inner);
    }

    /// <summary>
    /// Descriptor-heap slot 9: the game takes the handle in rax with only the heap in rcx (call after creation at
    /// 0x141c74559); the desktop method returns it through a hidden pointer. A heap in title memory answers with that memory.
    /// </summary>
    [HotPath]
    [UnmanagedCallersOnly]
    public static nint GetCpuHandleForHeapStart(XboxDescriptorHeapObject* self)
    {
        if (self->GameCpuStart != 0)
        {
            return self->GameCpuStart;
        }

        return HeapStart(&self->Header, DesktopCpuStartSlot);
    }

    /// <summary>Descriptor-heap slot 10, the GPU handle, returned like slot 9 (*inferred*); a heap in title memory answers with that memory too.</summary>
    [HotPath]
    [UnmanagedCallersOnly]
    public static nint GetGpuHandleForHeapStart(XboxDescriptorHeapObject* self)
    {
        if (self->GameGpuStart != 0)
        {
            return self->GameGpuStart;
        }

        return HeapStart(&self->Header, DesktopGpuStartSlot);
    }

    [HotPath]
    private static nint HeapStart(XboxObject* self, int desktopSlot)
    {
        nint inner = self->Inner;
        nint handle = 0;
        _ = ((delegate* unmanaged<nint, nint*, nint*>)ComObject.Slot(inner, desktopSlot))(inner, &handle);

        return handle;
    }
}
