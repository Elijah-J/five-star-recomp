// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using FiveStar.Shaders.Dxil;
using FiveStar.Shaders.Dxil.Validation;

namespace FiveStar.Cli.Shader;

/// <summary>
/// <c>fivestar shader dxil</c>: compiles translated HLSL with the DXIL backend (no dxc), optionally validates each
/// container with dxil.dll, and reports coverage, failure reasons and compile times. Each program's line goes to
/// <c>&lt;out&gt;/dxil-backend.tsv</c>: name, source characters, milliseconds, DXIL bytes, status, reason.
/// </summary>
internal static class DxilCommand
{
    private const int Success = 0;
    private const int OnePath = 1;
    private const int TopReasons = 25;
    private const int ReasonLength = 160;
    private const string ValidateFlag = "validate";
    private const string PhasesFlag = "phases";
    private const string LogName = "dxil-backend.tsv";

    public static int Run(IReadOnlyList<string> args)
    {
        var arguments = new CommandArguments(args, new HashSet<string> { ValidateFlag, PhasesFlag });

        if (arguments.Positional.Count != OnePath)
        {
            throw new ArgumentException("dxil <hlsl file or folder> [--out <dir>] [--validate] [--limit N] [--step K] [--jobs N] [--repeat N] [--dxc <path>]");
        }

        var files = Sources(arguments);
        var output = arguments.Option("out");
        using var validator = Validator(arguments);
        var results = new ConcurrentBag<Outcome>();
        var options = new ParallelOptions { MaxDegreeOfParallelism = arguments.IntegerOption("jobs") ?? 1 };

        if (output is not null)
        {
            Directory.CreateDirectory(output);
        }

        var repeat = arguments.IntegerOption("repeat") ?? 1;

        if (arguments.Flag(PhasesFlag))
        {
            Phases(files, repeat, arguments.Option("ir"));

            return Success;
        }

        Parallel.ForEach(files, options, file => results.Add(CompileOne(file, output, validator, repeat)));
        Report([.. results.OrderBy(result => result.Name, StringComparer.Ordinal)], output);

        return Success;
    }

    private static List<string> Sources(CommandArguments arguments)
    {
        var path = arguments.Positional[0];
        IEnumerable<string> files = [path];

        if (Directory.Exists(path))
        {
            files = Directory.EnumerateFiles(path, "*.hlsl").Order(StringComparer.Ordinal);
        }

        var step = arguments.IntegerOption("step") ?? 1;
        var selected = files.Where((_, index) => index % step == 0);
        var limit = arguments.IntegerOption("limit");

        if (limit is not null)
        {
            selected = selected.Take(limit.Value);
        }

        return [.. selected.Where(IsCompute)];
    }

    /// <summary>A compute program: numthreads and no mesh output topology.</summary>
    private static bool IsCompute(string file)
    {
        var text = File.ReadAllText(file);

        return text.Contains("[numthreads(", StringComparison.Ordinal) && !text.Contains("outputtopology", StringComparison.Ordinal);
    }

    private static DxilValidator? Validator(CommandArguments arguments)
    {
        if (!arguments.Flag(ValidateFlag))
        {
            return null;
        }

        var folder = Path.GetDirectoryName(ShaderTools.Dxc(arguments)) ?? string.Empty;

        return DxilValidator.Load(Path.Combine(folder, "dxil.dll"));
    }

    /// <summary>Compiles one program <paramref name="repeat"/> times (the last run is timed, after JIT warm-up).</summary>
    private static Outcome CompileOne(string file, string? output, DxilValidator? validator, int repeat)
    {
        var name = Path.GetFileNameWithoutExtension(file);
        var hlsl = File.ReadAllText(file);
        var clock = Stopwatch.StartNew();
        byte[] dxil = [];

        try
        {
            for (var run = 0; run < repeat; run++)
            {
                clock.Restart();
                dxil = DxilBackend.Compile(hlsl, "cs");
            }
        }
        catch (DxilBackendException exception)
        {
            return new Outcome(name, hlsl.Length, clock.Elapsed.TotalMilliseconds, 0, "fail", exception.Message);
        }

        var milliseconds = clock.Elapsed.TotalMilliseconds;

        if (output is not null)
        {
            File.WriteAllBytes(Path.Combine(output, name + ".dxil"), dxil);
        }

        var errors = validator?.Validate((byte[])dxil.Clone());

        if (errors is not null)
        {
            return new Outcome(name, hlsl.Length, milliseconds, dxil.Length, "invalid", errors.Replace('\n', ' '));
        }

        return new Outcome(name, hlsl.Length, milliseconds, dxil.Length, "ok", string.Empty);
    }

