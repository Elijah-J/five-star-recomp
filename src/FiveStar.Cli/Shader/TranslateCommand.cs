// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using FiveStar.Shaders.Compilation;
using FiveStar.Shaders.Disassembly;
using FiveStar.Shaders.Translation;

namespace FiveStar.Cli.Shader;

/// <summary><c>fivestar shader translate</c>: translates FSSM programs to HLSL, validates them with dxc and writes results.json.</summary>
internal static class TranslateCommand
{
    private const int Success = 0;
    private const int Failure = 1;
    private const int ReasonsShown = 25;
    private const int CountWidth = 6;
    private const int TwoPaths = 2;
    private const int BytesPerMebibyte = 1 << 20;
    private const string NoCompileFlag = "no-compile";
    private const string StructuredFlag = "structured";

    public static int Run(IReadOnlyList<string> args)
    {
        var arguments = new CommandArguments(args, new HashSet<string> { NoCompileFlag, StructuredFlag });

        if (arguments.Positional.Count != TwoPaths)
        {
            throw new ArgumentException(
                "translate <fssm-or-folder> <out> [--cache <dir>] [--retry results.json] [--stages ps,vs,cs,ms,gs] [--jobs N] [--structured]");
        }

        var programs = Programs(arguments);
        var retry = arguments.Option("retry");
        var llvm = ShaderTools.Llvm(arguments);
        var options = new CorpusOptions(arguments.Positional[1], Stages(arguments), arguments.IntegerOption("jobs") ?? Environment.ProcessorCount, retry)
        {
            Translation = new TranslationOptions(arguments.Flag(StructuredFlag), PinWaveSize: true),
        };

        var cacheFolder = arguments.Option("cache");
        CacheFolder? cache = null;
        TranslationCache? translations = null;
        IShaderCompiler? compiler = null;

        if (cacheFolder is not null)
        {
            cache = new CacheFolder(cacheFolder);
            translations = new TranslationCache(cache, TranslationCache.Identity(llvm, options.Translation));
        }

        if (!arguments.Flag(NoCompileFlag))
        {
            compiler = ShaderTools.Compiler(arguments, retry is not null);
            compiler = Cached(compiler, cache);
        }

        var fallback = compiler is null ? null : new MeshFallbackCompiler(compiler);
        var progress = new ConsoleProgress(() => Counts(compiler, translations));
        var loader = new ProgramLoader(new LlvmDisassembler(llvm));
        var results = new CorpusTranslator(loader, compiler, fallback, translations, progress).Run(programs, options);
        Report(results);

        if (cache is not null)
        {
            Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"cache {cache.Root}: {cache.Size() / BytesPerMebibyte} MiB"));
        }

        if (results.Any(result => !ProgramStatus.IsFinal(result.Status)))
        {
            return Failure;
        }

        return Success;
    }

    private static IShaderCompiler Cached(IShaderCompiler compiler, CacheFolder? cache)
    {
        if (cache is null)
        {
            return compiler;
        }

        return new CachedCompiler(compiler, cache);
    }

    private static string Counts(IShaderCompiler? compiler, TranslationCache? translations)
    {
        var text = string.Empty;

        if (translations is not null)
        {
            text = string.Create(CultureInfo.InvariantCulture, $"translations reused {translations.Hits}, new {translations.Misses}  ");
        }

        if (compiler is CachedCompiler cached)
        {
            text += string.Create(CultureInfo.InvariantCulture, $"compiles reused {cached.Reused}, run {cached.Compiled}");
        }

        return text;
    }

    private static List<string> Programs(CommandArguments arguments)
    {
        var input = arguments.Positional[0];
        List<string> programs = [input];

        if (Directory.Exists(input))
        {
            programs = Directory.GetFiles(input, "*.fssm").Order(StringComparer.Ordinal).ToList();
        }

        var step = arguments.IntegerOption("step") ?? 1;
        programs = programs.Where((_, index) => index % step == 0).ToList();
        var limit = arguments.IntegerOption("limit");

        if (limit is not null)
        {
            programs = programs.Take(limit.Value).ToList();
        }

        return programs;
    }

    private static HashSet<ShaderStage>? Stages(CommandArguments arguments)
    {
        var list = arguments.Option("stages");

        if (list is null)
        {
            return null;
        }

        var stages = new HashSet<ShaderStage>();

        foreach (var name in list.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var stage = ShaderStages.FromProfileName(name) ?? throw new ArgumentException($"unknown stage {name}");
            stages.Add(stage);
        }

        return stages;
    }

    private static void Report(IReadOnlyList<ProgramResult> results)
    {
        var counts = results.GroupBy(result => result.Status).Select(group => $"{group.Key} {group.Count()}");
        Console.WriteLine("results: " + string.Join(", ", counts));
        var failures = results.Where(result => !ProgramStatus.IsFinal(result.Status)).GroupBy(result => (result.Status, result.Reason));

        foreach (var group in failures.OrderByDescending(group => group.Count()).Take(ReasonsShown))
        {
            var count = group.Count().ToString(CultureInfo.InvariantCulture).PadLeft(CountWidth);
            Console.WriteLine($"  {count} {group.Key.Status}: {group.Key.Reason}  e.g. {group.First().Name}");
        }
    }
}
