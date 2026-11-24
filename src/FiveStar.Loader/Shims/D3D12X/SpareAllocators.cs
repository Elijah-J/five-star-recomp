// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using FiveStar.Loader.Native;

namespace FiveStar.Loader.Shims.D3D12X;

/// <summary>
/// Command allocators lent to a list whose Reset the desktop refuses with E_INVALIDARG because the game's allocator
/// already backs another list that is recording: the game takes a list and an allocator from separate pools
/// (0x140188430) and the console lets lists record on one allocator together; a failed Reset breaks into the debugger
/// at 0x142474ce1. Each game allocator keeps its own spares, reset with it (the game resets an allocator once the GPU
/// is done with every list recorded on it), so a spare is reused, not leaked.
/// </summary>
internal sealed unsafe class SpareAllocators
{
    /// <summary>ID3D12DeviceChild::GetDevice, ID3D12CommandList::GetType, ID3D12Device::CreateCommandAllocator, ID3D12CommandAllocator::Reset (d3d12.h).</summary>
    private const int GetDeviceSlot = 7;
    private const int GetTypeSlot = 8;
    private const int CreateCommandAllocatorSlot = 9;
    private const int AllocatorResetSlot = 8;

    private static readonly Guid DeviceIid = new("189819f1-1db6-4b57-be54-1821339b85f7");
    private static readonly Guid AllocatorIid = new("6102dee4-af59-4b09-b999-b44d73f09b24");

    private readonly object _gate = new();
    private readonly Dictionary<nint, List<nint>> _spares = [];

    /// <summary>Lists that recorded on a spare.</summary>
    public long Lent { get; private set; }

    /// <summary>
    /// Resets desktop <paramref name="list"/> on a spare of desktop <paramref name="allocator"/> through
    /// <paramref name="reset"/> (ID3D12GraphicsCommandList::Reset): the first spare that takes it, else a new one.
    /// </summary>
    public int Reset(nint list, nint allocator, nint pipeline, delegate* unmanaged<nint, nint, nint, int> reset)
    {
        lock (_gate)
        {
            if (!_spares.TryGetValue(allocator, out var spares))
            {
                spares = [];
                _spares[allocator] = spares;
            }

            Lent++;

            foreach (nint spare in spares)
            {
                if (!HResult.Failed(reset(list, spare, pipeline)))
                {
                    return HResult.Ok;
                }
            }

            nint created = Create(list);
            int hr = HResult.InvalidArgument;

            if (created != 0)
            {
                spares.Add(created);
                hr = reset(list, created, pipeline);
            }

            if (HResult.Failed(hr))
            {
                int type = ((delegate* unmanaged<nint, int>)ComObject.Slot(list, GetTypeSlot))(list);
                ShimContext.Runtime.D3D12X.Warnings.Warn("spare-allocator", $"[d3d12_xs] command list Reset refused on a fresh allocator too: list type {type}, allocator created {created != 0}, {spares.Count} spares, pipeline 0x{pipeline:x}: {HResult.Format(hr)}");
            }

            return hr;
        }
    }

    /// <summary>Resets the spares of desktop <paramref name="allocator"/>; one a list still records on keeps its memory.</summary>
    public void ResetSpares(nint allocator)
    {
        lock (_gate)
        {
            if (!_spares.TryGetValue(allocator, out var spares))
            {
                return;
            }

            foreach (nint spare in spares)
            {
                _ = ((delegate* unmanaged<nint, int>)ComObject.Slot(spare, AllocatorResetSlot))(spare);
            }
        }
    }

    /// <summary>A new allocator of <paramref name="list"/>'s type on its device; 0 when the desktop refuses.</summary>
    private static nint Create(nint list)
    {
        Guid deviceIid = DeviceIid;
        nint device = 0;

        if (HResult.Failed(((delegate* unmanaged<nint, Guid*, nint*, int>)ComObject.Slot(list, GetDeviceSlot))(list, &deviceIid, &device)))
        {
            return 0;
        }

        int type = ((delegate* unmanaged<nint, int>)ComObject.Slot(list, GetTypeSlot))(list);
        Guid allocatorIid = AllocatorIid;
        nint allocator = 0;
        int hr = ((delegate* unmanaged<nint, int, Guid*, nint*, int>)ComObject.Slot(device, CreateCommandAllocatorSlot))(device, type, &allocatorIid, &allocator);
        ComObject.Release(device);

        if (HResult.Failed(hr))
        {
            return 0;
        }

        return allocator;
    }
}
