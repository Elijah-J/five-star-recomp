// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using FiveStar.Loader.Tests.Support;
using Xunit;

namespace FiveStar.Loader.Tests;

/// <summary>D3D12XboxCreateDevice: the D3D12.X device vtable on a desktop device.</summary>
public sealed unsafe class D3D12XDeviceTests
{
    private const int GetNodeCountSlot = 7;
    private const int GetCustomHeapPropertiesSlot = 26;
    private const int UnmappedXboxSlot = 44;
    private const int CreateCommandQueueSlot = 48;
    private const int CreateCommandListSlot = 73;
    private const int CreateCommandAllocatorSlot = 9;
    private const int CloseSlot = 9;
    private const int ReleaseSlot = 2;
    private const int ComputeType = 2;
    private const int HeapTypeUpload = 2;
    private const int HeapTypeCustom = 4;
    private const int HeapPropertiesSize = 20;
    private const int NotImplemented = unchecked((int)0x80004001);

    [Fact]
    public void Device_forwards_desktop_slots_and_stubs_xbox_slots()
    {
        using var test = TestRuntime.Create();
        var create = (delegate* unmanaged<nint, byte*, Guid*, nint*, int>)test.Shim("d3d12_xs.dll", "D3D12XboxCreateDevice");
        byte* parameters = stackalloc byte[0x50];
        var iid = Guid.Empty;
        nint device = 0;

        Assert.Equal(0, create(0, parameters, &iid, &device));
        nint* vtable = *(nint**)device;

        // A plain slot, then a struct-returning one: the game passes the return buffer first and the object second.
        uint nodes = ((delegate* unmanaged<nint, uint>)vtable[GetNodeCountSlot])(device);
        byte* properties = stackalloc byte[HeapPropertiesSize];
        var custom = (delegate* unmanaged<byte*, nint, uint, int, byte*>)vtable[GetCustomHeapPropertiesSlot];
        custom(properties, device, 1, HeapTypeUpload);
        int xboxOnly = ((delegate* unmanaged<nint, int>)vtable[UnmappedXboxSlot])(device);

        Assert.Equal(1u, nodes);
        Assert.Equal(HeapTypeCustom, *(int*)properties);
        Assert.Equal(NotImplemented, xboxOnly);
    }

    [Fact]
    public void Xbox_queue_and_list_slots_create_desktop_objects()
    {
        using var test = TestRuntime.Create();
        var create = (delegate* unmanaged<nint, byte*, Guid*, nint*, int>)test.Shim("d3d12_xs.dll", "D3D12XboxCreateDevice");
        byte* parameters = stackalloc byte[0x50];
        var iid = Guid.Empty;
        nint device = 0;

        Assert.Equal(0, create(0, parameters, &iid, &device));
        nint* vtable = *(nint**)device;

        // The game's compute-queue desc: type 2, flags 2 (not a desktop flag), node mask 0.
        int* queueDesc = stackalloc int[] { ComputeType, 0, 2, 0 };
        var queueIid = new Guid("0ec870a6-5d7e-4c22-8cfc-5baae07616ed");
        nint queue = 0;
        var createQueue = (delegate* unmanaged<nint, int*, Guid*, nint*, int>)vtable[CreateCommandQueueSlot];
        Assert.Equal(0, createQueue(device, queueDesc, &queueIid, &queue));

        var allocatorIid = new Guid("6102dee4-af59-4b09-b999-b44d73f09b24");
        nint allocator = 0;
        var createAllocator = (delegate* unmanaged<nint, int, Guid*, nint*, int>)vtable[CreateCommandAllocatorSlot];
        Assert.Equal(0, createAllocator(device, ComputeType, &allocatorIid, &allocator));

        int* listDesc = stackalloc int[] { ComputeType, 6, 1, 0 };
        nint list = 0;
        var createList = (delegate* unmanaged<nint, int*, nint, nint, Guid*, nint*, int>)vtable[CreateCommandListSlot];
        Assert.Equal(0, createList(device, listDesc, allocator, 0, &allocatorIid, &list));
        int closed = ((delegate* unmanaged<nint, int>)(*(nint**)list)[CloseSlot])(list);

        Assert.Equal(0, closed);
        Assert.NotEqual(0, queue);
        _ = ((delegate* unmanaged<nint, uint>)(*(nint**)queue)[ReleaseSlot])(queue);
    }
}