    /// <summary>Times the backend's phases on each program (the last of <paramref name="repeat"/> runs).</summary>
    private static void Phases(List<string> files, int repeat, string? listing)
    {
        foreach (var file in files)
        {
            var hlsl = File.ReadAllText(file);
            var times = new double[PhaseCount];

            for (var run = 0; run < repeat; run++)
            {
                var a0 = GC.GetAllocatedBytesForCurrentThread();
                var clock = Stopwatch.StartNew();
                var tokens = FiveStar.Shaders.Dxil.Hlsl.Preprocessor.Run(hlsl, FiveStar.Shaders.Translation.RuntimeIncludes.Read);
                times[0] = clock.Elapsed.TotalMilliseconds;
                var tree = FiveStar.Shaders.Dxil.Hlsl.Parser.Parse(tokens);
                times[1] = clock.Elapsed.TotalMilliseconds;
                var a1 = GC.GetAllocatedBytesForCurrentThread();
                var (module, main, description) = FiveStar.Shaders.Dxil.Lowering.ShaderLowering.Compute(tree);
                times[PhaseLower] = clock.Elapsed.TotalMilliseconds;
                var a2 = GC.GetAllocatedBytesForCurrentThread();
                var container = FiveStar.Shaders.Dxil.Container.ShaderContainer.Write(module, main, description);
                times[PhaseWrite] = clock.Elapsed.TotalMilliseconds;
                _ = container;

                if (run == repeat - 1 && listing is not null)
                {
                    File.WriteAllText(listing, FiveStar.Shaders.Dxil.Ir.IrText.Print(main));
                }

                if (run == repeat - 1)
                {
                    Console.WriteLine($"alloc MB: front {(a1 - a0) / 1000000}, lower {(a2 - a1) / 1000000}, write {(GC.GetAllocatedBytesForCurrentThread() - a2) / 1000000}");
                    Console.WriteLine($"blocks {main.Blocks.Count}, instructions {main.Blocks.Sum(block => block.Body.Count)}, phis {main.Blocks.Sum(block => block.Phis.Count)}, constants {module.Constants.Count}, bytes {container.Length}");
                }
            }

            Console.WriteLine($"gc0 {GC.CollectionCount(0)} gc1 {GC.CollectionCount(1)} gc2 {GC.CollectionCount(2)} allocated {GC.GetTotalAllocatedBytes() / 1000000} MB");
            Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{Path.GetFileName(file)}: preprocess {times[0]:F1}, parse {times[1] - times[0]:F1}, lower {times[PhaseLower] - times[1]:F1}, write {times[PhaseWrite] - times[PhaseLower]:F1} ms"));
        }
    }

    private const int PhaseCount = 4;
    private const int PhaseLower = 2;
    private const int PhaseWrite = 3;

    private static void Report(List<Outcome> results, string? output)
    {
        if (output is not null)
        {
            var lines = results.Select(result => string.Create(
                CultureInfo.InvariantCulture,
                $"{result.Name}\t{result.Characters}\t{result.Milliseconds:F1}\t{result.Bytes}\t{result.Status}\t{result.Reason}"));
            File.WriteAllLines(Path.Combine(output, LogName), lines);
        }

        foreach (var group in results.GroupBy(result => result.Status).OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            var times = group.Select(result => result.Milliseconds).Order().ToList();
            var median = times[times.Count / 2];
            Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{group.Key}: {group.Count()} programs, {times.Sum() / 1000:F1} s, median {median:F1} ms, max {times[^1]:F0} ms"));
        }

        var reasons = results.Where(result => result.Status != "ok")
            .GroupBy(result => Normalize(result.Reason))
            .OrderByDescending(group => group.Count())
            .Take(TopReasons);

        foreach (var reason in reasons)
        {
            Console.WriteLine($"  {reason.Count(),5}  {reason.Key}   e.g. {reason.First().Name}");
        }
    }

    /// <summary>A reason without line numbers and names, so equal causes group.</summary>
    private static string Normalize(string reason)
    {
        var text = System.Text.RegularExpressions.Regex.Replace(reason, @"line \d+: ", string.Empty);
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\d+", "#");

        return text[..Math.Min(text.Length, ReasonLength)];
    }

    private sealed record Outcome(string Name, int Characters, double Milliseconds, int Bytes, string Status, string Reason);
}
