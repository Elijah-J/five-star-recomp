// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using FiveStar.Loader.Diagnostics;
using FiveStar.Loader.Native;
using FiveStar.Loader.Shims.D3D12X.Pipelines;
using FiveStar.Shaders.Translation;

namespace FiveStar.Loader.Shims.D3D12X.Binding;

/// <summary>
/// The address map through which translated programs read game memory at console GPU addresses (root descriptors,
/// V#s and pointers the game builds; rdna_prelude <c>gpu_page</c>, <see cref="AddressMap"/>). Every desktop buffer
/// the address table gains gets a raw SRV and UAV per 256 MiB of it, each view covering up to 512 MiB (the most a
/// raw view's element count reaches), and the map names for each 64 KiB page the views of its chunk. The map lives in
/// write-combined system memory read at heap slot <see cref="DescriptorWindow.AddressMapSlot"/>; the views take whole
/// remap pages of the desktop heap. Ranges added before the game's view heap exists wait for it.
/// </summary>
internal sealed unsafe class GameMemoryViews
{
    /// <summary>4 GiB spans the map can hold: the boot's game memory spans about 20.</summary>
    private const int MapSpans = 64;

    /// <summary>Remap pages for the views: 16,384 descriptors, 8,192 SRV/UAV pairs.</summary>
    private const uint DescriptorPages = 4;

    private const ulong ChunkBytes = 256UL << 20;
    private const ulong ViewBytes = 512UL << 20;
    private const uint DwordBytes = 4;
    private const uint PairSize = 2;
    private const int CommonState = 0;

    /// <summary>ID3D12Resource::GetDesc and D3D12_RESOURCE_FLAG_ALLOW_UNORDERED_ACCESS (d3d12.h).</summary>
    private const int GetDescSlot = 10;
    private const int AllowUnorderedAccess = 0x4;

    private readonly object _gate = new();
    private readonly List<AddressRange> _pending = [];
    private readonly AddressMap _map = new(MapSpans, DescriptorWindow.UnmappedSlot);
    private readonly OnceLog _warnings;
    private readonly LoaderLog _log;
    private nint _device;
    private nint _cpuStart;
    private uint _increment;
    private byte* _data;
    private uint _next;
    private uint _end;

    public GameMemoryViews(OnceLog warnings, LoaderLog log)
    {
        _warnings = warnings;
        _log = log;
    }

    /// <summary>Maps the pages of a range the address table gained (<see cref="AddressTable.Observe"/>).</summary>
    public void Add(AddressRange range)
    {
        if (range.Resource == 0)
        {
            return;
        }

        lock (_gate)
        {
            if (_data == null)
            {
                _pending.Add(range);

                return;
            }

            Place(range);
        }
    }

    /// <summary>
    /// Creates the map for the desktop view heap at <paramref name="cpuStart"/>, takes its views' descriptors from
    /// <paramref name="remap"/> and maps the ranges added so far.
    /// </summary>
    public void Attach(nint device, nint cpuStart, uint increment, DescriptorRemap remap)
    {
        lock (_gate)
        {
            var properties = new HeapProperties { Type = HeapProperties.Custom, CpuPageProperty = HeapProperties.WriteCombine, MemoryPool = HeapProperties.SystemPool };
            nint buffer = DesktopCalls.CreateBuffer(device, properties, (ulong)_map.Bytes, 0, CommonState);
            byte* data = buffer == 0 ? null : DesktopCalls.Map(buffer);
            uint first = remap.Reserve(DescriptorPages);

            if (data == null || first == DescriptorRemap.Unmapped)
            {
                _log.Warn("[d3d12_xs] address map: no buffer or descriptors; programs read game memory at console addresses as zeros");

                return;
            }

            _map.Clear(new Span<byte>(data, _map.Bytes));
            var view = BufferViewDescription.RawShaderResource((uint)_map.Bytes / DwordBytes);
            DesktopCalls.ShaderResourceView(device, buffer, &view, cpuStart + (nint)(DescriptorWindow.AddressMapSlot * increment));

            _device = device;
            _cpuStart = cpuStart;
            _increment = increment;
            _data = data;

            _next = first;
            _end = first + (DescriptorPages * DescriptorRemap.PageSize);
            _log.Detail($"  [d3d12_xs] address map: 0x{_map.Bytes:x} bytes for {MapSpans} spans, views from desktop index {first}, {_pending.Count} ranges waiting");

            foreach (var range in _pending)
            {
                Place(range);
            }

            _pending.Clear();
        }
    }

    /// <summary>Views over each chunk of the range's buffer and the map entries of its pages.</summary>
    private void Place(AddressRange range)
    {
        bool writable = (DesktopFlags(range.Resource) & AllowUnorderedAccess) != 0;
        var map = new Span<byte>(_data, _map.Bytes);

        for (ulong start = 0; start < range.Size; start += ChunkBytes)
        {
            if (_next + PairSize > _end)
            {
                _warnings.Warn("address-map-views", "[d3d12_xs] address map: no descriptors left; later game memory reads as zeros");

                return;
            }

            uint pair = _next;
            _next += PairSize;
            WriteViews(range.Resource, start, Math.Min(ViewBytes, range.Size - start), pair, writable);

            if (!_map.Map(map, range.GameAddress + start, Math.Min(ChunkBytes, range.Size - start), pair, range.GameAddress + start))
            {
                _warnings.Warn("address-map-spans", $"[d3d12_xs] address map: no page table left for 0x{range.GameAddress + start:x}; it reads as zeros");
            }
        }
    }

    private void WriteViews(nint resource, ulong offset, ulong bytes, uint pair, bool writable)
    {
        uint dwords = (uint)(bytes / DwordBytes);
        var read = BufferViewDescription.RawShaderResource(dwords, offset / DwordBytes);
        var write = BufferViewDescription.RawUnorderedAccess(dwords, offset / DwordBytes);
        nint handle = _cpuStart + (nint)(pair * _increment);
        DesktopCalls.ShaderResourceView(_device, resource, &read, handle);

        if (!writable)
        {
            // An upload buffer (list slot 148) takes no UAV: writes there go nowhere.
            write = BufferViewDescription.RawUnorderedAccess(0);
            resource = 0;
        }

        DesktopCalls.UnorderedAccessView(_device, resource, &write, handle + (nint)_increment);
    }

    /// <summary>The desktop resource's D3D12_RESOURCE_FLAGS.</summary>
    private static int DesktopFlags(nint resource)
    {
        ResourceDescription desc;
        _ = ((delegate* unmanaged<nint, ResourceDescription*, ResourceDescription*>)ComObject.Slot(resource, GetDescSlot))(resource, &desc);

        return desc.Flags;
    }
}
