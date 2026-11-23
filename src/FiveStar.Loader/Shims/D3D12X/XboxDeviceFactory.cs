// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Runtime.InteropServices;
using System.Text;
using FiveStar.Loader.Codegen;
using FiveStar.Loader.Diagnostics;
using FiveStar.Loader.Loading;
using FiveStar.Loader.Native;

namespace FiveStar.Loader.Shims.D3D12X;

/// <summary>
/// D3D12XboxCreateDevice on a desktop D3D12 device. The game gets an <see cref="XboxDeviceObject"/> whose vtable
/// forwards the desktop-compatible slots to the desktop device through generated tail-call thunks (no managed code on
/// the call path), and binds every D3D12.X-only slot to a stub that logs its first call and returns E_NOTIMPL.
/// </summary>
internal sealed unsafe class XboxDeviceFactory
{
    private readonly object _gate = new();
    private readonly D3D12Forwarder _d3d12;
    private readonly UnresolvedStubs _stubs;
    private readonly CodeHeap _heap;
    private readonly LoaderLog _log;
    private nint _vtable;

    public XboxDeviceFactory(D3D12Forwarder d3d12, UnresolvedStubs stubs, CodeHeap heap, LoaderLog log)
    {
        _d3d12 = d3d12;
        _stubs = stubs;
        _heap = heap;
        _log = log;
        CommandLists = new XboxCommandLists(stubs, heap, log);
    }

    /// <summary>The command-list wrappers device slot 73 hands out.</summary>
    public XboxCommandLists CommandLists { get; }

    /// <summary>HRESULT D3D12XboxCreateDevice(IGraphicsUnknown* adapter, const PARAMETERS*, REFIID, void** device).</summary>
    public int CreateDevice(nint adapter, byte* parameters, Guid* iid, nint* device)
    {
        if (device == null)
        {
            return HResult.Pointer;
        }

        *device = 0;
        LogParameters(adapter, parameters, iid);
        Guid desktopIid = D3D12XLayout.DeviceIid;
        nint inner;
        int hr = _d3d12.CreateDevice(D3D12XLayout.MinimumFeatureLevel, &desktopIid, &inner);

        if (HResult.Failed(hr))
        {
            _log.Error($"  [d3d12_xs] D3D12CreateDevice failed: {HResult.Format(hr)}");

            return hr;
        }

        var wrapper = (XboxDeviceObject*)NativeMemory.AllocZeroed((nuint)sizeof(XboxDeviceObject));
        wrapper->Vtable = Vtable();
        wrapper->Inner = inner;
        *device = (nint)wrapper;

        return HResult.Ok;
    }

    /// <summary>
    /// HRESULT D3D12ConfigureVirtualMemoryX(const CONFIGURATION*). The desktop device manages its own GPU virtual
    /// addresses, so the call only logs what the game asked for.
    /// </summary>
    public int ConfigureVirtualMemory(byte* configuration)
    {
        uint count = 0;

        if (configuration != null)
        {
            count = *(uint*)(configuration + D3D12XLayout.VirtualMemoryCountOffset);
        }

        _log.Detail($"  [d3d12_xs] D3D12ConfigureVirtualMemoryX(dword@4={count}) -> S_OK (no-op)");

        return HResult.Ok;
    }

    private void LogParameters(nint adapter, byte* parameters, Guid* iid)
    {
        if (!_log.DetailEnabled)
        {
            return;
        }

        var text = new StringBuilder($"  [d3d12_xs] D3D12XboxCreateDevice(adapter=0x{adapter:x}");

        if (iid != null)
        {
            text.Append($", iid={*iid}");
        }

        if (parameters != null)
        {
            text.Append(", parameters=");

            for (int offset = 0; offset < D3D12XLayout.ParametersSize; offset += sizeof(uint))
            {
                text.Append($" {*(uint*)(parameters + offset):x8}");
            }
        }

        _log.Detail(text.Append(')').ToString());
    }

    private nint Vtable()
    {
        lock (_gate)
        {
            if (_vtable == 0)
            {
                _vtable = BuildVtable();
            }

            return _vtable;
        }
    }

    private nint BuildVtable()
    {
        nint* vtable = (nint*)NativeMemory.AllocZeroed((nuint)(D3D12XLayout.DeviceSlotCount * sizeof(nint)));

        for (int slot = 0; slot < D3D12XLayout.DesktopDeviceSlotCount; slot++)
        {
            vtable[slot] = ForwardingThunk.Emit(_heap, slot, Array.IndexOf(D3D12XLayout.StructReturnSlots, slot) >= 0);
        }

        for (int slot = D3D12XLayout.DesktopDeviceSlotCount; slot < D3D12XLayout.DeviceSlotCount; slot++)
        {
            vtable[slot] = _stubs.Create(D3D12XLayout.Module, $"ID3D12Device.X::Slot{slot}", HResult.NotImplemented, true, false);
        }

        BindXboxSlots(vtable);

        // One read-write to read-execute flip for the thunks and the stubs.
        _heap.Seal();
        _log.Detail($"  [d3d12_xs] device vtable: {D3D12XLayout.DesktopDeviceSlotCount} forwarded slots, {D3D12XLayout.DeviceSlotCount - D3D12XLayout.DesktopDeviceSlotCount} logged stubs");

        return (nint)vtable;
    }

    /// <summary>The X-only slots with a desktop translation (<see cref="XboxDeviceMethods"/>) replace their stubs.</summary>
    private static void BindXboxSlots(nint* vtable)
    {
        delegate* unmanaged<XboxDeviceObject*, CommandQueueDescription*, Guid*, nint*, int> createQueue = &XboxDeviceMethods.CreateCommandQueue;
        delegate* unmanaged<XboxDeviceObject*, CommandListDescription*, nint, nint, Guid*, nint*, int> createList = &XboxDeviceMethods.CreateCommandList;
        delegate* unmanaged<XboxDeviceObject*, nint, uint, uint, uint, int> frameInterval = &XboxDeviceMethods.SetFrameInterval;
        delegate* unmanaged<XboxDeviceObject*, uint, uint, nint, uint, int> frameEvent = &XboxDeviceMethods.ScheduleFrameEvent;

        vtable[D3D12XLayout.CreateCommandQueueSlot] = (nint)createQueue;
        vtable[D3D12XLayout.CreateCommandListSlot] = (nint)createList;
        vtable[D3D12XLayout.SetFrameIntervalSlot] = (nint)frameInterval;
        vtable[D3D12XLayout.ScheduleFrameEventSlot] = (nint)frameEvent;
    }
}
