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
    private const int FirstXboxOnlySlot = 48;
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
        int xboxOnly = ((delegate* unmanaged<nint, int>)vtable[FirstXboxOnlySlot])(device);

        Assert.Equal(1u, nodes);
        Assert.Equal(HeapTypeCustom, *(int*)properties);
        Assert.Equal(NotImplemented, xboxOnly);
    }
}
