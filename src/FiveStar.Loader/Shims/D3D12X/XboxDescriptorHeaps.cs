// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using FiveStar.Loader.Diagnostics;
using FiveStar.Loader.Native;

namespace FiveStar.Loader.Shims.D3D12X;

/// <summary>
/// Device slot 14. A shader-visible heap gets its CPU descriptor range from the title's XMemAlloc hook, as on the
/// console (<see cref="TitleMemoryHooks"/>); the game sees that memory as the heap's CPU start and the desktop heap's
/// handles stand behind it in <see cref="Handles"/>. The GPU start stays the desktop one, so the game's GPU handles
/// (GPU start + CPU offset) are desktop handles. Other heaps use the desktop handles directly.
/// </summary>
internal sealed unsafe class XboxDescriptorHeaps
{
    /// <summary>ID3D12Device::CreateDescriptorHeap and ::GetDescriptorHandleIncrementSize (d3d12.h).</summary>
    private const int DesktopCreateSlot = 14;
    private const int DesktopIncrementSlot = 15;

    /// <summary>ID3D12DescriptorHeap::GetCPUDescriptorHandleForHeapStart (d3d12.h).</summary>
    private const int DesktopCpuStartSlot = 9;

    /// <summary>D3D12_MAX_SHADER_VISIBLE_DESCRIPTOR_HEAP_SIZE_TIER_2 (d3d12.h).</summary>
    private const uint MaxShaderVisibleViews = 1000000;

    /// <summary>D3D12_DESCRIPTOR_HEAP_FLAG_SHADER_VISIBLE (d3d12.h).</summary>
    private const int ShaderVisible = 1;

    /// <summary>
    /// XMemAlloc attributes of a descriptor range: allocator id 128 (bits 16..23) and object type 4 (bits 0..13), the
    /// pair the game's hook at 0x145eb2730 answers.
    /// </summary>
    private const ulong DescriptorAttributes = 0x00800004;

    private readonly XboxObjects _objects;
    private readonly TitleMemoryHooks _hooks;
    private readonly LoaderLog _log;

    public XboxDescriptorHeaps(XboxObjects objects, TitleMemoryHooks hooks, LoaderLog log)
    {
        _objects = objects;
        _hooks = hooks;
        _log = log;
    }

    /// <summary>Descriptor handles in title memory to desktop CPU descriptor handles.</summary>
    public AddressTable Handles { get; } = new();

    /// <summary>The desktop handle behind a CPU descriptor handle the game holds (other handles pass through).</summary>
    [HotPath]
    public nint Translate(nint handle)
    {
        if (Handles.TryTranslate((ulong)handle, out ulong desktop))
        {
            return (nint)desktop;
        }

        return handle;
    }

    public int Create(nint device, DescriptorHeapDescription* description, Guid* iid, nint* heap)
    {
        var desktop = *description;
        int hr = CreateDesktop(device, &desktop, iid, heap);

        if (hr == HResult.InvalidArgument && desktop.Count > MaxShaderVisibleViews)
        {
            // The game's shader-visible CBV/SRV/UAV heap has 1245184 descriptors, above the tier-2 limit: the heap
            // shrinks to the limit and descriptors past it are unusable.
            desktop.Count = MaxShaderVisibleViews;
            hr = CreateDesktop(device, &desktop, iid, heap);
            _log.Warn($"[d3d12_xs] descriptor heap of {description->Count} descriptors shrunk to {desktop.Count}: the desktop device takes no more");
        }

        _log.Detail($"  [d3d12_xs] CreateDescriptorHeap(type {description->Type}, {description->Count} descriptors, flags 0x{description->Flags:x}, node 0x{description->NodeMask:x}) -> {HResult.Format(hr)}");

        if (HResult.Failed(hr) || heap == null || (desktop.Flags & ShaderVisible) == 0)
        {
            return _objects.WrapResult(hr, XboxInterfaceKind.DescriptorHeap, heap);
        }

        nint inner = *heap;
        *heap = _objects.Wrap(XboxInterfaceKind.DescriptorHeap, inner);
        PlaceInTitleMemory(device, (XboxDescriptorHeapObject*)*heap, description, desktop.Count);

        return hr;
    }

    private void PlaceInTitleMemory(nint device, XboxDescriptorHeapObject* heap, DescriptorHeapDescription* description, uint desktopCount)
    {
        uint increment = ((delegate* unmanaged<nint, int, uint>)ComObject.Slot(device, DesktopIncrementSlot))(device, description->Type);
        nint memory = _hooks.Allocate((nuint)description->Count * increment, DescriptorAttributes);

        if (memory == 0)
        {
            return;
        }

        nint inner = heap->Header.Inner;
        nint desktopStart = 0;
        _ = ((delegate* unmanaged<nint, nint*, nint*>)ComObject.Slot(inner, DesktopCpuStartSlot))(inner, &desktopStart);
        heap->GameCpuStart = memory;
        Handles.Add(new AddressRange((ulong)memory, (ulong)desktopCount * increment, (ulong)desktopStart));
        _log.Detail($"  [d3d12_xs]   CPU descriptors at title memory 0x{memory:x} (desktop 0x{desktopStart:x})");
    }

    private static int CreateDesktop(nint device, DescriptorHeapDescription* description, Guid* iid, nint* heap)
    {
        var create = (delegate* unmanaged<nint, DescriptorHeapDescription*, Guid*, nint*, int>)ComObject.Slot(device, DesktopCreateSlot);

        return create(device, description, iid, heap);
    }
}
