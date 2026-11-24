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
    private const string LogName = "dxil-backend.tsv";

    public static int Run(IReadOnlyList<string> args)
    {
        var arguments = new CommandArguments(args, new HashSet<string> { ValidateFlag });

        if (arguments.Positional.Count != OnePath)
        {
            throw new ArgumentException("dxil <hlsl file or folder> [--out <dir>] [--validate] [--limit N] [--step K] [--jobs N] [--dxc <path>]");
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

        Parallel.ForEach(files, options, file => results.Add(CompileOne(file, output, validator)));
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

    private static Outcome CompileOne(string file, string? output, DxilValidator? validator)
    {
        var name = Path.GetFileNameWithoutExtension(file);
        var hlsl = File.ReadAllText(file);
        var clock = Stopwatch.StartNew();
        byte[] dxil;

        try
        {
            dxil = DxilBackend.Compile(hlsl, "cs");
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
