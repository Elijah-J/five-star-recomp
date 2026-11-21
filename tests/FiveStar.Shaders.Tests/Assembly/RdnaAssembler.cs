// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using FiveStar.Shaders.Disassembly;
using FiveStar.Shaders.Tooling;

namespace FiveStar.Shaders.Tests.Assembly;

/// <summary>
/// Assembles a handwritten gfx1013 program (llvm-mc syntax) to code bytes, then disassembles them with the same
/// <see cref="LlvmDisassembler"/> the real pipeline uses, so the translator sees exactly what it sees for a dump.
/// </summary>
public sealed class RdnaAssembler
{
    private const int Wave64 = 64;

    private readonly LlvmToolPaths _tools;
    private readonly string _folder;

    public RdnaAssembler(LlvmToolPaths tools, string folder)
    {
        _tools = tools;
        _folder = folder;
    }

    public AssembledProgram Assemble(string name, string source, int waveSize)
    {
        var code = Code(name, source, waveSize);

        return new AssembledProgram(code, new LlvmDisassembler(_tools).Disassemble(code, waveSize));
    }

    private byte[] Code(string name, string source, int waveSize)
    {
        var sourcePath = Path.Combine(_folder, name + ".s");
        var objectPath = Path.Combine(_folder, name + ".o");
        File.WriteAllText(sourcePath, ".text\n" + source + "\n");
        var arguments = new List<string> { "-triple=amdgcn", $"-mcpu={_tools.Cpu}" };

        if (waveSize == Wave64)
        {
            arguments.Add("-mattr=+wavefrontsize64");
        }

        arguments.AddRange(["-filetype=obj", sourcePath, "-o", objectPath]);
        var result = ExternalTool.Run(_tools.Assembler, arguments, timeout: null);

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"llvm-mc rejected {name}: {result.Error.Trim()}");
        }

        return ElfText.Read(File.ReadAllBytes(objectPath));
    }
}
