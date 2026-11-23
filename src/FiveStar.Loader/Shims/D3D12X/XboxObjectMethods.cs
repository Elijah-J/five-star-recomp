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

    /// <summary>ID3D12Resource::Map and ::Unmap (d3d12.h); the D3D12.X resource keeps the slots.</summary>
    public const int MapSlot = 8;
    public const int UnmapSlot = 9;
    private const int DesktopMapSlot = MapSlot;
    private const int DesktopUnmapSlot = UnmapSlot;

    /// <summary>ID3D12DescriptorHeap::GetCPUDescriptorHandleForHeapStart and ::GetGPUDescriptorHandleForHeapStart.</summary>
    private const int DesktopCpuStartSlot = 9;
    private const int DesktopGpuStartSlot = 10;

    /// <summary>A slot <c>--d3d12-skip</c> switched off: does nothing and returns 0 (S_OK), whatever the arguments.</summary>
    [HotPath]
    [UnmanagedCallersOnly]
    public static nint Skipped(XboxObject* self)
    {
        return 0;
    }

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
    /// Resource slot 8, Map(subresource, read range, data): a resource the game placed (slot 133) maps to its own memory,
    /// as on the console, where a texture's bytes are CPU-visible (0x145121891 maps a texture and writes a mip at
    /// the XG offset); its desktop texture is a committed one the CPU cannot map. Others map on the desktop.
    /// </summary>
    [HotPath]
    [UnmanagedCallersOnly]
    public static int Map(XboxResourceObject* self, uint subresource, void* readRange, void** data)
    {
        byte* game = GameBytes(self);

        if (game != null)
        {
            if (data != null)
            {
                *data = game;
            }

            return HResult.Ok;
        }

        nint inner = self->Header.Inner;

        return ((delegate* unmanaged<nint, uint, void*, void**, int>)ComObject.Slot(inner, DesktopMapSlot))(inner, subresource, readRange, data);
    }

    /// <summary>Resource slot 9, Unmap(subresource, written range): nothing to do for game memory.</summary>
    [HotPath]
    [UnmanagedCallersOnly]
    public static void Unmap(XboxResourceObject* self, uint subresource, void* writtenRange)
    {
        if (GameBytes(self) != null)
        {
            return;
        }

        nint inner = self->Header.Inner;
        ((delegate* unmanaged<nint, uint, void*, void>)ComObject.Slot(inner, DesktopUnmapSlot))(inner, subresource, writtenRange);
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

    /// <summary>The CPU address of a placed resource's game memory, or null when it has none mapped.</summary>
    [HotPath]
    private static byte* GameBytes(XboxResourceObject* self)
    {
        if (self->GameAddress == 0)
        {
            return null;
        }

        return ShimContext.Runtime.D3D12X.MemorySource.Find(self->GameAddress, 1);
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
