// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using FiveStar.Loader.Diagnostics;
using FiveStar.Loader.Native;
using FiveStar.Loader.Shims.D3D12X.Binding;

namespace FiveStar.Loader.Shims.D3D12X;

/// <summary>
/// Device slot 14. A shader-visible heap gets its CPU descriptor range from the title's XMemAlloc hook, as on the
/// console (<see cref="TitleMemoryHooks"/>); the game sees that memory as the heap's CPU start and the desktop heap's
/// handles stand behind it in <see cref="Handles"/>. The game's view descriptors start at desktop index
/// <see cref="DescriptorWindow.ReservedViews"/> (the indices below are the programs' fixed slots), and slot 10 returns
/// the GPU handle of that index, so the game's GPU handles (GPU start + CPU offset) are desktop handles. Other heaps
/// use the desktop handles directly.
/// </summary>
internal sealed unsafe class XboxDescriptorHeaps
{
    /// <summary>ID3D12Device::CreateDescriptorHeap and ::GetDescriptorHandleIncrementSize (d3d12.h).</summary>
    private const int DesktopCreateSlot = 14;
    private const int DesktopIncrementSlot = 15;

    /// <summary>D3D12_DESCRIPTOR_HEAP_FLAG_SHADER_VISIBLE (d3d12.h).</summary>
    private const int ShaderVisible = 1;

    /// <summary>D3D12_DESCRIPTOR_HEAP_TYPE_CBV_SRV_UAV and _SAMPLER (d3d12.h).</summary>
    private const int ViewHeapType = 0;
    private const int SamplerHeapType = 1;

    /// <summary>
    /// XMemAlloc attributes of a descriptor range: allocator id 128 (bits 16..23) and object type 4 (bits 0..13), the
    /// pair the game's hook at 0x145eb2730 answers.
    /// </summary>
    private const ulong DescriptorAttributes = 0x00800004;

    private readonly XboxObjects _objects;
    private readonly TitleMemoryHooks _hooks;
    private readonly DescriptorWindow _window;
    private readonly LoaderLog _log;
    private ulong _unbackedStart;
    private ulong _unbackedEnd;
    private nint _sink;

    public XboxDescriptorHeaps(XboxObjects objects, TitleMemoryHooks hooks, DescriptorWindow window, LoaderLog log)
    {
        _objects = objects;
        _hooks = hooks;
        _window = window;
        _log = log;
    }

    /// <summary>Descriptor handles in title memory to desktop CPU descriptor handles.</summary>
    public AddressTable Handles { get; } = new();

    /// <summary>
    /// The desktop handle behind a CPU descriptor handle the game holds; a descriptor past the desktop heap's end gets
    /// the sink, other handles pass through.
    /// </summary>
    [HotPath]
    public nint Translate(nint handle)
    {
        if (Handles.TryTranslate((ulong)handle, out ulong desktop))
        {
            return (nint)desktop;
        }

        if ((ulong)handle - _unbackedStart < _unbackedEnd - _unbackedStart)
        {
            return _sink;
        }

        return handle;
    }

    public int Create(nint device, DescriptorHeapDescription* description, Guid* iid, nint* heap)
    {
        var desktop = *description;
        bool visible = (desktop.Flags & ShaderVisible) != 0;

        if (visible && desktop.Type == ViewHeapType)
        {
            desktop.Count = (uint)Math.Min((ulong)desktop.Count + DescriptorWindow.ReservedViews, DescriptorWindow.MaxViews);
        }
        else if (visible && desktop.Type == SamplerHeapType)
        {
            // Room for the static samplers of the programs at STATIC_SAMPLER_BASE.
            desktop.Count = Math.Max(desktop.Count, DescriptorWindow.MaxSamplers);
        }

        int hr = CreateDesktop(device, &desktop, iid, heap);

        _log.Detail($"  [d3d12_xs] CreateDescriptorHeap(type {description->Type}, {description->Count} descriptors, flags 0x{description->Flags:x}, node 0x{description->NodeMask:x}) -> {HResult.Format(hr)}");

        if (HResult.Failed(hr) || heap == null || !visible)
        {
            return _objects.WrapResult(hr, XboxInterfaceKind.DescriptorHeap, heap);
        }

        nint inner = *heap;
        *heap = _objects.Wrap(XboxInterfaceKind.DescriptorHeap, inner);
        Place(device, (XboxDescriptorHeapObject*)*heap, description, desktop.Count);

        return hr;
    }

    /// <summary>Puts the heap's CPU descriptors in title memory and sets it up for binding.</summary>
    private void Place(nint device, XboxDescriptorHeapObject* heap, DescriptorHeapDescription* description, uint desktopCount)
    {
        uint increment = ((delegate* unmanaged<nint, int, uint>)ComObject.Slot(device, DesktopIncrementSlot))(device, description->Type);
        nint inner = heap->Header.Inner;
        uint first = 0;

        if (description->Type == ViewHeapType && _window.ViewHeap == 0 && _window.AttachViews(device, inner, desktopCount, increment))
        {
            first = DescriptorWindow.ReservedViews;
            heap->GameGpuStart = (nint)(DesktopCalls.HeapGpuStart(inner) + ((ulong)first * increment));
        }
        else if (description->Type == SamplerHeapType && _window.SamplerHeap == 0)
        {
            _window.AttachSamplers(device, inner, desktopCount, increment);
        }

        nint memory = _hooks.Allocate((nuint)description->Count * increment, DescriptorAttributes);
        nint desktopStart = DesktopCalls.HeapCpuStart(inner) + (nint)((ulong)first * increment);

        if (memory == 0)
        {
            // No title hooks (no game image): the game's CPU start is the first desktop descriptor it owns.
            heap->GameCpuStart = first == 0 ? 0 : desktopStart;

            return;
        }

        ulong backed = Math.Min(description->Count, desktopCount - first);
        heap->GameCpuStart = memory;
        Handles.Add(new AddressRange((ulong)memory, backed * increment, (ulong)desktopStart));
        _log.Detail($"  [d3d12_xs]   CPU descriptors at title memory 0x{memory:x} (desktop 0x{desktopStart:x}, from index {first})");

        if (backed < description->Count && first != 0)
        {
            // Descriptors the desktop heap has no room for: written to the sink, read as null descriptors.
            _unbackedStart = (ulong)memory + (backed * increment);
            _unbackedEnd = (ulong)memory + ((ulong)description->Count * increment);
            _sink = DesktopCalls.HeapCpuStart(inner) + (nint)((ulong)DescriptorWindow.SinkSlot * increment);
            _log.Warn($"[d3d12_xs] descriptor heap of {description->Count} descriptors: the last {description->Count - backed} have no desktop descriptor (the desktop takes {DescriptorWindow.MaxViews}, {first} of them reserved)");
        }
    }

    private static int CreateDesktop(nint device, DescriptorHeapDescription* description, Guid* iid, nint* heap)
    {
        var create = (delegate* unmanaged<nint, DescriptorHeapDescription*, Guid*, nint*, int>)ComObject.Slot(device, DesktopCreateSlot);

        return create(device, description, iid, heap);
    }
}
