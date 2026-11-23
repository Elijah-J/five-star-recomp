// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Collections.Concurrent;
using System.Diagnostics;
using FiveStar.Loader.Diagnostics;
using FiveStar.Loader.Native;

namespace FiveStar.Loader.Shims.D3D12X.Pipelines;

/// <summary>
/// Builds pipelines into the disk cache ahead of the game (<c>--warm-pipelines</c>): each target becomes the desktop
/// stream the game's creation would make, is skipped when the cache has it (so a new run resumes) or when an earlier
/// run saw the driver refuse it, and is otherwise built with no time limit on up to <c>jobs</c> threads.
/// </summary>
internal sealed unsafe class PipelineWarmup
{
    /// <summary>Refused pipelines, one "key hresult names" line each, in the cache folder.</summary>
    public const string FailureFile = "warm-failures.txt";

    /// <summary>A build that takes longer than this is named in the log.</summary>
    private static readonly TimeSpan SlowBuild = TimeSpan.FromSeconds(20);

    private static readonly TimeSpan ProgressInterval = TimeSpan.FromSeconds(30);

    private readonly PipelineFactory _factory;
    private readonly PipelineLibraryCache _cache;
    private readonly string _failurePath;
    private readonly LoaderLog _log;
    private readonly ConcurrentDictionary<string, byte> _seen = new(StringComparer.Ordinal);
    private readonly HashSet<string> _refused;
    private readonly object _failures = new();
    private readonly WarmTally _tally = new();
    private volatile bool _stopping;

    public PipelineWarmup(PipelineFactory factory, PipelineLibraryCache cache, string cacheFolder, LoaderLog log)
    {
        _factory = factory;
        _cache = cache;
        _failurePath = Path.Combine(cacheFolder, FailureFile);
        _log = log;
        _refused = ReadFailures(_failurePath);
    }

    /// <summary>What the run did so far.</summary>
    public WarmTally Tally => _tally;

    /// <summary>No new pipeline starts after this; the ones running finish.</summary>
    public void Stop()
    {
        _stopping = true;
    }

    /// <summary>Builds every target that is not cached; true when no target was left out by <see cref="Stop"/>.</summary>
    public bool Run(IReadOnlyList<WarmTarget> targets, int jobs, Action forget)
    {
        var clock = Stopwatch.StartNew();
        var options = new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, jobs) };
        var partitions = Partitioner.Create(targets, EnumerablePartitionerOptions.NoBuffering);
        using var progress = new Timer(_ => Report(targets.Count, clock), null, ProgressInterval, ProgressInterval);

        Parallel.ForEach(partitions, options, (target, loop) =>
        {
            if (_stopping)
            {
                loop.Stop();

                return;
            }

            Warm(target);

            if (_tally.Done % WarmTally.ForgetInterval == 0)
            {
                forget();
            }
        });

        Report(targets.Count, clock);

        return !_stopping;
    }

    private void Warm(WarmTarget target)
    {
        PipelineRecipe? recipe;

        try
        {
            recipe = Prepare(target);
        }
        catch (Exception exception) when (exception is InvalidDataException or NotSupportedException)
        {
            recipe = null;
        }

        if (recipe is null)
        {
            _tally.Count(WarmOutcome.Untranslated);

            return;
        }

        var key = recipe.Stream.Key;

        if (_cache.Contains(key) || _refused.Contains(key) || !_seen.TryAdd(key, 0))
        {
            recipe.Stream.Dispose();
            _tally.Count(WarmOutcome.Skipped);

            return;
        }

        Build(recipe, key);
    }

    private PipelineRecipe? Prepare(WarmTarget target)
    {
        if (target.Compute is not null)
        {
            return _factory.PrepareCompute(target.Compute);
        }

        return _factory.PrepareGraphics(target.Graphics!);
    }

    private void Build(PipelineRecipe recipe, string key)
    {
        var started = Stopwatch.GetTimestamp();
        var result = _factory.Build(recipe.Stream, DesktopDevice.PipelineStateIid);
        var took = Stopwatch.GetElapsedTime(started);

        if (took > SlowBuild)
        {
            _log.Info($"  {took.TotalSeconds:F0} s: {recipe.Names}");
        }

        if (HResult.Failed(result.Hr) || result.Pipeline == 0)
        {
            _tally.Count(WarmOutcome.Refused);
            RecordFailure(key, result.Hr, recipe.Names);

            return;
        }

        DesktopDevice.Release(result.Pipeline);
        _tally.Count(WarmOutcome.Built);
        _tally.AddBuildTime(took);
    }

    private void RecordFailure(string key, int hr, string names)
    {
        lock (_failures)
        {
            File.AppendAllLines(_failurePath, [$"{key} {HResult.Format(hr)} {names}"]);
        }
    }

    private void Report(int total, Stopwatch clock)
    {
        _log.Info($"  warm-up {_tally.Done}/{total} after {clock.Elapsed:hh\\:mm\\:ss}: {_tally.Summary}; {_cache.Summary}");
    }

    private static HashSet<string> ReadFailures(string path)
    {
        if (!File.Exists(path))
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        return File.ReadLines(path).Select(line => line.Split(' ')[0]).ToHashSet(StringComparer.Ordinal);
    }
}
