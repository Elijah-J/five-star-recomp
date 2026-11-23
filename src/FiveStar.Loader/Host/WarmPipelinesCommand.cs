// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Diagnostics;
using FiveStar.Loader.Diagnostics;
using FiveStar.Loader.Native;
using FiveStar.Loader.Shims;
using FiveStar.Loader.Shims.D3D12X;
using FiveStar.Loader.Shims.D3D12X.Pipelines;
using FiveStar.Shaders.Cache;

namespace FiveStar.Loader.Host;

/// <summary>
/// The <c>--warm-pipelines</c> mode: no game runs. On the default adapter, as the game's device would, every pipeline
/// of the console pipeline cache is built through the same factory as slots 57 and 67 and stored in the disk cache
/// (<see cref="PipelineWarmup"/>), so the game's first boot loads them instead of waiting for the driver.
/// </summary>
internal static unsafe class WarmPipelinesCommand
{
    /// <summary>The exit code of a run stopped by Ctrl+C; a new run resumes.</summary>
    private const int Stopped = 4;

    public static int Run(HostOptions options, LoaderLog log)
    {
        var folders = HostComposition.CreatePipelineFolders(options);

        if (folders.Programs is null || folders.Translated is null || folders.Cache is null)
        {
            log.Error("--warm-pipelines needs --shader-dir, --translated-dir and a pipeline cache (not --no-pipeline-cache)");

            return LoaderHost.UsageError;
        }

        var device = new D3D12Forwarder(log);
        var iid = DesktopDevice.Device2Iid;
        nint device2;
        nint root;

        if (HResult.Failed(device.CreateDevice(D3D12XLayout.MinimumFeatureLevel, &iid, &device2)) || HResult.Failed(BindlessRoot.Create(device2, &root)))
        {
            log.Error("no D3D12 device or root signature on the default adapter");

            return LoaderHost.Failure;
        }

        var cache = PipelineLibraryCache.Open(folders.Cache, device2, log);

        if (cache is null)
        {
            return LoaderHost.Failure;
        }

        using (cache)
        {
            cache.StartSaving();

            return Warm(options, folders, device2, root, cache, log);
        }
    }

    private static int Warm(HostOptions options, PipelineFolders folders, nint device2, nint root, PipelineLibraryCache cache, LoaderLog log)
    {
        var warnings = new OnceLog(log);
        var index = ProgramIndex.Load(folders.Programs!);
        var library = TranslatedProgramLibrary.Open(index, folders.Translated!, device2, warnings, log);
        var standIns = new StandInPipelines(PipelineSlots.CreationBudget, new PipelineBuildQueue(1), warnings);
        var factory = new PipelineFactory(device2, root, library, warnings, null, standIns, cache);
        var warmup = new PipelineWarmup(factory, cache, folders.Cache!, log);

        var targets = WarmTargets.Read(ConsolePipelineSource.Load(options.WarmSource!), Math.Max(1, options.WarmStep));
        targets = Matching(targets, options.WarmMatches, index);
        var jobs = options.WarmJobs ?? Math.Max(1, Environment.ProcessorCount / PipelineSlots.CoresPerBuildThread);
        log.Info($"warm-up: {targets.Count} pipelines on {jobs} threads into {folders.Cache}");

        ConsoleCancelEventHandler stop = (_, args) =>
        {
            args.Cancel = true;
            warmup.Stop();
            log.Info("warm-up stopping: the running builds finish, then the cache is written");
        };
        Console.CancelKeyPress += stop;
        var clock = Stopwatch.StartNew();

        try
        {
            if (!warmup.Run(targets, jobs, library.Forget))
            {
                log.Info($"warm-up stopped after {clock.Elapsed:hh\\:mm\\:ss}: {warmup.Tally.Summary}; run again to resume");

                return Stopped;
            }

            log.Info($"warm-up done in {clock.Elapsed:hh\\:mm\\:ss}: {warmup.Tally.Summary}");

            return LoaderHost.Success;
        }
        finally
        {
            Console.CancelKeyPress -= stop;
        }
    }

    /// <summary>The targets with a shader whose extracted program name contains one of <paramref name="matches"/>; all without.</summary>
    private static List<WarmTarget> Matching(List<WarmTarget> targets, IReadOnlyList<string> matches, ProgramIndex index)
    {
        if (matches.Count == 0)
        {
            return targets;
        }

        return targets.Where(target => Shaders(target).Any(shader => index.Find(shader.Bytes).Any(name => matches.Any(match => name.Contains(match, StringComparison.OrdinalIgnoreCase))))).ToList();
    }

    private static IEnumerable<ShaderSource> Shaders(WarmTarget target)
    {
        if (target.Compute is not null)
        {
            return [target.Compute.Shader];
        }

        return target.Graphics!.Shaders;
    }
}
