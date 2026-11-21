// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text.RegularExpressions;
using FiveStar.Shaders.Translation;

namespace FiveStar.Shaders.Compilation;

/// <summary>
/// Translates many FSSM programs and validates each with dxc: writes <c>&lt;name&gt;.hlsl</c> and <c>.dxil</c> (and the mesh
/// fallback pair <c>.msfb_cs</c>/<c>.msfb_vs</c> where needed), the runtime headers and results.json.
/// </summary>
public sealed partial class CorpusTranslator
{
    public const string ResultsName = "results.json";

    private const int ShortReasonLength = 40;
    private const int CrashReasonLength = 80;
    private const int CompileReasonLength = 90;

    // Unsupported reasons that keep their detail; others are cut to their first word (the instruction).
    private static readonly string[] DetailedReasons = ["operand", "implicit", "export", "mask"];

    private readonly ProgramLoader _loader;
    private readonly DxcCompiler? _compiler;
    private readonly MeshFallbackCompiler? _fallback;

    /// <summary>Without a compiler the programs are only translated (status ok when the translation succeeds).</summary>
    public CorpusTranslator(ProgramLoader loader, DxcCompiler? compiler, MeshFallbackCompiler? fallback)
    {
        _loader = loader;
        _compiler = compiler;
        _fallback = fallback;
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
        Parallel.For(0, paths.Count, parallel, index => results[index] = Work(paths[index], options));

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
        var loaded = _loader.Load(path, options.Stages);
        var stageName = loaded.Stage is null ? null : ShaderStages.ProfileName(loaded.Stage.Value);
        var program = loaded.Program;

        if (program is null)
        {
            return new ProgramResult(loaded.Name, stageName, loaded.Status ?? ProgramStatus.Crash, loaded.Reason);
        }

        string hlsl;

        try
        {
            hlsl = HlslTranslator.Translate(program).Hlsl;
        }
        catch (UnsupportedProgramException exception)
        {
            return new ProgramResult(loaded.Name, stageName, ProgramStatus.Unsupported, UnsupportedReason(exception.Message));
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return new ProgramResult(loaded.Name, stageName, ProgramStatus.Crash, Cut($"{exception.GetType().Name}: {exception.Message}", CrashReasonLength));
        }

        var hlslPath = Path.Combine(options.OutputFolder, loaded.Name + ".hlsl");
        File.WriteAllText(hlslPath, hlsl);

        if (_compiler is null)
        {
            return new ProgramResult(loaded.Name, stageName, ProgramStatus.Ok, null);
        }

        var (status, reason) = Compile(hlslPath, hlsl, program);

        return new ProgramResult(loaded.Name, stageName, status, reason);
    }

    private (string Status, string? Reason) Compile(string hlslPath, string hlsl, ShaderProgram program)
    {
        var compiler = _compiler!;
        var profile = ShaderStages.ProfileName(program.Stage);
        var large = hlsl.Length > compiler.Options.LargeSourceLength;
        var result = compiler.Compile(hlslPath, profile, large, compiler.Options.CompileTimeout);

        if (result.TimedOut)
        {
            result = compiler.Compile(hlslPath, profile, disableOptimizations: true, compiler.Options.RecompileTimeout);
        }

        if (result.TimedOut)
        {
            return (ProgramStatus.CompileTimeout, $"{program.Instructions.Count} instructions");
        }

        if (result.ExitCode == 0)
        {
            return (ProgramStatus.Ok, null);
        }

        var error = DxcCompiler.FirstError(result);

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
