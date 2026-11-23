// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Runtime.InteropServices;
using FiveStar.Loader.Native;

namespace FiveStar.Loader.Shims.D3D12X;

/// <summary>
/// The desktop-order device slots that create objects: each calls the desktop method with any wrapped argument
/// unwrapped and hands the game a wrapper. Creation runs at load and level changes, not per frame.
/// </summary>
internal static unsafe class XboxDeviceCreation
{
    /// <summary>ID3D12Device slots in desktop order (d3d12.h); the D3D12.X device keeps them.</summary>
    public const int CreateCommandQueueSlot = 8;
    public const int CreateCommandAllocatorSlot = 9;
    public const int CreateCommandListSlot = 12;
    public const int CreateDescriptorHeapSlot = 14;
    public const int GetResourceAllocationInfoSlot = 25;
    public const int CreateCommittedResourceSlot = 27;
    public const int CreateHeapSlot = 28;
    public const int CreatePlacedResourceSlot = 29;
    public const int CreateReservedResourceSlot = 30;
    public const int MakeResidentSlot = 34;
    public const int EvictSlot = 35;
    public const int CreateFenceSlot = 36;
    public const int GetCopyableFootprintsSlot = 38;
    public const int CreateQueryHeapSlot = 39;
    public const int CreateCommandSignatureSlot = 41;

    private static XboxObjects Objects => ShimContext.Runtime.D3D12X.Objects;

    [UnmanagedCallersOnly]
    public static int CreateCommandQueue(XboxObject* self, void* description, Guid* iid, nint* queue)
    {
        return CreateFromDescription(self, CreateCommandQueueSlot, XboxInterfaceKind.CommandQueue, description, iid, queue);
    }

    [UnmanagedCallersOnly]
    public static int CreateDescriptorHeap(XboxObject* self, DescriptorHeapDescription* description, Guid* iid, nint* heap)
    {
        if (description == null)
        {
            return HResult.InvalidArgument;
        }

        // The game passes node mask 1 (0x141cf32b3), which the desktop takes as the one GPU like 0.
        return ShimContext.Runtime.D3D12X.DescriptorHeaps.Create(self->Inner, description, iid, heap);
    }

    [UnmanagedCallersOnly]
    public static int CreateHeap(XboxObject* self, void* description, Guid* iid, nint* heap)
    {
        return CreateFromDescription(self, CreateHeapSlot, XboxInterfaceKind.Heap, description, iid, heap);
    }

    [UnmanagedCallersOnly]
    public static int CreateQueryHeap(XboxObject* self, void* description, Guid* iid, nint* heap)
    {
        return CreateFromDescription(self, CreateQueryHeapSlot, XboxInterfaceKind.QueryHeap, description, iid, heap);
    }

    [UnmanagedCallersOnly]
    public static int CreateCommandAllocator(XboxObject* self, int type, Guid* iid, nint* allocator)
    {
        nint inner = self->Inner;
        var create = (delegate* unmanaged<nint, int, Guid*, nint*, int>)ComObject.Slot(inner, CreateCommandAllocatorSlot);

        return Objects.WrapResult(create(inner, type, iid, allocator), XboxInterfaceKind.CommandAllocator, allocator);
    }

    /// <summary>Desktop CreateCommandList(node mask, type, allocator, PSO, IID, out), wrapped like slot 73's lists.</summary>
    [UnmanagedCallersOnly]
    public static int CreateCommandList(XboxObject* self, uint nodeMask, int type, nint allocator, nint pipelineState, Guid* iid, nint* list)
    {
        return ShimContext.Runtime.D3D12X.CommandLists.Create(self->Inner, type, allocator, pipelineState, list);
    }

    [UnmanagedCallersOnly]
    public static int CreateCommittedResource(XboxObject* self, void* properties, int flags, ResourceDescription* description, int state, void* clear, Guid* iid, nint* resource)
    {
        nint inner = self->Inner;
        var desktop = description->ToDesktop();
        var create = (delegate* unmanaged<nint, void*, int, ResourceDescription*, int, void*, Guid*, nint*, int>)ComObject.Slot(inner, CreateCommittedResourceSlot);

        return Objects.WrapResult(create(inner, properties, flags, &desktop, state, clear, iid, resource), XboxInterfaceKind.Resource, resource);
    }

    [UnmanagedCallersOnly]
    public static int CreatePlacedResource(XboxObject* self, nint heap, ulong offset, ResourceDescription* description, int state, void* clear, Guid* iid, nint* resource)
    {
        nint inner = self->Inner;
        var desktop = description->ToDesktop();
        var create = (delegate* unmanaged<nint, nint, ulong, ResourceDescription*, int, void*, Guid*, nint*, int>)ComObject.Slot(inner, CreatePlacedResourceSlot);
        int hr = create(inner, Objects.Unwrap(heap), offset, &desktop, state, clear, iid, resource);

        return Objects.WrapResult(hr, XboxInterfaceKind.Resource, resource);
    }

