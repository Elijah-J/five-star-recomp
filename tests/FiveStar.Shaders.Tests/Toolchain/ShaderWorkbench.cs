// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using FiveStar.Shaders.Compilation;
using FiveStar.Shaders.Tests.Assembly;
using FiveStar.Shaders.Translation;

namespace FiveStar.Shaders.Tests.Toolchain;

/// <summary>
/// The build steps of a translator test in one work folder: llvm-mc assembly, translation, dxc (shader model 6.7,
/// HLSL 2021, the runtime headers next to the source) and the helper shaders embedded in this assembly.
/// </summary>
public sealed class ShaderWorkbench
{
    private static readonly TimeSpan CompileTimeout = TimeSpan.FromMinutes(2);

    private readonly RdnaAssembler _assembler;
    private readonly DxcCompiler _compiler;
    private readonly string _folder;

    public ShaderWorkbench(ShaderToolchain tools, string folder)
    {
        _folder = folder;
        _assembler = new RdnaAssembler(tools.Llvm, folder);
        _compiler = new DxcCompiler(DxcOptions.Defaults(tools.Dxc, retry: false));
        RuntimeIncludes.WriteTo(folder);
    }

    public AssembledProgram Assemble(string name, string source, int waveSize)
    {
        return _assembler.Assemble(name, source, waveSize);
    }

    public static string Translate(ShaderProgram program)
    {
        return HlslTranslator.Translate(program).Hlsl;
    }

    /// <summary>Compiles HLSL with the profile prefix (ps, vs, cs, ms, gs) to DXIL; dxc errors become an exception.</summary>
    public byte[] Compile(string name, string hlsl, string profile)
    {
        var path = Path.Combine(_folder, name + ".hlsl");
        File.WriteAllText(path, hlsl);
        var outcome = _compiler.Compile(new CompileRequest(path, hlsl, profile, DisableOptimizations: false), CompileTimeout);

        if (!outcome.Succeeded)
        {
            throw new InvalidOperationException($"dxc rejected {name} ({profile}): {DxcCompiler.FirstError(outcome.Error)}\n{path}");
        }

        return File.ReadAllBytes(Path.ChangeExtension(path, ".dxil"));
    }

    /// <summary>Compiles a helper shader embedded from the Shaders folder.</summary>
    public byte[] CompileHelper(string fileName, string profile)
    {
        using var stream = typeof(ShaderWorkbench).Assembly.GetManifestResourceStream(fileName)
            ?? throw new FileNotFoundException("helper shader not embedded", fileName);
        using var reader = new StreamReader(stream);

        return Compile(Path.GetFileNameWithoutExtension(fileName), reader.ReadToEnd(), profile);
    }
}
