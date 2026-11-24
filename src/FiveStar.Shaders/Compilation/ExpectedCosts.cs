// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;

namespace FiveStar.Shaders.Compilation;

/// <summary>
/// What each program of a run is expected to cost, in milliseconds of dxc: its compiles' time in the output folder's
/// <see cref="TimedCompiler.LogName"/> from an earlier run, else its FSSM size at the logged programs' milliseconds per
/// byte (one when nothing is logged, then only the proportions mean anything). dxc time grows far faster than the
/// program count suggests: the largest programs take minutes, most take a second.
/// </summary>
public static class ExpectedCosts
{
    private const int NameField = 0;
    private const int MillisecondsField = 5;

    /// <summary>The cost of each of <paramref name="paths"/>, in their order.</summary>
    public static double[] Of(IReadOnlyList<string> paths, string outputFolder)
    {
        var sizes = paths.Select(path => File.Exists(path) ? new FileInfo(path).Length : 0).ToArray();
        var logged = Logged(Path.Combine(outputFolder, TimedCompiler.LogName));
        long loggedBytes = 0;
        double loggedMilliseconds = 0;

        for (var index = 0; index < paths.Count; index++)
        {
            if (logged.TryGetValue(Path.GetFileNameWithoutExtension(paths[index]), out var milliseconds))
            {
                loggedBytes += sizes[index];
                loggedMilliseconds += milliseconds;
            }
        }

        var perByte = loggedBytes > 0 ? loggedMilliseconds / loggedBytes : 1;

        return paths.Select((path, index) => logged.TryGetValue(Path.GetFileNameWithoutExtension(path), out var milliseconds) ? milliseconds : sizes[index] * perByte).ToArray();
    }

    /// <summary>Milliseconds per program in a compile log (every form of a program counts towards it); empty without one.</summary>
    private static Dictionary<string, double> Logged(string path)
    {
        var programs = new Dictionary<string, double>(StringComparer.Ordinal);

        if (!File.Exists(path))
        {
            return programs;
        }

        using var reader = new StreamReader(File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));

        while (reader.ReadLine() is { } line)
        {
            var fields = line.Split('\t');

            if (fields.Length <= MillisecondsField || !double.TryParse(fields[MillisecondsField], NumberStyles.Float, CultureInfo.InvariantCulture, out var milliseconds))
            {
                continue;
            }

            var name = fields[NameField];
            var stem = name[..Math.Max(0, name.IndexOf('.', StringComparison.Ordinal))];
            programs[stem] = programs.GetValueOrDefault(stem) + milliseconds;
        }

        return programs;
    }
}
