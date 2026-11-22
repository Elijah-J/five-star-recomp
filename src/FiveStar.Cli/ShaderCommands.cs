// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using FiveStar.Cli.Shader;
using FiveStar.Shaders.Containers;
using FiveStar.Shaders.Disassembly;
using FiveStar.Shaders.Hardware;

namespace FiveStar.Cli;

/// <summary>The <c>fivestar shader</c> commands.</summary>
internal static class ShaderCommands
{
    private const int Success = 0;
    private const int Failure = 1;
    private const int UsageError = 2;
    private const int Wave64 = 64;
    private const int OnePath = 1;
    private const int TwoPaths = 2;
    private const int AddressDigits = 6;
    private const int SizeWidth = 2;

    private static readonly string[] Usage =
    [
        "usage: fivestar shader <command> [arguments]",
        "  extract <shaders.rpf> <out>            write <out>/dxbc/*.dxbc (+ _manifest.tsv) and <out>/fssm/*.fssm",
        "  disasm <program.fssm> [--count N]      list the program's instructions (address, size, text)",
        "  translate <fssm-or-folder> <out>       translate to HLSL and compile with dxc, write <out>/results.json",
        "        [--cache <dir>] [--retry results.json] [--stages ps,vs,cs,ms,gs] [--jobs N] [--limit N] [--step K] [--no-compile] [--structured]",
        "  waves <out> <fssm-folder>              classify translated programs by wave-size dependence",
        "  pso <out> [--adapter <index|name>]     create a D3D12 pipeline state per compiled program, write <out>/pso-<adapter>.jsonl",
        "        [--stages ...] [--limit N] [--step K] [--jobs N] [--timeout S] [--max-memory GB]",
        "        [--debug-layer] [--recheck]",
        "  pso --list-adapters                    list the adapters with wave lane counts, shader model and mesh tier",
        "  unwrap <in.dxbc> <out.dxbc>            rewrite a container whose XDAT holds bitcode as standard DXBC",
        "  psocache <shaders.rpf | blob>          summarise the driver pipeline cache and match its shaders to programs",
        "        [--programs <fssm folder>] [--out <dir>]   (programs default to FIVESTAR_SHADER_DIR; --out writes code/ and pipelines.json)",
        "  tool options: --llvm <bin folder> (or FIVESTAR_LLVM), --dxc <dxc path> (or FIVESTAR_DXC)",
    ];

    public static int Run(IReadOnlyList<string> args)
    {
        if (args.Count == 0)
        {
            PrintUsage();

            return UsageError;
        }

        var rest = args.Skip(1).ToList();

        try
        {
            switch (args[0])
            {
                case "extract":
                    return Extract(rest);
                case "disasm":
                    return Disassemble(rest);
                case "translate":
                    return TranslateCommand.Run(rest);
                case "waves":
                    return WaveReport.Run(rest);
                case "pso":
                    return PsoCommand.Run(rest);
                case "unwrap":
                    return Unwrap(rest);
                case "psocache":
                    return PipelineCacheCommand.Run(rest);
                case "help":
                    return Help();
                default:
                    return Unknown();
            }
        }
        catch (ArgumentException exception)
        {
            Console.Error.WriteLine($"fivestar shader: {exception.Message}");

            return UsageError;
        }
    }

    private static int Unknown()
    {
        PrintUsage();

        return UsageError;
    }

    private static int Help()
    {
        PrintUsage();

        return Success;
    }

    private static void PrintUsage()
    {
        foreach (var line in Usage)
        {
            Console.Error.WriteLine(line);
        }
    }

    private static int Extract(IReadOnlyList<string> args)
    {
        var arguments = new CommandArguments(args, new HashSet<string>());

        if (arguments.Positional.Count != TwoPaths)
        {
            throw new ArgumentException("extract <shaders.rpf> <out>");
        }

        var summary = ShaderArchiveExtractor.Extract(arguments.Positional[0], arguments.Positional[1]);
        Console.WriteLine($"records {summary.Records}, unique containers {summary.Containers}, programs {summary.Programs}, failed {summary.ProgramFailures}");

        if (summary.ProgramFailures > 0)
        {
            return Failure;
        }

        return Success;
    }

    private static int Disassemble(IReadOnlyList<string> args)
    {
        var arguments = new CommandArguments(args, new HashSet<string>());

        if (arguments.Positional.Count != OnePath)
        {
            throw new ArgumentException("disasm <program.fssm> [--count N]");
        }

        var program = File.ReadAllBytes(arguments.Positional[0]);
        var config = FssmHeader.Decode(program);
        var disassembler = new LlvmDisassembler(ShaderTools.Llvm(arguments));
        var instructions = disassembler.Disassemble(FssmHeader.ProgramCode(program), config.WaveSize ?? Wave64);
        var count = arguments.IntegerOption("count") ?? instructions.Count;

        foreach (var instruction in instructions.Take(count))
        {
            var address = instruction.Address.ToString("x" + AddressDigits.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
            Console.WriteLine($"{address} {instruction.Size.ToString(CultureInfo.InvariantCulture).PadLeft(SizeWidth)}  {instruction.Text}");
        }

        return Success;
    }

    private static int Unwrap(IReadOnlyList<string> args)
    {
        var arguments = new CommandArguments(args, new HashSet<string>());

        if (arguments.Positional.Count != TwoPaths)
        {
            throw new ArgumentException("unwrap <in.dxbc> <out.dxbc>");
        }

        File.WriteAllBytes(arguments.Positional[1], XdatUnwrapper.Unwrap(File.ReadAllBytes(arguments.Positional[0])));

        return Success;
    }
}
