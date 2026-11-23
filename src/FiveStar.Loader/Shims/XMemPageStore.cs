// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using FiveStar.Loader.Native;

namespace FiveStar.Loader.Shims;

/// <summary>
/// The console's physical memory: 64 KiB pages carved from pagefile-backed sections of <see cref="ChunkBytes"/>, created
/// as pages are first handed out. A page is a fixed offset in one section, so every view of it shares its contents.
/// Page frame numbers count 4 KiB frames like the console's (page index times 16, never 0). Allocation and freeing
/// lock; locating a frame does not and never allocates.
/// </summary>
internal sealed unsafe class XMemPageStore : IDisposable
{
    /// <summary>A physical page: 64 KiB (the GDK memory overview), which is also the Windows allocation granularity.</summary>
    public const ulong PageSize = 0x10000;

    /// <summary>Frames per page: the game turns a frame number into a page index with bits 4..35 (bextr 0x2004).</summary>
    public const ulong FramesPerPage = PageSize / Win32Constants.PageSize;

    /// <summary>Pages per section: 64 MiB of commit charge each time the store grows.</summary>
    private const ulong PagesPerChunk = 1024;

    private const ulong ChunkBytes = PagesPerChunk * PageSize;

    /// <summary>The game packs page indices into 21-bit fields (mask 0x1FFFFF): at most 2^21 pages, 128 GiB.</summary>
    private const int PageIndexBits = 21;

    private const ulong MaxPages = 1UL << PageIndexBits;

    private const int MaxChunks = (int)(MaxPages / PagesPerChunk);

    /// <summary>Page 0 is never handed out, so frame 0 can mean "no page" as in a scatter list.</summary>
    private const ulong FirstPage = 1;

    private readonly object _gate = new();
    private readonly XMemPageChunk?[] _chunks = new XMemPageChunk?[MaxChunks];
    private readonly nint _process;
    private ulong[] _freed = [];
    private int _freedCount;
    private ulong _nextPage = FirstPage;
    private ulong _backedPages = FirstPage;
    private long _livePages;

    public XMemPageStore(nint process)
    {
        _process = process;
    }

    public long LivePages => Interlocked.Read(ref _livePages);

    /// <summary>Fills <paramref name="frames"/> with up to <paramref name="count"/> zeroed pages; returns how many it got.</summary>
    public ulong Allocate(ulong count, ulong* frames)
    {
        ulong allocated = 0;

        lock (_gate)
        {
            while (allocated < count && TryTakePage(out ulong page))
            {
                frames[allocated] = page * FramesPerPage;
                allocated++;
            }
        }

        Interlocked.Add(ref _livePages, (long)allocated);

        return allocated;
    }

    /// <summary>Returns pages to the store, zeroing them; frames the store never handed out are skipped.</summary>
    public void Free(ulong count, ulong* frames)
    {
        long freed = 0;

        lock (_gate)
        {
            for (ulong i = 0; i < count; i++)
            {
                // A frame freed twice would overfill the free list; skip it.
                if (_freedCount == _freed.Length || !TryLocate(frames[i], out var chunk, out ulong offset))
                {
                    continue;
                }

                NativeMemory.Clear((void*)(chunk.Window + (nint)offset), (nuint)PageSize);
                _freed[_freedCount++] = frames[i] / FramesPerPage;
                freed++;
            }
        }

        Interlocked.Add(ref _livePages, -freed);
    }

    /// <summary>The section and byte offset holding frame <paramref name="frame"/>, if the store handed it out.</summary>
    public bool TryLocate(ulong frame, [NotNullWhen(true)] out XMemPageChunk? chunk, out ulong offset)
    {
        ulong page = frame / FramesPerPage;
        chunk = null;
        offset = 0;

        if (frame % FramesPerPage != 0 || page < FirstPage || page >= Volatile.Read(ref _backedPages))
        {
            return false;
        }

        ulong index = page - FirstPage;
        var found = Volatile.Read(ref _chunks[index / PagesPerChunk]);

        if (found is null)
        {
            return false;
        }

        chunk = found;
        offset = index % PagesPerChunk * PageSize;

        return true;
    }

    public void Dispose()
    {
        foreach (var chunk in _chunks)
        {
            if (chunk is null)
            {
                continue;
            }

            _ = KernelBase.UnmapViewOfFile2(_process, chunk.Window, 0);
            _ = Kernel32.CloseHandle(chunk.Section);
        }

        Array.Clear(_chunks);
    }

    private bool TryTakePage(out ulong page)
    {
        if (_freedCount > 0)
        {
            page = _freed[--_freedCount];

            return true;
        }

        page = _nextPage;

        if (page >= _backedPages && !TryAddChunk())
        {
            return false;
        }

        _nextPage++;

        return true;
    }

    private bool TryAddChunk()
    {
        int index = (int)((_backedPages - FirstPage) / PagesPerChunk);

        if (index >= MaxChunks)
        {
            return false;
        }

        nint section = KernelBase.CreateFileMapping2(MemoryConstants.InvalidHandleValue, null, MemoryConstants.FileMapRead | MemoryConstants.FileMapWrite, Win32Constants.PageReadWrite, MemoryConstants.SecCommit, ChunkBytes, null, null, 0);

        if (section == 0)
        {
            return false;
        }

        nint window = KernelBase.MapViewOfFile3(section, _process, 0, 0, (nuint)ChunkBytes, 0, Win32Constants.PageReadWrite, null, 0);

        if (window == 0)
        {
            _ = Kernel32.CloseHandle(section);

            return false;
        }

        Array.Resize(ref _freed, _freed.Length + (int)PagesPerChunk);
        Volatile.Write(ref _chunks[index], new XMemPageChunk(section, window));
        Volatile.Write(ref _backedPages, _backedPages + PagesPerChunk);

        return true;
    }
}
