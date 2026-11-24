// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

namespace FiveStar.Loader.Shims.D3D12X.Binding;

/// <summary>
/// One of the game's shader-visible heaps, or one of its CPU-only view heaps. Its descriptors live in title memory as
/// on the console, one record per descriptor whose dword 0 is the index in the desktop shader-visible heap standing
/// behind it and dword 1 a buffer view's element stride (<see cref="BufferRecordStrides"/>); the game copies these
/// records itself (its per-draw tables, 0x144d9efc0, also from CPU-only heaps), so a table in title memory names
/// desktop descriptors wherever the game put it. For a shader-visible heap the <see cref="DescriptorWindow"/> holds a
/// mirror of dwords 0 and 1 of every record, which programs read through a table's pointer; a record is mirrored when
/// the shim writes it and again when a table that covers it is bound. A CPU-only heap takes fresh desktop descriptors
/// from a <see cref="DescriptorRing"/> on every write and is never mirrored.
/// </summary>
internal sealed unsafe class ShaderVisibleHeap
{
    /// <summary>A shadow value no title record has been mirrored from yet.</summary>
    private const ulong NotMirrored = ulong.MaxValue;

    /// <summary>The record dword holding the stride, in title memory and in the window.</summary>
    private const int StrideDword = 1;

    /// <summary>Where the stride sits in a shadow value, above the desktop index.</summary>
    private const int StrideShift = 32;

    private readonly byte* _title;
    private readonly nint _desktopStart;
    private readonly DescriptorRemap? _remap;
    private readonly DescriptorRing? _ring;
    private readonly DescriptorWindow _window;
    private readonly HeapRecords _layout;

    /// <summary>The dwords 0 and 1 last mirrored to the window, per record.</summary>
    private readonly ulong[] _shadow;

    /// <param name="title">The title memory of the heap (its CPU and GPU start for the game).</param>
    /// <param name="count">Descriptors in the game heap.</param>
    /// <param name="increment">Bytes per descriptor, in title memory as on the desktop.</param>
    /// <param name="desktopStart">The desktop CPU handle of desktop index 0.</param>
    /// <param name="layout">Where the records of the heap lie in the window and which desktop indices they may name.</param>
    /// <param name="remap">Game to desktop indices; null when they are equal.</param>
    /// <param name="window">The window the records are mirrored to.</param>
    /// <param name="ring">For a CPU-only heap, where its writes take desktop descriptors; null for a shader-visible heap.</param>
    public ShaderVisibleHeap(nint title, uint count, uint increment, nint desktopStart, HeapRecords layout, DescriptorRemap? remap, DescriptorWindow window, DescriptorRing? ring)
    {
        _ring = ring;
        _title = (byte*)title;
        Count = count;
        Increment = increment;
        _desktopStart = desktopStart;

        _remap = remap;
        _window = window;
        _layout = layout;

        _shadow = new ulong[count];
        Array.Fill(_shadow, NotMirrored);
    }

    public uint Count { get; }

    public uint Increment { get; }

    public nint Title => (nint)_title;

    /// <summary>Whether programs read the heap through the window (a shader-visible heap).</summary>
    public bool Mirrored => _ring is null;

    /// <summary>The game index of a handle (CPU or GPU, both title memory addresses); false outside the heap.</summary>
    [HotPath]
    public bool TryIndex(ulong handle, out uint index)
    {
        ulong offset = handle - (ulong)_title;
        index = 0;

        if (offset >= (ulong)Count * Increment)
        {
            return false;
        }

        index = (uint)(offset / Increment);

        return true;
    }

