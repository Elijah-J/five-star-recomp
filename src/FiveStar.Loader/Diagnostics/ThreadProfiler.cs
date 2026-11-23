// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Diagnostics;
using System.Runtime;
using FiveStar.Loader.Loading;
using FiveStar.Loader.Native;

namespace FiveStar.Loader.Diagnostics;

/// <summary>
/// <c>--profile-threads</c>: with each thread sample, a burst of stack captures of every thread; the threads that are
/// running (innermost frame outside ntdll, where waits sit) count towards the functions of the main image on their
/// stack. The busiest functions, inclusive and innermost, show what the game does each frame while it waits for
/// something.
/// </summary>
internal sealed unsafe class ThreadProfiler
{
    private const int Rounds = 400;
    private const int GapMilliseconds = 2;
    private const int MaxFrames = 48;
    private const int Top = 40;
    private const long NoGcBudget = 16L << 20;
    private const string WaitModule = "ntdll.dll";
    private const string Prefix = "  [profile] ";

    private readonly ImageTable _images;
    private readonly LoaderLog _log;

    public ThreadProfiler(ImageTable images, LoaderLog log)
    {
        _images = images;
        _log = log;
    }

    public void Run(CodeRange[] modules)
    {
        var main = _images.All.FirstOrDefault(i => !i.Pe.IsDll);

        if (main is null)
        {
            return;
        }

        var wait = WaitRange();
        var inclusive = new Dictionary<uint, int>();
        var innermost = new Dictionary<uint, int>();
        int running = 0;

        uint self = Kernel32.GetCurrentThreadId();
        uint[] threads = [.. Process.GetCurrentProcess().Threads.Cast<ProcessThread>().Select(t => (uint)t.Id).Where(id => id != self)];
        var frames = new ulong[threads.Length * MaxFrames];
        var counts = new int[threads.Length];

        try
        {
            for (int round = 0; round < Rounds; round++)
            {
                CaptureRound(threads, modules, frames, counts);

                for (int i = 0; i < threads.Length; i++)
                {
                    if (counts[i] > 0 && !wait.Contains(frames[i * MaxFrames]))
                    {
                        running++;
                        Count(main, frames.AsSpan(i * MaxFrames, counts[i]), inclusive, innermost);
                    }
                }

                Thread.Sleep(GapMilliseconds);
            }
        }
        finally
        {
            EndNoGcRegion();
        }

        _log.Info($"{Prefix}{running} running-thread samples over {Rounds} rounds of {threads.Length} threads");
        _log.Info($"{Prefix}inclusive: {Format(inclusive)}");
        _log.Info($"{Prefix}innermost: {Format(innermost)}");
    }

    /// <summary>
    /// One capture of every thread, inside a no-GC region (see <see cref="ThreadSampler"/>). The region stays open
    /// across rounds and is only started again when it lapsed: starting one runs a full blocking GC, and one per
    /// round stalled every managed shim, the present path included, for most of the burst.
    /// </summary>
    private static void CaptureRound(uint[] threads, CodeRange[] modules, ulong[] frames, int[] counts)
    {
        if (GCSettings.LatencyMode != GCLatencyMode.NoGCRegion)
        {
            _ = GC.TryStartNoGCRegion(NoGcBudget);
        }

        for (int i = 0; i < threads.Length; i++)
        {
            counts[i] = ThreadStackCapture.Capture(threads[i], modules, frames.AsSpan(i * MaxFrames, MaxFrames));
        }
    }

    private static void EndNoGcRegion()
    {
        if (GCSettings.LatencyMode == GCLatencyMode.NoGCRegion)
        {
            GC.EndNoGCRegion();
        }
    }

    private static void Count(LoadedImage main, ReadOnlySpan<ulong> frames, Dictionary<uint, int> inclusive, Dictionary<uint, int> innermost)
    {
        var seen = new HashSet<uint>();
        bool first = true;

        foreach (ulong pc in frames)
        {
            if (!main.Contains((nint)pc))
            {
                continue;
            }

            uint start = FunctionStart(pc, main);

            if (first)
            {
                innermost[start] = innermost.GetValueOrDefault(start) + 1;
                first = false;
            }

            if (seen.Add(start))
            {
                inclusive[start] = inclusive.GetValueOrDefault(start) + 1;
            }
        }
    }

    /// <summary>The RVA of the .pdata function around <paramref name="pc"/>, else the RVA itself.</summary>
    private static uint FunctionStart(ulong pc, LoadedImage main)
    {
        ulong imageBase = 0;
        nint function = Ntdll.RtlLookupFunctionEntry(pc, &imageBase, 0);

        if (function == 0)
        {
            return (uint)(pc - (ulong)main.Base);
        }

        return *(uint*)function;
    }

    private static CodeRange WaitRange()
    {
        foreach (ProcessModule module in Process.GetCurrentProcess().Modules)
        {
            if (string.Equals(module.ModuleName, WaitModule, StringComparison.OrdinalIgnoreCase))
            {
                return new CodeRange((ulong)module.BaseAddress, (ulong)module.BaseAddress + (ulong)module.ModuleMemorySize);
            }
        }

        return default;
    }

    private static string Format(Dictionary<uint, int> counts)
    {
        return string.Join(", ", counts.OrderByDescending(p => p.Value).Take(Top).Select(p => $"0x{p.Key:x} x{p.Value}"));
    }
}
