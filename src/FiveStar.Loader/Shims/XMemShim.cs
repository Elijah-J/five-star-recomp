// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Runtime.InteropServices;

namespace FiveStar.Loader.Shims;

/// <summary>
/// xmem.dll entry points. GDK signatures: <c>void* XMemAlloc(SIZE_T size, uint64_t attributes)</c>,
/// <c>void XMemFree(void* p, uint64_t attributes)</c>. Inferred from the shipped DLLs' call sites:
/// <c>BOOL XMemVirtualQuery(HANDLE process, const void* address, XMEM_BASIC_INFO* info)</c> and
/// <c>BOOL XMemGetWorkingSetStatistics(uint32_t kind, STATS* out)</c>.
/// </summary>
[ShimModule("xmem.dll")]
internal static unsafe class XMemShim
{
    [HotPath]
    [Shim("xmem.dll", "XMemAlloc")]
    [UnmanagedCallersOnly]
    private static nint XMemAlloc(nuint size, ulong attributes)
    {
        return ShimContext.Runtime.XMem.Allocate(size, attributes);
    }

    [HotPath]
    [Shim("xmem.dll", "XMemFree")]
    [UnmanagedCallersOnly]
    private static void XMemFree(nint block, ulong attributes)
    {
        ShimContext.Runtime.XMem.Free(block, attributes);
    }

    [HotPath]
    [Shim("xmem.dll", "XMemVirtualQuery")]
    [UnmanagedCallersOnly]
    private static int XMemVirtualQuery(nint process, nint address, byte* info)
    {
        return ShimContext.Runtime.XMem.VirtualQuery(process, address, info);
    }

    [Shim("xmem.dll", "XMemGetWorkingSetStatistics")]
    [UnmanagedCallersOnly]
    private static int XMemGetWorkingSetStatistics(uint kind, ulong* statistics)
    {
        return ShimContext.Runtime.XMem.GetWorkingSetStatistics(kind, statistics);
    }
}
