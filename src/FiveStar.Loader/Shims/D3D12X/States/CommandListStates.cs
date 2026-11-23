// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Collections.Concurrent;
using FiveStar.Loader.Diagnostics;
using FiveStar.Loader.Native;

namespace FiveStar.Loader.Shims.D3D12X.States;

/// <summary>
/// The <see cref="ListResourceStates"/> of every command-list wrapper that recorded a barrier, and whether the device
/// takes enhanced barriers (D3D12_FEATURE_D3D12_OPTIONS12.EnhancedBarriersSupported), asked once.
/// </summary>
internal sealed unsafe class CommandListStates
{
    /// <summary>ID3D12Device::CheckFeatureSupport and D3D12_FEATURE_D3D12_OPTIONS12 (d3d12.h): three dwords, the second EnhancedBarriersSupported.</summary>
    private const int CheckFeatureSupportSlot = 13;
    private const int Options12Feature = 41;
    private const uint Options12Size = 12;
    private const int EnhancedBarriersDword = 1;

    private const int Unknown = 0;
    private const int Supported = 1;
    private const int Unsupported = 2;

    private static readonly Guid CommandList7Iid = new("dd171223-8b61-4769-90e3-160ccde4e2c1");

    private readonly ConcurrentDictionary<nint, ListResourceStates> _lists = new();
    private readonly XboxObjects _objects;
    private readonly LoaderLog _log;
    private int _enhanced;

    public CommandListStates(XboxObjects objects, LoaderLog log)
    {
        _objects = objects;
        _log = log;
    }

    /// <summary>Whether transitions are written as enhanced barriers; false until a device answers.</summary>
    public bool EnhancedBarriers => Volatile.Read(ref _enhanced) == Supported;

    /// <summary>The states of <paramref name="list"/>, made on its first barrier, with its enhanced interface current.</summary>
    [HotPath]
    public ListResourceStates For(XboxObject* list)
    {
        if (!_lists.TryGetValue((nint)list, out var states))
        {
            states = Add(list);
        }

        nint inner = list->Inner;

        if (states.Desktop != inner)
        {
            states.Desktop = inner;
            states.Enhanced = EnhancedInterface(inner);
        }

        return states;
    }

    /// <summary>The states of <paramref name="list"/> when it ever recorded a barrier, else null.</summary>
    [HotPath]
    public ListResourceStates? Find(XboxObject* list)
    {
        _lists.TryGetValue((nint)list, out var states);

        return states;
    }

    private ListResourceStates Add(XboxObject* list)
    {
        return _lists.GetOrAdd((nint)list, new ListResourceStates());
    }

    private nint EnhancedInterface(nint inner)
    {
        if (!AskSupport())
        {
            return 0;
        }

        Guid iid = CommandList7Iid;
        nint list7 = 0;
        var query = (delegate* unmanaged<nint, Guid*, nint*, int>)ComObject.Slot(inner, ComObject.QueryInterfaceSlot);

        if (HResult.Failed(query(inner, &iid, &list7)) || list7 == 0)
        {
            return 0;
        }

        // The list keeps the interface alive; the reference QueryInterface added is given back at once.
        ComObject.Release(list7);

        return list7;
    }

    private bool AskSupport()
    {
        int known = Volatile.Read(ref _enhanced);

        if (known != Unknown)
        {
            return known == Supported;
        }

        nint device = _objects.DesktopDevice;

        if (device == 0)
        {
            return false;
        }

        uint* data = stackalloc uint[] { 0, 0, 0 };
        var check = (delegate* unmanaged<nint, int, void*, uint, int>)ComObject.Slot(device, CheckFeatureSupportSlot);
        bool supported = !HResult.Failed(check(device, Options12Feature, data, Options12Size)) && data[EnhancedBarriersDword] != 0;
        int state = Unsupported;

        if (supported)
        {
            state = Supported;
        }

        Volatile.Write(ref _enhanced, state);
        string kind = supported ? "enhanced (ID3D12GraphicsCommandList7::Barrier)" : "legacy ResourceBarrier";
        _log.Info($"  [d3d12_xs] barriers: {kind}");

        return supported;
    }
}
