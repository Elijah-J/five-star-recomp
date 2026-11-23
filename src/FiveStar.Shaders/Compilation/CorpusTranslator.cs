// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text.RegularExpressions;
using FiveStar.Shaders.Translation;

namespace FiveStar.Shaders.Compilation;

/// <summary>
/// Translates many FSSM programs and validates each with dxc: writes <c>&lt;name&gt;.hlsl</c> and <c>.dxil</c> (and the mesh
/// fallback pair <c>.msfb_cs</c>/<c>.msfb_vs</c> where needed or asked for, the wave32 variant <c>.w32</c> of wave64-pinned compute
/// programs), the runtime headers and results.json.
/// </summary>
public sealed partial class CorpusTranslator
{
    public const string ResultsName = "results.json";

    /// <summary>The file suffix of the wave32 variant of a wave64-pinned compute program.</summary>
    public const string GroupWaveSuffix = ".w32.hlsl";

    /// <summary>The reason of a wave64-pinned compute program whose wave32 variant compiled.</summary>
    public const string GroupWaveReady = GroupWaveNote + " ok";

    /// <summary>The reason of a mesh program whose fallback also compiled (<see cref="CorpusOptions.MeshFallbacks"/>).</summary>
    public const string MeshFallbackReady = MeshFallbackNote + " ok";

    private const string Wave64Pin = "[WaveSize(64)]";
    private const string GroupWaveNote = "wave32 variant";
    private const string MeshFallbackNote = "fallback";

    private const int ShortReasonLength = 40;
    private const int CrashReasonLength = 80;
    private const int CompileReasonLength = 90;

    // Unsupported reasons that keep their detail; others are cut to their first word (the instruction).
    private static readonly string[] DetailedReasons = ["operand", "implicit", "export", "mask"];

    private readonly ProgramLoader _loader;
    private readonly IShaderCompiler? _compiler;
    private readonly MeshFallbackCompiler? _fallback;
    private readonly TranslationCache? _translations;
    private readonly IProgress<CorpusProgress>? _progress;

    /// <summary>
    /// Without a compiler the programs are only translated (status ok when the translation succeeds); without a
    /// translation cache every program is translated.
    /// </summary>
    public CorpusTranslator(
        ProgramLoader loader,
        IShaderCompiler? compiler,
        MeshFallbackCompiler? fallback,
        TranslationCache? translations,
        IProgress<CorpusProgress>? progress)
    {
        _loader = loader;
        _compiler = compiler;
        _fallback = fallback;
        _translations = translations;
        _progress = progress;
    }

