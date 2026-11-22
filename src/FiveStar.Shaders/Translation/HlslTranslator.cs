// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using FiveStar.Shaders.Analysis;
using FiveStar.Shaders.Translation.Stages;

namespace FiveStar.Shaders.Translation;

/// <summary>
/// Translates a native RDNA program into HLSL (shader model 6.7). One HLSL invocation is one GPU lane: registers are
/// uint variables, EXEC is a per-lane bool, control flow is a wave-uniform dispatch over basic blocks (or, as an option,
/// structured if/else and loops) and resources are bindless heap indices. docs/shader-translation.md describes the model.
/// </summary>
public static class HlslTranslator
{
    /// <summary>
    /// Translates a program in the dispatch form without wave-size pins; throws <see cref="UnsupportedProgramException"/>
    /// for what it cannot handle.
    /// </summary>
    public static TranslationResult Translate(ShaderProgram program)
    {
        return Translate(program, new TranslationOptions(StructuredControlFlow: false, PinWaveSize: false));
    }

    /// <summary>Translates a program; throws <see cref="UnsupportedProgramException"/> for what it cannot handle.</summary>
    public static TranslationResult Translate(ShaderProgram program, TranslationOptions options)
    {
        string hlsl;

        if (program.Stage == ShaderStage.Geometry)
        {
            hlsl = GeometryProgramBuilder.Build(program, options);
        }
        else
        {
            hlsl = ProgramTranslator.Translate(program, program.Instructions, program.Stage, copyShader: false, options);
        }

        if (options.PinWaveSize && program.Stage == ShaderStage.Compute)
        {
            hlsl = PinWaveSize(hlsl, program.Config.WaveSize);
        }

        return new TranslationResult(program.Stage, hlsl);
    }

    /// <summary>Adds [WaveSize(n)] (compute only in SM 6.7) when the program reads other lanes by lane number.</summary>
    private static string PinWaveSize(string hlsl, int? waveSize)
    {
        const string EntryAttribute = "[numthreads(";

        var entry = hlsl.IndexOf(EntryAttribute, StringComparison.Ordinal);

        if (waveSize is null || entry < 0 || WaveUsageAnalyzer.Dependence(hlsl) != WaveDependence.LaneIndexed)
        {
            return hlsl;
        }

        return hlsl.Insert(entry, $"[WaveSize({waveSize.Value})]\n");
    }
}
