// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Runtime.InteropServices;
using FiveStar.Loader.Native;
using FiveStar.Loader.Shims.D3D12X.Resources;
using FiveStar.Loader.Shims.D3D12X.States;

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

    /// <summary>ID3D12Device4::GetResourceAllocationInfo1 (d3d12.h) and the D3D12.X slot the game calls it through.</summary>
    public const int GetResourceAllocationInfo1Slot = 56;
    public const int XboxAllocationInfo1Slot = 108;

    private static readonly Guid Device4Iid = new("e865df17-a9ee-46f9-a463-3098315aa2e5");
    public const int CreateQueryHeapSlot = 39;
    public const int CreateCommandSignatureSlot = 41;

    private static XboxObjects Objects => ShimContext.Runtime.D3D12X.Objects;

    private static DesktopResourceDescriptions Descriptions => ShimContext.Runtime.D3D12X.Descriptions;

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

    /// <summary>Desktop CreateCommittedResource with the desc and clear value made desktop ones, in the resource's home state.</summary>
    [UnmanagedCallersOnly]
    public static int CreateCommittedResource(XboxObject* self, void* properties, int flags, ResourceDescription* description, int state, void* clear, Guid* iid, nint* resource)
    {
        nint inner = self->Inner;
        var desktop = Descriptions.ToDesktop(inner, description);
        int home = ResourceStates.Home(desktop.Dimension, desktop.Flags, state);
        byte* clearCopy = stackalloc byte[DepthViewDescriptions.ClearValueSize];
        var create = (delegate* unmanaged<nint, void*, int, ResourceDescription*, int, void*, Guid*, nint*, int>)ComObject.Slot(inner, CreateCommittedResourceSlot);
        int hr = create(inner, properties, flags, &desktop, home, DepthViewDescriptions.ClearValue(clear, clearCopy, desktop.Format), iid, resource);

        return WrapResource(hr, resource, desktop.Dimension, home);
    }

    [UnmanagedCallersOnly]
    public static int CreatePlacedResource(XboxObject* self, nint heap, ulong offset, ResourceDescription* description, int state, void* clear, Guid* iid, nint* resource)
    {
        nint inner = self->Inner;
        var desktop = Descriptions.ToDesktop(inner, description);
        int home = ResourceStates.Home(desktop.Dimension, desktop.Flags, state);
        byte* clearCopy = stackalloc byte[DepthViewDescriptions.ClearValueSize];
        var create = (delegate* unmanaged<nint, nint, ulong, ResourceDescription*, int, void*, Guid*, nint*, int>)ComObject.Slot(inner, CreatePlacedResourceSlot);
        int hr = create(inner, Objects.Unwrap(heap), offset, &desktop, home, DepthViewDescriptions.ClearValue(clear, clearCopy, desktop.Format), iid, resource);

        return WrapResource(hr, resource, desktop.Dimension, home);
    }

    [UnmanagedCallersOnly]
    public static int CreateReservedResource(XboxObject* self, ResourceDescription* description, int state, void* clear, Guid* iid, nint* resource)
    {
        nint inner = self->Inner;
        var desktop = Descriptions.ToDesktop(inner, description);
        int home = ResourceStates.Home(desktop.Dimension, desktop.Flags, state);
        byte* clearCopy = stackalloc byte[DepthViewDescriptions.ClearValueSize];
        var create = (delegate* unmanaged<nint, ResourceDescription*, int, void*, Guid*, nint*, int>)ComObject.Slot(inner, CreateReservedResourceSlot);
        int hr = create(inner, &desktop, home, DepthViewDescriptions.ClearValue(clear, clearCopy, desktop.Format), iid, resource);

        return WrapResource(hr, resource, desktop.Dimension, home);
    }

    /// <summary>
    /// Slot 25 with the console's struct-return order (result in rcx, device in rdx): the descs made desktop ones. The
    /// game breaks into the debugger when the size does not fit 32 bits (0x140344196), which a rejected desc gives.
    /// </summary>
    [UnmanagedCallersOnly]
    public static ResourceAllocationInfo* GetResourceAllocationInfo(ResourceAllocationInfo* result, XboxObject* self, uint visibleMask, uint count, ResourceDescription* descriptions)
    {
        nint inner = self->Inner;

        return QueryAllocationInfo(inner, ComObject.Slot(inner, GetResourceAllocationInfoSlot), result, visibleMask, count, descriptions, null);
    }

    /// <summary>
    /// Slot 108, GetResourceAllocationInfo1 (ID3D12Device4, *inferred*: struct return, mask, count, descs, per-desc
    /// offsets; call at 0x140abfe07, the same check as slot 25 follows). Answered by the desktop ID3D12Device4.
    /// </summary>
    [UnmanagedCallersOnly]
    public static ResourceAllocationInfo* GetResourceAllocationInfo1(ResourceAllocationInfo* result, XboxObject* self, uint visibleMask, uint count, ResourceDescription* descriptions, void* infos)
    {
        Guid iid = Device4Iid;
        nint device4 = 0;
        nint inner = self->Inner;

        if (HResult.Failed(((delegate* unmanaged<nint, Guid*, nint*, int>)ComObject.Slot(inner, ComObject.QueryInterfaceSlot))(inner, &iid, &device4)))
        {
            return QueryAllocationInfo(inner, ComObject.Slot(inner, GetResourceAllocationInfoSlot), result, visibleMask, count, descriptions, null);
        }

        QueryAllocationInfo(device4, ComObject.Slot(device4, GetResourceAllocationInfo1Slot), result, visibleMask, count, descriptions, infos);
        ComObject.Release(device4);

        return result;
    }

    /// <summary>GetCopyableFootprints with the desc made a desktop one.</summary>
    [UnmanagedCallersOnly]
    public static void GetCopyableFootprints(XboxObject* self, ResourceDescription* description, uint first, uint count, ulong baseOffset, void* layouts, uint* rows, ulong* rowSizes, ulong* totalBytes)
    {
        nint inner = self->Inner;
        var desktop = Descriptions.ToDesktop(inner, description);
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

    /// <summary>Desktop CreateCommandSignature with the D3D12.X argument type 0x69 read as a dispatch.</summary>
    [UnmanagedCallersOnly]
    public static int CreateCommandSignature(XboxObject* self, CommandSignatureDescription* description, nint rootSignature, Guid* iid, nint* signature)
    {
        const int MaxArguments = 32;

        if (description == null || description->ArgumentCount > MaxArguments)
        {
            return HResult.InvalidArgument;
        }

        var desktop = *description;
        IndirectArgument* arguments = stackalloc IndirectArgument[MaxArguments];

        for (uint i = 0; i < desktop.ArgumentCount; i++)
        {
            arguments[i] = description->Arguments[i];

            if (arguments[i].Type == IndirectArgument.XboxDispatchType)
            {
                arguments[i].Type = IndirectArgument.DispatchType;
            }
        }

        desktop.Arguments = arguments;
        nint inner = self->Inner;
        var create = (delegate* unmanaged<nint, CommandSignatureDescription*, nint, Guid*, nint*, int>)ComObject.Slot(inner, CreateCommandSignatureSlot);

        return Objects.WrapResult(create(inner, &desktop, rootSignature, iid, signature), XboxInterfaceKind.CommandSignature, signature);
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

    /// <summary>
    /// GetResourceAllocationInfo or GetResourceAllocationInfo1 (<paramref name="query"/>) on desktop copies of the
    /// descs (<see cref="DesktopResourceDescriptions.ToAllocationQuery"/>: a substituted render target is sized in its
    /// console format); the extra <paramref name="infos"/> argument is passed to both and read only by the second.
    /// </summary>
    private static ResourceAllocationInfo* QueryAllocationInfo(nint device, nint query, ResourceAllocationInfo* result, uint visibleMask, uint count, ResourceDescription* descriptions, void* infos)
    {
        const int MaxStackDescriptions = 16;
        bool onStack = count <= MaxStackDescriptions;
        ResourceDescription* stack = stackalloc ResourceDescription[MaxStackDescriptions];
        ResourceDescription* desktop = onStack ? stack : (ResourceDescription*)NativeMemory.Alloc(count, (nuint)sizeof(ResourceDescription));

        var call = (delegate* unmanaged<nint, ResourceAllocationInfo*, uint, uint, ResourceDescription*, void*, ResourceAllocationInfo*>)query;
        ConvertDescriptions(device, descriptions, desktop, count);
        call(device, result, visibleMask, count, desktop, infos);

        // A refused UAV format gives the error size; the query runs again without the flag on those textures.
        if (result->Size == ulong.MaxValue && RefuseUavs(descriptions, count))
        {
            ConvertDescriptions(device, descriptions, desktop, count);
            call(device, result, visibleMask, count, desktop, infos);
        }

        if (!onStack)
        {
            NativeMemory.Free(desktop);
        }

        return result;
    }

    private static void ConvertDescriptions(nint device, ResourceDescription* descriptions, ResourceDescription* desktop, uint count)
    {
        for (uint i = 0; i < count; i++)
        {
            desktop[i] = Descriptions.ToAllocationQuery(device, descriptions + i);
        }
    }

    private static bool RefuseUavs(ResourceDescription* descriptions, uint count)
    {
        bool changed = false;

        for (uint i = 0; i < count; i++)
        {
            changed |= Descriptions.RefuseUav(descriptions + i);
        }

        return changed;
    }

    /// <summary>Wraps a created resource and records its dimension and home state on the wrapper.</summary>
    private static int WrapResource(int hr, nint* resource, int dimension, int home)
    {
        hr = Objects.WrapResult(hr, XboxInterfaceKind.Resource, resource);

        if (!HResult.Failed(hr) && resource != null)
        {
            ResourceStates.Record(Objects.AsResource(*resource), dimension, home);
        }

        return hr;
    }
}