    /// <summary>Runs every program (or, with a retry file, every program not final there) and writes results.json.</summary>
    public IReadOnlyList<ProgramResult> Run(IReadOnlyList<string> programPaths, CorpusOptions options)
    {
        Directory.CreateDirectory(options.OutputFolder);
        RuntimeIncludes.WriteTo(options.OutputFolder);
        IReadOnlyList<ProgramResult> prior = [];
        var paths = programPaths;

        if (options.RetryResults is not null)
        {
            prior = ResultsFile.Read(options.RetryResults);
            var final = prior.Where(result => ProgramStatus.IsFinal(result.Status)).Select(result => result.Name).ToHashSet();
            paths = programPaths.Where(path => !final.Contains(Path.GetFileNameWithoutExtension(path))).ToList();
        }

        var results = new ProgramResult[paths.Count];
        var parallel = new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, options.Parallelism) };
        var done = 0;
        Parallel.For(0, paths.Count, parallel, index =>
        {
            results[index] = Work(paths[index], options);
            _progress?.Report(new CorpusProgress(Interlocked.Increment(ref done), paths.Count));
        });

        var merged = Merge(prior, results);
        ResultsFile.Write(Path.Combine(options.OutputFolder, ResultsName), merged);

        return merged;
    }

    /// <summary>Earlier results in their order with re-run programs replaced; new programs at the end.</summary>
    private static List<ProgramResult> Merge(IReadOnlyList<ProgramResult> prior, ProgramResult[] results)
    {
        var merged = prior.ToList();
        var positions = new Dictionary<string, int>();

        for (var index = 0; index < merged.Count; index++)
        {
            positions[merged[index].Name] = index;
        }

        foreach (var result in results)
        {
            if (positions.TryGetValue(result.Name, out var position))
            {
                merged[position] = result;
            }
            else
            {
                positions[result.Name] = merged.Count;
                merged.Add(result);
            }
        }

        return merged;
    }

    private ProgramResult Work(string path, CorpusOptions options)
    {
        var translated = Translate(path, options);
        string? stageName = null;

        if (translated.Stage is not null)
        {
            stageName = ShaderStages.ProfileName(translated.Stage.Value);
        }

        if (translated.Hlsl is null)
        {
            return new ProgramResult(translated.Name, stageName, translated.Status ?? ProgramStatus.Crash, translated.Reason);
        }

        var hlslPath = Path.Combine(options.OutputFolder, translated.Name + ".hlsl");
        File.WriteAllText(hlslPath, translated.Hlsl);

        if (_compiler is null)
        {
            return new ProgramResult(translated.Name, stageName, ProgramStatus.Ok, null);
        }

        var (status, reason) = Compile(_compiler, hlslPath, translated.Hlsl, translated);

        if (status == ProgramStatus.Ok && translated.GroupWave is not null)
        {
            var variantPath = Path.Combine(options.OutputFolder, translated.Name + GroupWaveSuffix);
            reason = CompileGroupWave(_compiler, variantPath, translated, translated.GroupWave);
        }

        if (status == ProgramStatus.Ok && translated.Stage == ShaderStage.Mesh && options.MeshFallbacks && _fallback is not null)
        {
            reason = MeshFallbackReason(_fallback.Compile(hlslPath, translated.Hlsl, translated.Hlsl.Length > _compiler.Options.LargeSourceLength));
        }

        return new ProgramResult(translated.Name, stageName, status, reason);
    }

    /// <summary>The reason of an ok mesh program whose fallback was also compiled: <c>fallback ok</c> or why it failed.</summary>
    private static string MeshFallbackReason(string? error)
    {
        if (error is null)
        {
            return MeshFallbackReady;
        }

        return Cut($"{MeshFallbackNote}: {error}", CompileReasonLength);
    }

    /// <summary>The translation from the cache when it has one; otherwise a new one, kept when it is final.</summary>
    private TranslatedProgram Translate(string path, CorpusOptions options)
    {
        if (_translations is null)
        {
            return TranslateProgram(path, options);
        }

        var key = _translations.Key(path);
        var cached = _translations.Find(key, Path.GetFileNameWithoutExtension(path));

        if (cached is not null)
        {
            return SelectStage(cached, options.Stages);
        }

        var translated = TranslateProgram(path, options);

        if (translated.Hlsl is not null || translated.Status is ProgramStatus.Unsupported or ProgramStatus.Crash)
        {
            _translations.Store(key, translated);
        }

        return translated;
    }

    private static TranslatedProgram SelectStage(TranslatedProgram program, IReadOnlySet<ShaderStage>? stages)
    {
        if (stages is null || (program.Stage is not null && stages.Contains(program.Stage.Value)))
        {
            return program;
        }

        return program with { Hlsl = null, Status = ProgramStatus.SkipStage, Reason = null };
    }

    private TranslatedProgram TranslateProgram(string path, CorpusOptions options)
    {
        var loaded = _loader.Load(path, options.Stages);
        var program = loaded.Program;

        if (program is null)
        {
            return new TranslatedProgram(loaded.Name, loaded.Stage, null, loaded.Status ?? ProgramStatus.Crash, loaded.Reason, 0);
        }

        var count = program.Instructions.Count;

        try
        {
            var hlsl = HlslTranslator.Translate(program, options.Translation).Hlsl;

            return new TranslatedProgram(loaded.Name, loaded.Stage, hlsl, null, null, count) { GroupWave = GroupWave(program, hlsl, options.Translation) };
        }
        catch (UnsupportedProgramException exception)
        {
            return new TranslatedProgram(loaded.Name, loaded.Stage, null, ProgramStatus.Unsupported, UnsupportedReason(exception.Message), count);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            var reason = Cut($"{exception.GetType().Name}: {exception.Message}", CrashReasonLength);

            return new TranslatedProgram(loaded.Name, loaded.Stage, null, ProgramStatus.Crash, reason, count);
        }
    }

    /// <summary>
    /// The variant of a wave64-pinned compute program for GPUs without 64-lane waves
    /// (<see cref="HlslTranslator.GroupWaveVariant"/>), from the structured form where the graph allows it (WARP's shader
    /// compiler crashes on barriers inside the dispatch switch); null for every other program.
    /// </summary>
    private static GroupWaveVariant? GroupWave(ShaderProgram program, string hlsl, TranslationOptions options)
    {
        if (program.Stage != ShaderStage.Compute || !hlsl.Contains(Wave64Pin, StringComparison.Ordinal))
        {
            return null;
        }

        try
        {
            var structured = HlslTranslator.Translate(program, options with { StructuredControlFlow = true }).Hlsl;

            return new GroupWaveVariant(HlslTranslator.GroupWaveVariant(structured), null);
        }
        catch (UnsupportedProgramException exception)
        {
            return new GroupWaveVariant(null, Cut($"{GroupWaveNote}: {exception.Message}", CompileReasonLength));
        }
    }

    /// <summary>Writes and compiles <c>name.w32.hlsl</c>; the outcome becomes the reason of the ok result.</summary>
    private string CompileGroupWave(IShaderCompiler compiler, string path, TranslatedProgram program, GroupWaveVariant variant)
    {
        if (variant.Hlsl is null)
        {
            return variant.Reason ?? GroupWaveNote;
        }

        File.WriteAllText(path, variant.Hlsl);
        var (status, reason) = Compile(compiler, path, variant.Hlsl, program);

        if (status == ProgramStatus.Ok)
        {
            return GroupWaveReady;
        }

        return Cut($"{GroupWaveNote}: {status} {reason}", CompileReasonLength);
    }

    private (string Status, string? Reason) Compile(IShaderCompiler compiler, string hlslPath, string hlsl, TranslatedProgram program)
    {
        var profile = ShaderStages.ProfileName(program.Stage!.Value);
        var large = hlsl.Length > compiler.Options.LargeSourceLength;
        var outcome = compiler.Compile(new CompileRequest(hlslPath, hlsl, profile, large), compiler.Options.CompileTimeout);

        if (outcome.TimedOut)
        {
            outcome = compiler.Compile(new CompileRequest(hlslPath, hlsl, profile, DisableOptimizations: true), compiler.Options.RecompileTimeout);
        }

        if (outcome.TimedOut)
        {
            return (ProgramStatus.CompileTimeout, $"{program.Instructions} instructions");
        }

        if (outcome.Succeeded)
        {
            return (ProgramStatus.Ok, null);
        }

        var error = DxcCompiler.FirstError(outcome.Error);

        if (program.Stage == ShaderStage.Mesh && _fallback is not null)
        {
            // PC mesh output limit (32 KB) and similar: compute + vertex-pulling fallback.
            var fallbackError = _fallback.Compile(hlslPath, hlsl, large);

            if (fallbackError is null)
            {
                return (ProgramStatus.OkMeshFallback, null);
            }

            error = "fallback: " + fallbackError;
        }

        return (ProgramStatus.CompileFailure, Cut(ErrorPrefixPattern().Replace(error, string.Empty, 1), CompileReasonLength));
    }

    private static string UnsupportedReason(string message)
    {
        if (DetailedReasons.Any(prefix => message.StartsWith(prefix, StringComparison.Ordinal)))
        {
            return Cut(message, ShortReasonLength);
        }

        return message.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? message;
    }

    private static string Cut(string text, int length)
    {
        if (text.Length <= length)
        {
            return text;
        }

        return text[..length];
    }

    [GeneratedRegex(@"^.*?error: ")]
    private static partial Regex ErrorPrefixPattern();
}
