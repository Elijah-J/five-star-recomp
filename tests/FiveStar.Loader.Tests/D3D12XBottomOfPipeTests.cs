// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using FiveStar.Loader.Shims.D3D12X;
using FiveStar.Loader.Tests.Support;
using Xunit;

namespace FiveStar.Loader.Tests;

/// <summary>List slots 71 and 74 write their values to game memory when the list runs, on the default adapter.</summary>
public sealed unsafe class D3D12XBottomOfPipeTests
{
    private const int CloseSlot = 9;
    private const int ExecuteSlot = 35;
    private const int SignalSlot = 16;
    private const int FenceWaitSlot = 9;

    [Fact]
    public void A_list_writes_its_dword_and_qword_values_into_game_memory()
    {
        const uint ReserveCommitLargePages = 0x20003000;
        const uint GpuReadWrite = 0x2404;
        const ulong Counter = 0x1234_5678_9ABC_DEF0;
        const uint Marker = 0xC0FFEE;

        using var test = TestRuntime.Create();
        var virtualAlloc = (delegate* unmanaged<nint, nuint, uint, ulong, uint, nint>)test.Shim("xmem.dll", "XMemVirtualAlloc");
        byte* memory = (byte*)virtualAlloc(0, 0x20000, ReserveCommitLargePages, 0x283, GpuReadWrite);

        var device = new XboxDevice(test);
        nint queue = device.Queue();
        nint fence = device.Fence();
        nint list = device.DirectList();

        var value = (delegate* unmanaged<nint, ulong, uint, ulong, uint, int>)XboxDevice.Vtable(list)[BottomOfPipeWrites.ValueSlot];
        var marker = (delegate* unmanaged<nint, ulong, uint, ulong, uint, int>)XboxDevice.Vtable(list)[BottomOfPipeWrites.MarkerSlot];
        Assert.Equal(0, value(list, (ulong)(memory + 0x100), 7, Counter, 1));
        Assert.Equal(0, marker(list, (ulong)(memory + 0x200), Marker, 0, 0));

        Assert.Equal(0, ((delegate* unmanaged<nint, int>)XboxDevice.Vtable(list)[CloseSlot])(list));
        nint* lists = stackalloc nint[] { list };
        Assert.Equal(0, ((delegate* unmanaged<nint, uint, nint*, nint, uint, int>)XboxDevice.Vtable(queue)[ExecuteSlot])(queue, 1, lists, 0, 0));
        Assert.Equal(0, ((delegate* unmanaged<nint, nint, ulong, int>)XboxDevice.Vtable(queue)[SignalSlot])(queue, fence, 1));
        Assert.Equal(0, ((delegate* unmanaged<nint, ulong, nint, int>)XboxDevice.Vtable(fence)[FenceWaitSlot])(fence, 1, 0));

        Assert.Equal(Counter, *(ulong*)(memory + 0x100));
        Assert.Equal(Marker, *(uint*)(memory + 0x200));
    }
}
