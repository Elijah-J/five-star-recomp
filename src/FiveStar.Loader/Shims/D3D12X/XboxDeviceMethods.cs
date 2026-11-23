// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Runtime.InteropServices;
using FiveStar.Loader.Native;

namespace FiveStar.Loader.Shims.D3D12X;

/// <summary>D3D12.X-only device slots that translate to a desktop call (creation calls, not on the per-frame path).</summary>
internal static unsafe class XboxDeviceMethods
{
    /// <summary>D3D12_COMMAND_QUEUE_FLAG_DISABLE_GPU_TIMEOUT, the only desktop queue flag (d3d12.h).</summary>
    private const int DesktopQueueFlags = 1;

    /// <summary>ID3D12Device::CreateCommandQueue in the desktop vtable (d3d12.h).</summary>
    private const int DesktopCreateCommandQueueSlot = 8;

    /// <summary>
    /// Slot 48: (desc, REFIID, void**) like the desktop CreateCommandQueue. The game asks for compute queues with
    /// flags 1 and 2 and node mask 0; flag bits the desktop does not define are dropped and node 0 means the one GPU.
    /// </summary>
    [UnmanagedCallersOnly]
    public static int CreateCommandQueue(XboxDeviceObject* self, CommandQueueDescription* description, Guid* iid, nint* queue)
    {
        if (self == null || description == null)
        {
            return HResult.InvalidArgument;
        }

        var desktop = *description;
        desktop.Flags &= DesktopQueueFlags;
        nint inner = self->Inner;
        var create = (delegate* unmanaged<nint, CommandQueueDescription*, Guid*, nint*, int>)(*(nint**)inner)[DesktopCreateCommandQueueSlot];
        int hr = create(inner, &desktop, iid, queue);

        string desc = $"type {description->Type}, priority {description->Priority}, flags 0x{description->Flags:x}, node 0x{description->NodeMask:x}";
        ShimContext.Runtime.Log.Detail($"  [d3d12_xs] ID3D12Device.X::Slot48 CreateCommandQueue({desc}) -> {HResult.Format(hr)}");

        return hr;
    }

    /// <summary>
    /// Slot 78, read as SetFrameIntervalX (*inferred*: null output, interval length, period 2 or 3, flags 0). Frame
    /// pacing on the PC comes from the swap chain, so the interval is only logged.
    /// </summary>
    [UnmanagedCallersOnly]
    public static int SetFrameInterval(XboxDeviceObject* self, nint output, uint length, uint period, uint flags)
    {
        ShimContext.Runtime.Log.Detail($"  [d3d12_xs] ID3D12Device.X::Slot78 SetFrameInterval(output 0x{output:x}, length {length}, period {period}, flags 0x{flags:x}) -> S_OK (no-op)");

        return HResult.Ok;
    }

    /// <summary>Slot 79, read as ScheduleFrameEventX (*inferred*: event type, delta, null output, flags 0); logged only.</summary>
    [UnmanagedCallersOnly]
    public static int ScheduleFrameEvent(XboxDeviceObject* self, uint type, uint delta, nint output, uint flags)
    {
        ShimContext.Runtime.Log.Detail($"  [d3d12_xs] ID3D12Device.X::Slot79 ScheduleFrameEvent(type {type}, delta {delta}, output 0x{output:x}, flags 0x{flags:x}) -> S_OK (no-op)");

        return HResult.Ok;
    }

    /// <summary>Slot 73: (desc, allocator, PSO, REFIID, void**). The list is wrapped whatever IID the game asks for.</summary>
    [UnmanagedCallersOnly]
    public static int CreateCommandList(XboxDeviceObject* self, CommandListDescription* description, nint allocator, nint pipelineState, Guid* iid, nint* list)
    {
        if (self == null)
        {
            return HResult.InvalidArgument;
        }

        return ShimContext.Runtime.D3D12X.CommandLists.Create(self->Inner, description, allocator, pipelineState, list);
    }
}