    /// <summary>
    /// A new descriptor at <paramref name="index"/> and the <paramref name="count"/> - 1 after it (one desktop run): the
    /// desktop CPU handle to write it to, with its records written (<paramref name="stride"/> for a buffer view, else
    /// 0); 0 when no desktop descriptor is left for it.
    /// </summary>
    [HotPath]
    public nint Destination(uint index, uint count, uint stride)
    {
        uint desktop = _ring is null ? DesktopIndex(index) : _ring.Next(count);

        if (desktop == DescriptorRemap.Unmapped)
        {
            return 0;
        }

        for (uint i = 0; i < count; i++)
        {
            WriteRecord(index + i, desktop + i, stride);
        }

        return _desktopStart + (nint)((ulong)desktop * Increment);
    }

    /// <summary>The desktop CPU handle of the descriptor the record at <paramref name="index"/> names.</summary>
    [HotPath]
    public nint Source(uint index)
    {
        return _desktopStart + (nint)((ulong)Valid(*Record(index)) * Increment);
    }

    /// <summary>Descriptors from <paramref name="index"/> that one desktop run holds.</summary>
    [HotPath]
    public uint RunLength(uint index)
    {
        if (_remap is null || _ring is not null)
        {
            return Count - index;
        }

        return DescriptorRemap.RunLength(index);
    }

    /// <summary>Copies records inside the heap, as the console copies descriptor bytes.</summary>
    [HotPath]
    public void CopyRecords(uint destination, uint source, uint count)
    {
        Buffer.MemoryCopy(Record(source), Record(destination), (ulong)(Count - destination) * Increment, (ulong)count * Increment);
        Mirror(destination, count);
    }

    /// <summary>Copies records from another title-memory heap of the same kind: both name descriptors of the desktop shader-visible heap.</summary>
    [HotPath]
    public void CopyRecords(uint destination, ShaderVisibleHeap source, uint sourceIndex, uint count)
    {
        Buffer.MemoryCopy(source.Record(sourceIndex), Record(destination), (ulong)(Count - destination) * Increment, (ulong)count * Increment);
        Mirror(destination, count);
    }

    /// <summary>A table at <paramref name="index"/> of up to <paramref name="extent"/> descriptors: its records mirrored, its window pointer returned.</summary>
    [HotPath]
    public uint Table(uint index, uint extent)
    {
        uint left = Count - index;

        if (extent > left)
        {
            extent = left;
        }

        Mirror(index, extent);

        return _layout.Base + (index * _layout.RecordBytes);
    }

    [HotPath]
    private uint DesktopIndex(uint index)
    {
        if (_remap is not null)
        {
            return _remap.Map(index);
        }

        if (index >= _layout.DesktopCount)
        {
            return DescriptorRemap.Unmapped;
        }

        return index;
    }

    [HotPath]
    private void WriteRecord(uint index, uint desktop, uint stride)
    {
        uint* record = Record(index);
        new Span<byte>(record, (int)Increment).Clear();
        record[0] = desktop;
        record[StrideDword] = stride;
        MirrorOne(index);
    }

    [HotPath]
    private void Mirror(uint index, uint count)
    {
        for (uint i = index; i < index + count; i++)
        {
            MirrorOne(i);
        }
    }

    [HotPath]
    private void MirrorOne(uint index)
    {
        byte* window = _window.Data;
        uint* record = Record(index);
        ulong value = record[0] | ((ulong)record[StrideDword] << StrideShift);

        if (_ring is not null || _shadow[index] == value || window == null)
        {
            return;
        }

        _shadow[index] = value;
        uint* mirror = (uint*)(window + _layout.Base + ((ulong)index * _layout.RecordBytes));
        mirror[0] = Valid(record[0]);
        mirror[StrideDword] = record[StrideDword];
    }

    /// <summary>A record's desktop index, or the null descriptor for one that names none (a null or unwritten descriptor).</summary>
    [HotPath]
    private uint Valid(uint desktop)
    {
        if (desktop < _layout.FirstDesktop || desktop >= _layout.DesktopCount)
        {
            return _layout.NullIndex;
        }

        return desktop;
    }

    [HotPath]
    private uint* Record(uint index)
    {
        return (uint*)(_title + ((ulong)index * Increment));
    }
}
