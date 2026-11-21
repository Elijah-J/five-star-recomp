// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

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
    private const string NoCompileFlag = "no-compile";

    public static int Run(IReadOnlyList<string> args)
    {
        var arguments = new CommandArguments(args, new HashSet<string> { NoCompileFlag });

        if (arguments.Positional.Count != TwoPaths)
        {
            throw new ArgumentException("translate <fssm-or-folder> <out> [--retry results.json] [--stages ps,vs,cs,ms,gs] [--jobs N]");
        }

        var programs = Programs(arguments);
        var retry = arguments.Option("retry");
        var loader = new ProgramLoader(new LlvmDisassembler(ShaderTools.Llvm(arguments)));
        DxcCompiler? compiler = null;
        MeshFallbackCompiler? fallback = null;

        if (!arguments.Flag(NoCompileFlag))
        {
            compiler = ShaderTools.Compiler(arguments, retry is not null);
            fallback = new MeshFallbackCompiler(compiler);
        }

        var options = new CorpusOptions(arguments.Positional[1], Stages(arguments), arguments.IntegerOption("jobs") ?? Environment.ProcessorCount, retry);
        var results = new CorpusTranslator(loader, compiler, fallback).Run(programs, options);
        Report(results);

        if (results.Any(result => !ProgramStatus.IsFinal(result.Status)))
        {
            return Failure;
        }

        return Success;
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
            var count = group.Count().ToString(System.Globalization.CultureInfo.InvariantCulture).PadLeft(CountWidth);
            Console.WriteLine($"  {count} {group.Key.Status}: {group.Key.Reason}  e.g. {group.First().Name}");
        }
    }
}