    [UnmanagedCallersOnly]
    public static int CreateReservedResource(XboxObject* self, ResourceDescription* description, int state, void* clear, Guid* iid, nint* resource)
    {
        nint inner = self->Inner;
        var desktop = description->ToDesktop();
        var create = (delegate* unmanaged<nint, ResourceDescription*, int, void*, Guid*, nint*, int>)ComObject.Slot(inner, CreateReservedResourceSlot);

        return Objects.WrapResult(create(inner, &desktop, state, clear, iid, resource), XboxInterfaceKind.Resource, resource);
    }

    /// <summary>
    /// Slot 25 with the console's struct-return order (result in rcx, device in rdx): the descs made desktop ones. The
    /// game breaks into the debugger when the size does not fit 32 bits (0x140344196), which a rejected desc gives.
    /// </summary>
    [UnmanagedCallersOnly]
    public static ResourceAllocationInfo* GetResourceAllocationInfo(ResourceAllocationInfo* result, XboxObject* self, uint visibleMask, uint count, ResourceDescription* descriptions)
    {
        const int MaxStackDescriptions = 16;
        bool onStack = count <= MaxStackDescriptions;
        ResourceDescription* stack = stackalloc ResourceDescription[MaxStackDescriptions];
        ResourceDescription* desktop = onStack ? stack : (ResourceDescription*)NativeMemory.Alloc(count, (nuint)sizeof(ResourceDescription));

        for (uint i = 0; i < count; i++)
        {
            desktop[i] = descriptions[i].ToDesktop();
        }

        nint inner = self->Inner;
        var query = (delegate* unmanaged<nint, ResourceAllocationInfo*, uint, uint, ResourceDescription*, ResourceAllocationInfo*>)ComObject.Slot(inner, GetResourceAllocationInfoSlot);
        query(inner, result, visibleMask, count, desktop);

        if (!onStack)
        {
            NativeMemory.Free(desktop);
        }

        return result;
    }

    /// <summary>GetCopyableFootprints with the desc made a desktop one.</summary>
    [UnmanagedCallersOnly]
    public static void GetCopyableFootprints(XboxObject* self, ResourceDescription* description, uint first, uint count, ulong baseOffset, void* layouts, uint* rows, ulong* rowSizes, ulong* totalBytes)
    {
        nint inner = self->Inner;
        var desktop = description->ToDesktop();
        var query = (delegate* unmanaged<nint, ResourceDescription*, uint, uint, ulong, void*, uint*, ulong*, ulong*, void>)ComObject.Slot(inner, GetCopyableFootprintsSlot);
        query(inner, &desktop, first, count, baseOffset, layouts, rows, rowSizes, totalBytes);
    }

    [UnmanagedCallersOnly]
    public static int CreateFence(XboxObject* self, ulong initialValue, int flags, Guid* iid, nint* fence)
    {
        nint inner = self->Inner;
        var create = (delegate* unmanaged<nint, ulong, int, Guid*, nint*, int>)ComObject.Slot(inner, CreateFenceSlot);

        return Objects.WrapResult(create(inner, initialValue, flags, iid, fence), XboxInterfaceKind.Fence, fence);
    }

    [UnmanagedCallersOnly]
    public static int CreateCommandSignature(XboxObject* self, void* description, nint rootSignature, Guid* iid, nint* signature)
    {
        nint inner = self->Inner;
        var create = (delegate* unmanaged<nint, void*, nint, Guid*, nint*, int>)ComObject.Slot(inner, CreateCommandSignatureSlot);

        return Objects.WrapResult(create(inner, description, rootSignature, iid, signature), XboxInterfaceKind.CommandSignature, signature);
    }

    /// <summary>
    /// MakeResident and Evict take arrays of wrapped objects. Desktop objects are resident from creation and the shim
    /// never evicts, so both succeed without a call.
    /// </summary>
    [UnmanagedCallersOnly]
    public static int Residency(XboxObject* self, uint count, nint* objects)
    {
        return HResult.Ok;
    }

    private static int CreateFromDescription(XboxObject* self, int slot, XboxInterfaceKind kind, void* description, Guid* iid, nint* result)
    {
        nint inner = self->Inner;
        var create = (delegate* unmanaged<nint, void*, Guid*, nint*, int>)ComObject.Slot(inner, slot);

        return Objects.WrapResult(create(inner, description, iid, result), kind, result);
    }
}
