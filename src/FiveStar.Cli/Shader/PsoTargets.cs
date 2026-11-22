// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using FiveStar.Shaders.Compilation;

namespace FiveStar.Cli.Shader;

/// <summary>
/// The pipelines of a translate run: one per <c>ok</c> program (<c>&lt;name&gt;.dxil</c>) and two per <c>ok-msfb</c> mesh
/// program (<c>.msfb_cs.dxil</c>, <c>.msfb_vs.dxil</c>), in results.json order, filtered by <c>--stages</c> and then
/// thinned by <c>--step</c> and <c>--limit</c> as programs.
/// </summary>
internal static class PsoTargets
{
    private const string MeshFallbackCompute = "msfb_cs";
    private const string MeshFallbackVertex = "msfb_vs";

    public static List<PsoTarget> Read(string folder, CommandArguments arguments)
    {
        var stages = Stages(arguments.Option("stages"));
        var programs = ResultsFile.Read(Path.Combine(folder, CorpusTranslator.ResultsName))
            .Where(result => result.Status is ProgramStatus.Ok or ProgramStatus.OkMeshFallback)
            .Where(result => result.Stage is not null && (stages is null || stages.Contains(result.Stage)))
            .ToList();
        var step = arguments.IntegerOption("step") ?? 1;
        programs = programs.Where((_, index) => index % step == 0).ToList();
        var limit = arguments.IntegerOption("limit");

        if (limit is not null)
        {
            programs = programs.Take(limit.Value).ToList();
        }

        return programs.SelectMany(result => Targets(folder, result)).ToList();
    }

    private static IEnumerable<PsoTarget> Targets(string folder, ProgramResult result)
    {
        var stem = Path.Combine(folder, result.Name);

        if (result.Status == ProgramStatus.OkMeshFallback)
        {
            yield return new PsoTarget(result.Name, MeshFallbackCompute, $"{stem}.{MeshFallbackCompute}.dxil");

            yield return new PsoTarget(result.Name, MeshFallbackVertex, $"{stem}.{MeshFallbackVertex}.dxil");

            yield break;
        }

        yield return new PsoTarget(result.Name, result.Stage!, stem + ".dxil");
    }

    private static HashSet<string>? Stages(string? list)
    {
        if (list is null)
        {
            return null;
        }

        return list.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet(StringComparer.Ordinal);
    }
}
