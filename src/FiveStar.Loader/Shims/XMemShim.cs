// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Runtime.InteropServices;

namespace FiveStar.Loader.Shims;

/// <summary>
/// xmem.dll entry points. GDK signatures (xmem.h): <c>void* XMemAlloc(SIZE_T size, uint64_t attributes)</c>,
/// <c>void XMemFree(void* p, uint64_t attributes)</c> and their <c>XMemAllocDefault</c>/<c>XMemFreeDefault</c>
/// twins. Inferred from the shipped DLLs' call sites:
/// <c>BOOL XMemVirtualQuery(HANDLE process, const void* address, XMEM_BASIC_INFO* info)</c>. The working set
/// statistics are in <see cref="XMemWorkingSet"/>, the virtual and physical page functions in
/// <see cref="XMemPhysicalMemory"/>.
/// </summary>
[ShimModule(Module)]
internal static unsafe partial class XMemShim
{
    private const string Module = "xmem.dll";

    [NotImplementedTable(Module)]
    private static readonly (string Name, long ReturnValue)[] Catalog =
    [
        // No tools (development kit) memory on a retail machine; the callers do not act on the result.
        ("XMemIsToolsMemAvailable", 0), ("XMemTransferMemory", 0),
    ];

    [HotPath]
    [Shim(Module,"XMemAlloc")]
    [UnmanagedCallersOnly]
    private static nint XMemAlloc(nuint size, ulong attributes)
    {
        return ShimContext.Runtime.XMem.Allocate(size, attributes);
    }

    [HotPath]
    [Shim(Module,"XMemFree")]
    [UnmanagedCallersOnly]
    private static void XMemFree(nint block, ulong attributes)
    {
        ShimContext.Runtime.XMem.Free(block, attributes);
    }

    [HotPath]
    [Shim(Module,"XMemVirtualQuery")]
    [UnmanagedCallersOnly]
    private static int XMemVirtualQuery(nint process, nint address, byte* info)
    {
        return ShimContext.Runtime.XMem.VirtualQuery(process, address, info);
    }

    [Shim(Module,"XMemGetWorkingSetStatistics")]
    [UnmanagedCallersOnly]
    private static int XMemGetWorkingSetStatistics(uint flags, byte* statistics)
    {
        return ShimContext.Runtime.XMemWorkingSet.GetStatistics(flags, statistics);
    }

    [Shim(Module, "XMemAllocDefault")]
    [UnmanagedCallersOnly]
    private static nint XMemAllocDefault(nuint size, ulong attributes)
    {
        return ShimContext.Runtime.XMem.Allocate(size, attributes);
    }

    [Shim(Module, "XMemFreeDefault")]
    [UnmanagedCallersOnly]
    private static void XMemFreeDefault(nint block, ulong attributes)
    {
        ShimContext.Runtime.XMem.Free(block, attributes);
    }

    [Shim(Module, "XMemVirtualAlloc")]
    [UnmanagedCallersOnly]
    private static nint XMemVirtualAlloc(nint address, nuint size, uint allocationType, ulong attributes, uint protect)
    {
        return ShimContext.Runtime.XMemPhysical.VirtualAlloc(address, size, allocationType, attributes, protect);
    }

    [Shim(Module, "XMemAllocatePhysicalPages")]
    [UnmanagedCallersOnly]
    private static int XMemAllocatePhysicalPages(ulong flags, ulong* count, ulong* pages)
    {
        return ShimContext.Runtime.XMemPhysical.AllocatePages(flags, count, pages);
    }

    [Shim(Module, "XMemFreePhysicalPages")]
    [UnmanagedCallersOnly]
    private static int XMemFreePhysicalPages(ulong count, ulong* pages)
    {
        return ShimContext.Runtime.XMemPhysical.FreePages(count, pages);
    }

    [Shim(Module, "XMemMapPhysicalPages")]
    [UnmanagedCallersOnly]
    private static nint XMemMapPhysicalPages(nint address, ulong count, ulong* pages)
    {
        return ShimContext.Runtime.XMemPhysical.MapPages(address, count, pages);
    }

    [Shim(Module, "XMemMapPhysicalPagesScatter")]
    [UnmanagedCallersOnly]
    private static int XMemMapPhysicalPagesScatter(nint* addresses, ulong count, ulong* pages)
    {
        return ShimContext.Runtime.XMemPhysical.MapPagesScatter(addresses, count, pages);
    }
}
