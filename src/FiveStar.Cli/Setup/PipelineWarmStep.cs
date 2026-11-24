// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Diagnostics;
using System.Globalization;
using FiveStar.Cli.Exe;

namespace FiveStar.Cli.Setup;

/// <summary>
/// Builds every pipeline of the console pipeline cache with the desktop driver into the loader's pipeline cache
/// (<c>&lt;work&gt;/pipeline-cache</c>, the folder <c>fivestar run</c> gives the loader), so the game's first boot
/// loads them instead of waiting minutes for the largest compute programs: runs <c>fivestar-loader --warm-pipelines</c>.
/// An interrupted run keeps every pipeline built so far, and the next run resumes.
/// </summary>
internal sealed class PipelineWarmStep : ISetupStep
{
    public const string StepName = "pipeline-warm";

    // Measured on an RTX 4090: the library takes about 90 KiB per pipeline, 28,164 pipelines.
    private const long ExpectedBytes = 3L << 30;

    private const string SegmentPattern = "*.psolib";
    private const string LoaderOption = "loader";

    private static readonly TimeSpan TypicalTime = TimeSpan.FromHours(1);

    public string Name => StepName;

    public string Title => "Build the desktop pipelines into the pipeline cache";

    public IReadOnlyList<string> DependsOn => [ShaderTranslateStep.StepName, PipelineCacheStep.StepName];

    public TimeSpan Estimate(SetupContext context) => TypicalTime;

    public StepFingerprint Fingerprint(SetupContext context)
    {
        var loader = Loader(context);
        var tool = "no-loader";

        if (loader is not null)
        {
            tool = Fingerprints.OfFiles(Path.GetDirectoryName(loader)!, [loader]);
        }

        return new StepFingerprint(Fingerprints.Combine(Fingerprints.OfFiles(context.Dump, [context.ShaderArchive]), context.Sample), tool);
    }

    /// <summary>The cache has segments; a driver update makes the next loader run delete them, which redoes the step.</summary>
    public bool HasOutputs(SetupContext context) =>
        Directory.Exists(context.PipelineLibraryFolder) && Directory.EnumerateFiles(context.PipelineLibraryFolder, SegmentPattern).Any();

    public long RequiredBytes(SetupContext context) => Math.Max(0, ExpectedBytes - StepText.FolderSize(context.PipelineLibraryFolder));

    public string? MissingRequirement(SetupContext context)
    {
        if (Loader(context) is null)
        {
            return "fivestar-loader not found (build it, or pass --loader <path> or set FIVESTAR_LOADER)";
        }

        return null;
    }

    public StepResult Run(SetupContext context)
    {
        Directory.CreateDirectory(context.LogFolder);
        var log = Path.Combine(context.LogFolder, $"pipeline-warm-{DateTime.Now:yyyyMMdd-HHmmss}.log");
        var start = new ProcessStartInfo(Loader(context)!) { UseShellExecute = false };
        string[] arguments =
        [
            "--warm-pipelines", context.ShaderArchive,
            "--shader-dir", context.ProgramFolder,
            "--translated-dir", context.TranslationFolder,
            "--pipeline-cache", context.PipelineLibraryFolder,
            "--warm-jobs", context.Jobs.ToString(CultureInfo.InvariantCulture),
            "--warm-step", context.Every.ToString(CultureInfo.InvariantCulture),
            "--log", log,
        ];

        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start) ?? throw new InvalidOperationException("cannot start fivestar-loader");
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            return StepResult.Failed($"fivestar-loader --warm-pipelines exit code {process.ExitCode} (see {log}); run setup again to resume");
        }

        return StepResult.Done($"pipelines in {context.PipelineLibraryFolder} ({StepText.Size(StepText.FolderSize(context.PipelineLibraryFolder))})");
    }

    private static string? Loader(SetupContext context)
    {
        return LoaderLocator.Locate(context.Arguments.Option(LoaderOption));
    }
}
