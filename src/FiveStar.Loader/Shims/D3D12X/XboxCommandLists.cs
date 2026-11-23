// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Runtime.InteropServices;
using FiveStar.Loader.Codegen;
using FiveStar.Loader.Diagnostics;
using FiveStar.Loader.Loading;
using FiveStar.Loader.Native;

namespace FiveStar.Loader.Shims.D3D12X;

/// <summary>
/// Command lists for device slot 73. The game gets a wrapper (<see cref="XboxDeviceObject"/> layout) around a desktop
/// ID3D12GraphicsCommandList: slots 0 to 9 (IUnknown, ID3D12Object, GetDevice, GetType, Close) forward, every later
/// slot is a logged stub until the X recording slots are mapped (docs/d3d12x-shim.md).
/// </summary>
internal sealed unsafe class XboxCommandLists
{
    /// <summary>ID3D12Device::CreateCommandList in the desktop vtable (d3d12.h).</summary>
    private const int DesktopCreateCommandListSlot = 12;

    private readonly object _gate = new();
    private readonly UnresolvedStubs _stubs;
    private readonly CodeHeap _heap;
    private readonly LoaderLog _log;
    private nint _vtable;

    public XboxCommandLists(UnresolvedStubs stubs, CodeHeap heap, LoaderLog log)
    {
        _stubs = stubs;
        _heap = heap;
        _log = log;
    }

    /// <summary>Creates a desktop list of the desc's type on <paramref name="device"/> and returns it wrapped.</summary>
    public int Create(nint device, CommandListDescription* description, nint allocator, nint pipelineState, nint* list)
    {
        if (description == null || list == null)
        {
            return HResult.Pointer;
        }

        *list = 0;
        Guid iid = D3D12XLayout.GraphicsCommandListIid;
        nint inner;
        var create = (delegate* unmanaged<nint, uint, int, nint, nint, Guid*, nint*, int>)(*(nint**)device)[DesktopCreateCommandListSlot];
        int hr = create(device, 0, description->Type, allocator, pipelineState, &iid, &inner);
        _log.Detail($"  [d3d12_xs] ID3D12Device.X::Slot73 CreateCommandList(type {description->Type}, dword1 {description->Unknown1}, dword2 {description->Unknown2}, dword3 {description->Unknown3}) -> {HResult.Format(hr)}");

        if (HResult.Failed(hr))
        {
            return hr;
        }

        var wrapper = (XboxDeviceObject*)NativeMemory.AllocZeroed((nuint)sizeof(XboxDeviceObject));
        wrapper->Vtable = Vtable();
        wrapper->Inner = inner;
        *list = (nint)wrapper;

        return HResult.Ok;
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
        nint* vtable = (nint*)NativeMemory.AllocZeroed((nuint)(D3D12XLayout.CommandListSlotCount * sizeof(nint)));

        for (int slot = 0; slot < D3D12XLayout.CommandListForwardedSlots; slot++)
        {
            vtable[slot] = ForwardingThunk.Emit(_heap, slot, false);
        }

        for (int slot = D3D12XLayout.CommandListForwardedSlots; slot < D3D12XLayout.CommandListSlotCount; slot++)
        {
            vtable[slot] = _stubs.Create(D3D12XLayout.Module, $"ID3D12GraphicsCommandList.X::Slot{slot}", HResult.NotImplemented, true, false);
        }

        _heap.Seal();

        return (nint)vtable;
    }
}
