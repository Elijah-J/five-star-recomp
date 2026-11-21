// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text.RegularExpressions;
using FiveStar.Shaders.Compilation;
using FiveStar.Shaders.Hardware;

namespace FiveStar.Shaders.Analysis;

/// <summary>
/// Classifies translated programs by wave-size dependence (wave64 programs on wave32-only GPUs): programs without wave
/// or quad intrinsics are agnostic; lane-index and EXEC bookkeeping can be fixed mechanically; quad operations and
/// first-lane reads stay correct; lane-indexed data movement (readlane, permlane, bpermute, DPP rows) needs wave64
/// emulation or a fixed wave size.
/// </summary>
public static partial class WaveUsageAnalyzer
{
    private const string EntryMarker = "main(";

    /// <summary>Every ok program of a corpus run with its classification.</summary>
    public static IReadOnlyList<WaveUsageEntry> Analyze(string outputFolder, string programFolder)
    {
        var entries = new List<WaveUsageEntry>();

        foreach (var result in ResultsFile.Read(Path.Combine(outputFolder, CorpusTranslator.ResultsName)))
        {
            if (result.Status is not ProgramStatus.Ok and not ProgramStatus.OkMeshFallback)
            {
                continue;
            }

            var programPath = Path.Combine(programFolder, result.Name + ".fssm");
            var hlslPath = Path.Combine(outputFolder, result.Name + ".hlsl");

            if (!File.Exists(programPath) || !File.Exists(hlslPath))
            {
                continue;
            }

            var wave = WaveSize(programPath);

            if (wave is not null)
            {
                entries.Add(Classify(result, wave.Value, File.ReadAllText(hlslPath)));
            }
        }

        return entries;
    }

    /// <summary>Classifies the code after the entry point (helpers above it are not counted).</summary>
    public static WaveUsageEntry Classify(ProgramResult result, int waveSize, string hlsl)
    {
        var entry = hlsl.IndexOf(EntryMarker, StringComparison.Ordinal);
        var body = entry < 0 ? hlsl[^1..] : hlsl[entry..];
        var data = DataPattern().Matches(body).Select(match => match.Groups[1].Value).ToList();
        var bookkeeping = BookkeepingPattern().IsMatch(body);
        var dependence = WaveDependence.Agnostic;

        if (data.Any(IsLaneIndexed))
        {
            dependence = WaveDependence.LaneIndexed;
        }
        else if (data.Count > 0)
        {
            dependence = WaveDependence.QuadAndFirstLane;
        }
        else if (bookkeeping)
        {
            dependence = WaveDependence.Bookkeeping;
        }

        return new WaveUsageEntry(result.Name, result.Stage, waveSize, dependence, data.ToHashSet());
    }

    private static int? WaveSize(string programPath)
    {
        try
        {
            return FssmHeader.Decode(File.ReadAllBytes(programPath)).WaveSize;
        }
        catch (InvalidDataException)
        {
            return null;
        }
    }

    /// <summary>The report label of a classification.</summary>
    public static string Label(WaveDependence dependence)
    {
        switch (dependence)
        {
            case WaveDependence.LaneIndexed:
                return "lane-indexed / wave-wide data";
            case WaveDependence.QuadAndFirstLane:
                return "quad + first-lane only";
            case WaveDependence.Bookkeeping:
                return "lane/exec bookkeeping only";
            default:
                return "wave-size agnostic";
        }
    }

    private static bool IsLaneIndexed(string operation)
    {
        string[] wavePrefixes = ["WaveMultiPrefix", "WavePrefix", "WaveActive"];

        return operation is "WaveReadLaneAt" or "WaveMatch" || wavePrefixes.Any(prefix => operation.StartsWith(prefix, StringComparison.Ordinal));
    }

    [GeneratedRegex(@"\b(WaveReadLaneAt|WaveReadLaneFirst|WaveActive(?:Sum|Product|Min|Max|BitAnd|BitOr|BitXor|AllEqual)|WavePrefix\w+|WaveMultiPrefix\w+|WaveMatch|QuadRead\w+|quad_perm|readfirstlane)\s*\(")]
    private static partial Regex DataPattern();

    [GeneratedRegex(@"\b(WaveActiveBallot|WaveActiveCountBits|WaveActiveAnyTrue|WaveActiveAllTrue|WaveGetLaneIndex|lane_bit|WaveIsFirstLane)\s*\(")]
    private static partial Regex BookkeepingPattern();
}
