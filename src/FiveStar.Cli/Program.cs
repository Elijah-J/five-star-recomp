// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using FiveStar.Cli.Setup;

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
            case "audio":
                return AudioCommands.Run(rest);
            case "texture":
                return TextureCommands.Run(rest);
            case "platform":
                return PlatformCommands.Run(rest);
            case "shader":
                return ShaderCommands.Run(rest);
            case "graphics":
                return GraphicsCommands.Run(rest);
            case "model":
                return ModelCommands.Run(rest);
            case "progress":
                return ProgressCommands.Run(rest);
            case "setup":
                return SetupCommand.Run(rest);
            default:
                PrintUsage();

                return UsageError;
        }
    }

    private static void PrintUsage()
    {
        Console.Error.WriteLine("usage: fivestar setup <dump folder> [options]     prepare everything the port needs (fivestar setup help)");
        Console.Error.WriteLine("       fivestar <archive|audio|texture|platform|shader|graphics|model|progress> <command> [arguments]");
        Console.Error.WriteLine("       fivestar <group> help    lists the commands of a group");
    }
}
