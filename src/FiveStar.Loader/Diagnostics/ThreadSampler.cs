// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Diagnostics;
using System.Runtime;
using System.Text;
using FiveStar.Loader.Loading;
using FiveStar.Loader.Native;

namespace FiveStar.Loader.Diagnostics;

/// <summary>
/// <c>--sample-threads &lt;s&gt;</c>: every s seconds logs the calls through shims, stubs and GDK slots since the last
/// sample (busiest first), a line of loader counters, and the stack of every thread of the process, threads with the
/// same stack grouped. Stacks are captured inside a no-GC region, so a garbage collection cannot wait on a thread that
/// is suspended for its walk.
/// </summary>
internal sealed unsafe class ThreadSampler
{
    private const int MaxFrames = 24;
    private const int TopCalls = 96;
    private const int MaxThreads = 512;
    private const long NoGcBudget = 16L << 20;
    private const string Prefix = "  [sample] ";

    private readonly ImageTable _images;
    private readonly CallTrace _trace;
    private readonly Func<string> _counters;
    private readonly LoaderLog _log;
    private readonly ThreadProfiler? _profiler;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private Dictionary<string, ulong> _lastTotals = new(StringComparer.Ordinal);

    /// <summary>
    /// <paramref name="counters"/> gives one line of other counters (DirectStorage) logged with each sample;
    /// <paramref name="profile"/> adds a <see cref="ThreadProfiler"/> burst to each sample.
    /// </summary>
    public ThreadSampler(ImageTable images, CallTrace trace, Func<string> counters, bool profile, LoaderLog log)
    {
        _images = images;
        _trace = trace;
        _counters = counters;
        _log = log;
        _profiler = profile ? new ThreadProfiler(images, log) : null;
    }

    public void Start(TimeSpan interval)
    {
        var thread = new Thread(() => Run(interval)) { IsBackground = true, Name = "thread sampler" };
        thread.Start();
        _log.Info($"{Prefix}every {interval.TotalSeconds} s");
    }

    private void Run(TimeSpan interval)
    {
        for (int number = 1; ; number++)
        {
            Thread.Sleep(interval);
            _log.Info($"{Prefix}#{number} at {_clock.Elapsed.TotalSeconds:F1} s: {_counters()}");
            LogCalls();
            LogThreads();
            _profiler?.Run(Modules());
        }
    }

    private void LogCalls()
    {
        var totals = _trace.Totals();
        var busiest = totals
            .Select(pair => (pair.Key, Delta: pair.Value - _lastTotals.GetValueOrDefault(pair.Key)))
            .Where(pair => pair.Delta != 0)
            .OrderByDescending(pair => pair.Delta)
            .Take(TopCalls)
            .Select(pair => $"{pair.Key} x{pair.Delta}")
            .ToList();
        _lastTotals = totals;
        _log.Info($"{Prefix}calls since the last sample: {string.Join(", ", busiest)}");
    }

    private void LogThreads()
    {
        var modules = Modules();
        var ids = new uint[MaxThreads];
        var states = new string[MaxThreads];
        var frames = new ulong[MaxThreads * MaxFrames];
        var counts = new int[MaxThreads];

        int threads = Capture(modules, ids, states, frames, counts);
        var groups = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        for (int i = 0; i < threads; i++)
        {
            string stack = Format(frames.AsSpan(i * MaxFrames, counts[i]));

            if (!groups.TryGetValue(stack, out var members))
            {
                members = [];
                groups[stack] = members;
            }

            members.Add($"{ids[i]} {states[i]}");
        }

        _log.Info($"{Prefix}{threads} threads, {groups.Count} distinct stacks");

        foreach (var (stack, members) in groups.OrderByDescending(g => g.Value.Count))
        {
            _log.Info($"{Prefix}  {members.Count} x [{string.Join(", ", members)}]: {stack}");
        }
    }

    /// <summary>Walks every other thread inside a no-GC region; returns the number of threads captured.</summary>
    private static int Capture(CodeRange[] modules, uint[] ids, string[] states, ulong[] frames, int[] counts)
    {
        uint self = Kernel32.GetCurrentThreadId();
        bool noGc = GC.TryStartNoGCRegion(NoGcBudget);
        int threads = 0;

        try
        {
            foreach (ProcessThread thread in Process.GetCurrentProcess().Threads)
            {
                if ((uint)thread.Id == self || threads == MaxThreads)
                {
                    continue;
                }

                ids[threads] = (uint)thread.Id;
                states[threads] = State(thread);
                counts[threads] = ThreadStackCapture.Capture((uint)thread.Id, modules, frames.AsSpan(threads * MaxFrames, MaxFrames));
                threads++;
            }
        }
        finally
        {
            if (noGc && GCSettings.LatencyMode == GCLatencyMode.NoGCRegion)
            {
                GC.EndNoGCRegion();
            }
        }

        return threads;
    }

    private static string State(ProcessThread thread)
    {
        try
        {
            if (thread.ThreadState == System.Diagnostics.ThreadState.Wait)
            {
                return $"wait:{thread.WaitReason}";
            }

            return thread.ThreadState.ToString();
        }
        catch (InvalidOperationException)
        {
            return "gone";
        }
    }

    /// <summary>The mapped images and every OS-loaded module: the code whose unwind data the walk may use.</summary>
    private CodeRange[] Modules()
    {
        var ranges = _images.All.Select(i => new CodeRange((ulong)i.Base, (ulong)i.Base + i.Size)).ToList();

        foreach (ProcessModule module in Process.GetCurrentProcess().Modules)
        {
            ranges.Add(new CodeRange((ulong)module.BaseAddress, (ulong)module.BaseAddress + (ulong)module.ModuleMemorySize));
        }

        return [.. ranges];
    }

    private string Format(ReadOnlySpan<ulong> pcs)
    {
        if (pcs.IsEmpty)
        {
            return "(not read)";
        }

        var text = new StringBuilder();

        foreach (ulong pc in pcs)
        {
            if (text.Length != 0)
            {
                text.Append(" <- ");
            }

            text.Append(Describe(pc));
        }

        return text.ToString();
    }

    /// <summary>"image+0xrva (fn 0xstart)" for mapped code, "module+0xoffset" for OS modules, else the address.</summary>
    private string Describe(ulong pc)
    {
        var image = _images.FindByAddress((nint)pc);

        if (image is null)
        {
            string where = SystemModules.Describe((nint)pc);

            if (where == SystemModules.Unknown)
            {
                return $"0x{pc:x}";
            }

            return where;
        }

        ulong imageBase = 0;
        nint function = Ntdll.RtlLookupFunctionEntry(pc, &imageBase, 0);
        string offset = $"{image.Name}+0x{pc - (ulong)image.Base:x}";

        if (function == 0)
        {
            return offset;
        }

        return $"{offset} (fn 0x{*(uint*)function:x})";
    }
}
