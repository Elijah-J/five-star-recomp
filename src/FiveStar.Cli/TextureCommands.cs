// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using FiveStar.Cli.Archive;
using FiveStar.Cli.Texture;

namespace FiveStar.Cli;

/// <summary>The <c>fivestar texture</c> commands over streamed texture page files (<c>.ttpf</c>).</summary>
internal static class TextureCommands
{
    private const int Success = 0;
    private const int UsageError = 2;
    private const int TwoArguments = 2;

    private static readonly string[] Flags = ["--no-dds", "--stats", "--models"];

    public static int Run(IReadOnlyList<string> args)
    {
        if (args.Count == 0)
        {
            PrintHelp(Console.Error);

            return UsageError;
        }

        var arguments = new CommandArguments(args.Skip(1).ToList(), Flags);
        using var output = ConsoleOutput.Open();

        switch (args[0])
        {
            case "extract":
                return RunExtract(new TextureReports(output), arguments);
            case "coverage":
                return RunCoverage(new TextureReports(output), arguments);
            case "codecs":
                return RunCodecs(new TextureReports(output), arguments);
            case "pages":
                return RunPages(new PageReports(output), arguments);
            case "bcpack":
                return RunBcPack(new PageReports(output), arguments);
            case "help":
                return RunHelp(output);
            default:
                return RunUnknown();
        }
    }

    private static int RunExtract(TextureReports reports, CommandArguments arguments)
    {
        if (arguments.Positionals.Count < TwoArguments)
        {
            return Usage("texture extract <file.ttpf> <out dir> [--limit N] [--only i,j] [--no-dds]");
        }

        reports.Extract(arguments.Positionals[0], arguments.Positionals[1], arguments);

        return Success;
    }

    private static int RunCoverage(TextureReports reports, CommandArguments arguments)
    {
        if (arguments.Positionals.Count < 1)
        {
            return Usage("texture coverage <dir>");
        }

        reports.Coverage(arguments.Positionals[0]);

        return Success;
    }

    private static int RunCodecs(TextureReports reports, CommandArguments arguments)
    {
        if (arguments.Positionals.Count < 1)
        {
            return Usage("texture codecs <dir>");
        }

        reports.Codecs(arguments.Positionals[0]);

        return Success;
    }

    private static int RunPages(PageReports reports, CommandArguments arguments)
    {
        if (arguments.Positionals.Count < 1)
        {
            return Usage("texture pages <file.ttpf>");
        }

        reports.Pages(arguments.Positionals[0]);

        return Success;
    }

    private static int RunBcPack(PageReports reports, CommandArguments arguments)
    {
        if (arguments.Positionals.Count < 1)
        {
            return Usage("texture bcpack <file.ttpf> [--limit N] [--codec C] | texture bcpack <dir> --stats | --models");
        }

        if (arguments.HasFlag("--stats"))
        {
            reports.BcPackStats(arguments.Positionals[0]);
        }
        else if (arguments.HasFlag("--models"))
        {
            reports.BcPackModels(arguments.Positionals[0]);
        }
        else
        {
            reports.BcPackPages(arguments.Positionals[0], arguments);
        }

        return Success;
    }

    private static int RunHelp(TextWriter output)
    {
        PrintHelp(output);

        return Success;
    }

    private static int RunUnknown()
    {
        PrintHelp(Console.Error);

        return UsageError;
    }

    private static int Usage(string usage)
    {
        Console.Error.WriteLine("usage: fivestar " + usage);

        return UsageError;
    }

    private static void PrintHelp(TextWriter output)
    {
        output.WriteLine("fivestar texture <command>");
        output.WriteLine("  extract  <file.ttpf> <out> [--limit N] [--only i,j] [--no-dds]   mip 0 of every texture as DDS");
        output.WriteLine("  coverage <dir>                     share of textures readable without BCPack, per file and in total");

        output.WriteLine("  codecs   <dir>                     pages and stored bytes per storage kind (zlib, stored, codecN)");
        output.WriteLine("  pages    <file.ttpf>               sample page-table entries per flag value with their storage kind");
        output.WriteLine("  bcpack   <file.ttpf> [--limit N] [--codec C]   BCPack page headers and near-constant lane checks");
        output.WriteLine("  bcpack   <dir> --stats             lane check totals over every page file");
        output.WriteLine("  bcpack   <dir> --models            model use per codec and lane, and the twin-lane check");
    }
}
