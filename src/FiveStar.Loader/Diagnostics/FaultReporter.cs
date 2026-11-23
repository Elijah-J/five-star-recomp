// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Runtime.InteropServices;
using FiveStar.Loader.Loading;
using FiveStar.Loader.Native;
using FiveStar.Loader.Shims;

namespace FiveStar.Loader.Diagnostics;

/// <summary>
/// A first vectored exception handler that reports hardware faults (access violations, illegal instructions,
/// breakpoints, ...) whose stack passes through a mapped image. It only reports: every exception continues to the
/// image's own SEH and C++ handlers, so a fault the game handles itself shows up in the log and nothing else changes.
/// </summary>
internal sealed unsafe class FaultReporter
{
    /// <summary>EXCEPTION_CONTINUE_SEARCH (excpt.h).</summary>
    private const int ContinueSearch = 0;

    /// <summary>AddVectoredExceptionHandler's First argument: call this handler before the others.</summary>
    private const uint CallFirst = 1;

    /// <summary>NTSTATUS severity bits 30..31 = 3: an error, the class of hardware faults (ntstatus.h).</summary>
    private const uint ErrorSeverity = 0xC0000000;

    /// <summary>NTSTATUS bit 29: a customer code, such as a C++ (0xE06D7363) or CLR exception (winnt.h).</summary>
    private const uint CustomerBit = 0x20000000;

    /// <summary>STATUS_BREAKPOINT (ntstatus.h): int3 and __debugbreak.</summary>
    private const uint StatusBreakpoint = 0x80000003;

    /// <summary>Frames searched for a mapped image before a fault counts as foreign (runtime-internal).</summary>
    private const int MappedFrameSearchDepth = 12;

    /// <summary>Faults reported with registers, stack and calls; later ones get one line each.</summary>
    private const int FullReports = 3;

    /// <summary>Faults reported at all.</summary>
    private const int MaxReports = 64;

    private readonly ImageTable _images;
    private readonly FaultRecordWriter _writer;
    private readonly LoaderLog _log;
    private int _reported;

    /// <summary>1 while a report is written; a fault meanwhile (in the report or on another thread) is not reported.</summary>
    private int _reporting;
    private nint _handle;

    public FaultReporter(ImageTable images, CallTrace trace, LoaderLog log)
    {
        _images = images;
        _writer = new FaultRecordWriter(images, trace, log);
        _log = log;
    }

    /// <summary>Faults reported so far.</summary>
    public int Reported => Volatile.Read(ref _reported);

    /// <summary>Registers the handler; once per process.</summary>
    public void Install()
    {
        if (_handle != 0)
        {
            return;
        }

        delegate* unmanaged<nint, int> handler = &OnException;
        _handle = Kernel32.AddVectoredExceptionHandler(CallFirst, (nint)handler);
        _log.Info("fault reporter: vectored exception handler installed");
    }

    /// <summary>
    /// Moves the handler back to the front after mapped code added its own first handler (the game's crash handler
    /// would otherwise end the process before the fault is reported).
    /// </summary>
    public void KeepFirst()
    {
        if (_handle == 0)
        {
            return;
        }

        _ = Kernel32.RemoveVectoredExceptionHandler(_handle);
        delegate* unmanaged<nint, int> handler = &OnException;
        _handle = Kernel32.AddVectoredExceptionHandler(CallFirst, (nint)handler);
    }

    /// <summary>Reports a suspended thread's CONTEXT like a fault: where it is, its stack and the last calls.</summary>
    public void WriteThreadState(string title, uint threadId, byte* context)
    {
        _writer.WriteThreadState(title, threadId, context);
    }

    public void WriteRecentCalls(string title)
    {
        _writer.WriteRecentCalls(title);
    }

    [UnmanagedCallersOnly]
    private static int OnException(nint pointers)
    {
        return ShimContext.Runtime.Faults.Handle(pointers);
    }

    private static bool IsFault(uint code)
    {
        if ((code & CustomerBit) != 0)
        {
            return false;
        }

        return code == StatusBreakpoint || (code & ErrorSeverity) == ErrorSeverity;
    }

    /// <summary>EXCEPTION_POINTERS: ExceptionRecord at +0, ContextRecord at +8 (winnt.h).</summary>
    private int Handle(nint pointers)
    {
        byte* record = *(byte**)pointers;
        byte* context = *(byte**)(pointers + sizeof(nint));

        if (!IsFault(*(uint*)record) || Interlocked.CompareExchange(ref _reporting, 1, 0) != 0)
        {
            return ContinueSearch;
        }

        try
        {
            Report(record, context);
        }
        finally
        {
            Volatile.Write(ref _reporting, 0);
        }

        return ContinueSearch;
    }

    private void Report(byte* record, byte* context)
    {
        var frames = NativeStack.WalkFrom(_images, context, MappedFrameSearchDepth);
        bool mapped = frames.Any(f => _images.FindByAddress((nint)f.Pc) is not null);

        if (!mapped)
        {
            return;
        }

        int number = Interlocked.Increment(ref _reported);

        if (number > MaxReports)
        {
            return;
        }

        if (number > FullReports)
        {
            _writer.WriteBrief(number, record);

            return;
        }

        _writer.WriteFull(number, record, context);
    }
}
