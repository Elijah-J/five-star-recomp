// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using FiveStar.Loader.Diagnostics;
using FiveStar.Loader.Native;

namespace FiveStar.Loader.Shims;

/// <summary>
/// The xmem.dll virtual and physical page functions on VirtualAlloc. On the console a title reserves address space,
/// allocates physical pages and maps them into it. Here a physical page is only a number: XMemVirtualAlloc reserves
/// (and commits when asked), and mapping commits ordinary memory at the target address, so every mapped page is
/// backed by real memory. Not emulated: one physical page mapped at two addresses (aliasing) and page contents moving
/// with a remap. The signatures are inferred from the executable's call sites; none is in the public GDK headers.
/// </summary>
internal sealed unsafe class XMemPhysicalMemory
{
    /// <summary>The mapping unit: callers size page arrays as bytes &gt;&gt; 16 and map at 64 KB strides.</summary>
    public const ulong PageSize = 0x10000;

    /// <summary>Page entries count 4 KB frames: the game rebuilds them as <c>(index &lt;&lt; 4) &amp; 0x1FFFFF0</c>.</summary>
    public const ulong FramesPerPage = PageSize / Win32Constants.PageSize;

    /// <summary>The first page number handed out; 0 is never one, since a scatter entry of 0 unmaps.</summary>
    private const ulong FirstPage = 1;

    /// <summary>winnt.h MEM_PHYSICAL. Only MEM_RESERVE and MEM_COMMIT reach VirtualAlloc; MEM_LARGE_PAGES and the console bits are dropped.</summary>
    private const uint MemPhysical = 0x00400000;

    /// <summary>The standard PAGE_* protection; higher bits are console GPU/cache attributes.</summary>
    private const uint StandardProtectionMask = 0xFF;

    private readonly object _gate = new();
    private readonly Stack<ulong> _freedPages = new();
    private readonly LoaderLog _log;
    private ulong _nextPage = FirstPage;
    private long _livePages;

    public XMemPhysicalMemory(LoaderLog log)
    {
        _log = log;
    }

    public long LivePages => Interlocked.Read(ref _livePages);

    /// <summary>
    /// <c>void* XMemVirtualAlloc(void* address, SIZE_T size, DWORD allocationType, uint64 attributes, DWORD protect)</c>.
    /// A physical reservation (MEM_PHYSICAL without MEM_COMMIT) only reserves: its pages are committed when mapped.
    /// </summary>
    public nint VirtualAlloc(nint address, nuint size, uint allocationType, ulong attributes, uint protect)
    {
        uint type = allocationType & Win32Constants.MemReserveCommit;

        if (address == 0 || type == 0)
        {
            type |= Win32Constants.MemReserve;
        }

        uint protection = protect & StandardProtectionMask;

        if (protection == 0)
        {
            protection = Win32Constants.PageReadWrite;
        }

        nint result = Kernel32.VirtualAlloc(address, size, type, protection);
        string note = (allocationType & MemPhysical) != 0 ? " (physical reservation)" : string.Empty;
        _log.Detail($"  [xmem] XMemVirtualAlloc(0x{address:x}, 0x{size:x}, type=0x{allocationType:x}, attr=0x{attributes:x}, protect=0x{protect:x}) -> 0x{result:x}{note}");

        return result;
    }

    /// <summary><c>BOOL XMemAllocatePhysicalPages(uint64 flags, ULONG_PTR* count, ULONG_PTR* pages)</c>: *count page numbers.</summary>
    public int AllocatePages(ulong flags, ulong* count, ulong* pages)
    {
        if (count == null || pages == null)
        {
            return 0;
        }

        lock (_gate)
        {
            for (ulong i = 0; i < *count; i++)
            {
                pages[i] = NextPage() * FramesPerPage;
            }
        }

        Interlocked.Add(ref _livePages, (long)*count);
        _log.Detail($"  [xmem] XMemAllocatePhysicalPages(flags=0x{flags:x}, count={*count})");

        return 1;
    }

    /// <summary><c>BOOL XMemFreePhysicalPages(ULONG_PTR count, ULONG_PTR* pages)</c>.</summary>
    public int FreePages(ulong count, ulong* pages)
    {
        if (pages == null)
        {
            return 0;
        }

        lock (_gate)
        {
            for (ulong i = 0; i < count; i++)
            {
                _freedPages.Push(pages[i] / FramesPerPage);
            }
        }

        Interlocked.Add(ref _livePages, -(long)count);

        return 1;
    }

    /// <summary>
    /// <c>void* XMemMapPhysicalPages(void* address, ULONG_PTR count, ULONG_PTR* pages)</c>: commits the range and
    /// returns the address (0 on failure); null pages decommit it.
    /// </summary>
    public nint MapPages(nint address, ulong count, ulong* pages)
    {
        nuint size = (nuint)(count * PageSize);

        if (pages == null)
        {
            _ = Kernel32.VirtualFree(address, size, Win32Constants.MemDecommit);

            return address;
        }

        nint mapped = Kernel32.VirtualAlloc(address, size, Win32Constants.MemCommit, Win32Constants.PageReadWrite);
        _log.Detail($"  [xmem] XMemMapPhysicalPages(0x{address:x}, {count} pages) -> 0x{mapped:x}");

        return mapped;
    }

    /// <summary>
    /// <c>BOOL XMemMapPhysicalPagesScatter(void** addresses, ULONG count, ULONG_PTR* pages)</c>: one page per address;
    /// a 0 entry (or no page array) decommits. The game breaks into the debugger when this returns 0.
    /// </summary>
    public int MapPagesScatter(nint* addresses, ulong count, ulong* pages)
    {
        if (addresses == null)
        {
            return 0;
        }

        int succeeded = 1;

        for (ulong i = 0; i < count; i++)
        {
            if (pages == null || pages[i] == 0)
            {
                _ = Kernel32.VirtualFree(addresses[i], (nuint)PageSize, Win32Constants.MemDecommit);

                continue;
            }

            if (Kernel32.VirtualAlloc(addresses[i], (nuint)PageSize, Win32Constants.MemCommit, Win32Constants.PageReadWrite) == 0)
            {
                succeeded = 0;
            }
        }

        if (succeeded == 0)
        {
            _log.Warn($"[xmem] XMemMapPhysicalPagesScatter({count} pages): a commit failed");
        }

        return succeeded;
    }

    private ulong NextPage()
    {
        if (_freedPages.Count > 0)
        {
            return _freedPages.Pop();
        }

        return _nextPage++;
    }
}
