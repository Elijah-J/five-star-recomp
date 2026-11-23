// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using FiveStar.Loader.Native;

namespace FiveStar.Loader.Diagnostics;

/// <summary>
/// Captures another thread's return addresses: suspend, GetThreadContext, unwind through .pdata, resume. Nothing is
/// allocated and nothing is logged while the thread is suspended (it may hold the heap, log or loader lock). The walk
/// stops at the first address outside the given modules, so JIT code, whose unwind data the runtime answers through
/// a callback that may take a lock the suspended thread holds, is never unwound.
/// </summary>
internal static unsafe class ThreadStackCapture
{
    // winnt.h: THREAD_SUSPEND_RESUME | THREAD_GET_CONTEXT | THREAD_QUERY_INFORMATION.
    private const uint ThreadAccess = 0x0002 | 0x0008 | 0x0040;

    // CONTEXT (winnt.h, AMD64): size, alignment, ContextFlags, Rsp and Rip; CONTEXT_CONTROL | CONTEXT_INTEGER.
    private const int ContextSize = 1232;
    private const int ContextAlignment = 16;
    private const int ContextFlagsOffset = 0x30;
    private const int RspOffset = 0x98;
    private const int RipOffset = 0xF8;
    private const uint ContextControlInteger = 0x100003;

    /// <summary>UNW_FLAG_NHANDLER: unwind without calling handlers.</summary>
    private const uint NoHandler = 0;

    /// <summary>SuspendThread's failure value, (DWORD)-1.</summary>
    private const uint SuspendFailed = uint.MaxValue;

    /// <summary>Fills <paramref name="pcs"/> with the thread's frames, innermost first; returns the count (0: not read).</summary>
    public static int Capture(uint threadId, ReadOnlySpan<CodeRange> modules, Span<ulong> pcs)
    {
        nint thread = Kernel32.OpenThread(ThreadAccess, 0, threadId);

        if (thread == 0)
        {
            return 0;
        }

        byte* raw = stackalloc byte[ContextSize + ContextAlignment];
        byte* context = (byte*)(((ulong)raw + ContextAlignment - 1) & ~(ulong)(ContextAlignment - 1));
        *(uint*)(context + ContextFlagsOffset) = ContextControlInteger;
        int count = 0;

        if (Kernel32.SuspendThread(thread) != SuspendFailed)
        {
            if (Kernel32.GetThreadContext(thread, context) != 0)
            {
                count = Walk(context, modules, pcs);
            }

            _ = Kernel32.ResumeThread(thread);
        }

        _ = Kernel32.CloseHandle(thread);

        return count;
    }

    private static int Walk(byte* context, ReadOnlySpan<CodeRange> modules, Span<ulong> pcs)
    {
        int count = 0;

        while (count < pcs.Length)
        {
            ulong pc = *(ulong*)(context + RipOffset);
            ulong rsp = *(ulong*)(context + RspOffset);

            if (pc == 0)
            {
                break;
            }

            pcs[count++] = pc;

            if (!InModules(pc, modules))
            {
                break;
            }

            ulong imageBase = 0;
            nint function = Ntdll.RtlLookupFunctionEntry(pc, &imageBase, 0);

            if (function == 0 && count > 1)
            {
                break;
            }

            Unwind(context, pc, imageBase, function);

            if (*(ulong*)(context + RspOffset) <= rsp)
            {
                break;
            }
        }

        return count;
    }

    private static bool InModules(ulong pc, ReadOnlySpan<CodeRange> modules)
    {
        foreach (var range in modules)
        {
            if (range.Contains(pc))
            {
                return true;
            }
        }

        return false;
    }

    private static void Unwind(byte* context, ulong pc, ulong imageBase, nint function)
    {
        if (function == 0)
        {
            // A leaf function: the return address is at [rsp].
            ulong rsp = *(ulong*)(context + RspOffset);
            *(ulong*)(context + RipOffset) = *(ulong*)rsp;
            *(ulong*)(context + RspOffset) = rsp + sizeof(ulong);

            return;
        }

        nint handlerData;
        ulong establisherFrame;
        Ntdll.RtlVirtualUnwind(NoHandler, imageBase, pc, function, context, &handlerData, &establisherFrame, 0);
    }
}
