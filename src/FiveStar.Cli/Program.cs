// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

namespace FiveStar.Cli;

/// <summary>Entry point of the <c>fivestar</c> command: the first argument picks a command group.</summary>
public static class Program
{
    private const int UsageError = 2;

    public static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            PrintUsage();

            return UsageError;
        }

        var rest = args[1..];

        switch (args[0])
        {
            case "archive":
                return ArchiveCommands.Run(rest);
            case "texture":
                return TextureCommands.Run(rest);
            case "platform":
                return PlatformCommands.Run(rest);
            case "shader":
                return ShaderCommands.Run(rest);
            case "graphics":
                return GraphicsCommands.Run(rest);
            case "progress":
                return ProgressCommands.Run(rest);
            default:
                PrintUsage();

                return UsageError;
        }
    }

    private static void PrintUsage()
    {
        Console.Error.WriteLine("usage: fivestar <archive|texture|platform|shader|graphics|progress> <command> [arguments]");
        Console.Error.WriteLine("       fivestar <group> help    lists the commands of a group");
    }
}
