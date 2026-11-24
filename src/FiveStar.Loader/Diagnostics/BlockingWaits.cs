// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Collections.Concurrent;
using System.Diagnostics;
using FiveStar.Loader.Native;

namespace FiveStar.Loader.Diagnostics;

/// <summary>
/// Where a thread waits inside the loader: a stack walk stops at the first JIT frame, so a game thread blocked in a shim
/// shows only the OS wait. Waits a game thread can reach register here (<see cref="Enter"/>), and
/// <see cref="ThreadStacks"/> names each thread's wait and how long it has lasted; the totals per wait are kept too.
/// </summary>
internal static class BlockingWaits
{
    private static readonly ConcurrentDictionary<uint, (string What, long Since)> Current = new();
    private static readonly ConcurrentDictionary<string, long> Totals = new(StringComparer.Ordinal);

    /// <summary>Notes that this thread starts waiting in <paramref name="what"/>; dispose the result when it ends.</summary>
    public static Scope Enter(string what)
    {
        uint thread = Kernel32.GetCurrentThreadId();
        long since = Stopwatch.GetTimestamp();
        Current[thread] = (what, since);

        return new Scope(thread, what, since);
    }

    /// <summary>The thread's wait as "waiting in X for N ms", or empty.</summary>
    public static string Describe(uint thread)
    {
        if (!Current.TryGetValue(thread, out var wait))
        {
            return string.Empty;
        }

        return $"waiting in {wait.What} for {Stopwatch.GetElapsedTime(wait.Since).TotalMilliseconds:F0} ms";
    }

    /// <summary>The time spent in each wait so far, longest first.</summary>
    public static string Summary()
    {
        var parts = Totals.OrderByDescending(pair => pair.Value).Select(pair => $"{pair.Key} {Stopwatch.GetElapsedTime(0, pair.Value).TotalSeconds:F1} s");

        return $"loader waits: {string.Join(", ", parts)}";
    }

    /// <summary>Ends a wait noted by <see cref="Enter"/>.</summary>
    internal readonly struct Scope : IDisposable
    {
        private readonly uint _thread;
        private readonly string _what;
        private readonly long _since;

        public Scope(uint thread, string what, long since)
        {
            _thread = thread;
            _what = what;
            _since = since;
        }

        public void Dispose()
        {
            Current.TryRemove(_thread, out _);
            long took = Stopwatch.GetTimestamp() - _since;
            Totals.AddOrUpdate(_what, took, (_, total) => total + took);
        }
    }
}
