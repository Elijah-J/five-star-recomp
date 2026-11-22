// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using FiveStar.Cli.Shader;
using FiveStar.Shaders.Compilation;
using FiveStar.Shaders.Translation;

namespace FiveStar.Cli.Setup;

/// <summary>
/// Translates every extracted program to HLSL and compiles it with dxc through the shader cache: runs
/// <c>fivestar shader translate</c> into <c>&lt;work&gt;/shaders/hlsl</c> with <c>--cache &lt;work&gt;/shader-cache</c>. An
/// interrupted run keeps every finished translation and compile in the cache, so the next run resumes where it stopped.
/// </summary>
internal sealed class ShaderTranslateStep : ISetupStep
{
    public const string StepName = "shader-translate";

    /// <summary>The translator options setup uses: the dispatch form with pinned wave sizes, as the translate command.</summary>
    public static readonly TranslationOptions Translation = new(StructuredControlFlow: false, PinWaveSize: true);

    // Measured: HLSL, DXIL and cache entries come to about 610 KiB per program.
    private const long BytesPerProgram = 640 * 1024;

    // Measured: a cold translation and compile takes about 3.2 s of one job per program.
    private const double JobSecondsPerProgram = 3.2;

    // The programs of version 1.0.2.1, for the estimate before they are extracted.
    private const int CorpusPrograms = 25_884;

    private const string ProgramPattern = "*.fssm";

    private static readonly string[] ToolOptions = ["llvm", "dxc"];

    private string? _tool;

    public string Name => StepName;

    public string Title => "Translate and compile shaders";

    public IReadOnlyList<string> DependsOn => [ShaderExtractStep.StepName];

    public TimeSpan Estimate(SetupContext context) => TimeSpan.FromSeconds(Programs(context) * JobSecondsPerProgram / context.Jobs);

    /// <summary>The identity of the translator and compiler build: also the key of the cache's usage record.</summary>
    public static string Identity(CommandArguments arguments)
    {
        var compiler = ShaderTools.Compiler(arguments, retry: false);

        return ShaderCacheIdentity.Of(ShaderTools.Llvm(arguments), Translation, compiler);
    }

    public StepFingerprint Fingerprint(SetupContext context)
    {
        _tool ??= Identity(context.Arguments);

        return new StepFingerprint(Fingerprints.Combine(Fingerprints.OfContent(context.ManifestPath), context.Sample), _tool);
    }

    public bool HasOutputs(SetupContext context) => File.Exists(ResultsPath(context));

    public long RequiredBytes(SetupContext context)
    {
        // What an earlier run wrote is overwritten in place or reused from the cache.
        var existing = StepText.FolderSize(context.TranslationFolder) + StepText.FolderSize(context.CacheFolder);

        return Math.Max(0, (Programs(context) * BytesPerProgram) - existing);
    }

    /// <summary>The programs the sample selects: the first <c>--limit</c> of every <c>--step</c>-th.</summary>
    private static long Programs(SetupContext context)
    {
        long programs = CorpusPrograms;

        if (Directory.Exists(context.ProgramFolder))
        {
            programs = Directory.EnumerateFiles(context.ProgramFolder, ProgramPattern).Count();
        }

        programs = (programs + context.Every - 1) / context.Every;

        if (context.Limit is not null)
        {
            programs = Math.Min(programs, context.Limit.Value);
        }

        return programs;
    }

    public string? MissingRequirement(SetupContext context)
    {
        var missing = ToolCheck.MissingShaderTools(context.Arguments);

        if (missing.Count == 0)
        {
            return null;
        }

        return string.Join("; ", missing);
    }

    public StepResult Run(SetupContext context)
    {
        TranslateCommand.Run(Arguments(context));
        var results = ResultsFile.Read(ResultsPath(context));
        var counts = results.GroupBy(result => result.Status).OrderByDescending(group => group.Count()).Select(group => $"{group.Key} {group.Count()}");
        var summary = $"{results.Count} programs: {string.Join(", ", counts)}";
        var open = results.Count(result => !ProgramStatus.IsFinal(result.Status));

        if (open > 0)
        {
            summary += $"; {open} did not translate or compile (see {ResultsPath(context)})";
        }

        return StepResult.Done(summary);
    }

    private static string ResultsPath(SetupContext context)
    {
        return Path.Combine(context.TranslationFolder, CorpusTranslator.ResultsName);
    }

    private static List<string> Arguments(SetupContext context)
    {
        var jobs = context.Jobs.ToString(CultureInfo.InvariantCulture);
        var arguments = new List<string> { context.ProgramFolder, context.TranslationFolder, "--cache", context.CacheFolder, "--jobs", jobs };

        if (context.Limit is not null)
        {
            arguments.AddRange(["--limit", context.Limit.Value.ToString(CultureInfo.InvariantCulture)]);
        }

        arguments.AddRange(["--step", context.Every.ToString(CultureInfo.InvariantCulture)]);

        foreach (var name in ToolOptions)
        {
            var value = context.Arguments.Option(name);

            if (value is not null)
            {
                arguments.AddRange(["--" + name, value]);
            }
        }

        return arguments;
    }
}
