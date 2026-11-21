// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using FiveStar.Shaders.Translation.Stages;

namespace FiveStar.Shaders.Translation;

/// <summary>
/// Translates a native RDNA program into HLSL (shader model 6.7). One HLSL invocation is one GPU lane: registers are
/// uint variables, EXEC is a per-lane bool, control flow is a wave-uniform dispatch over basic blocks and resources are
/// bindless heap indices. docs/shader-translation.md describes the model.
/// </summary>
public static class HlslTranslator
{
    /// <summary>Translates a program; throws <see cref="UnsupportedProgramException"/> for what it cannot handle.</summary>
    public static TranslationResult Translate(ShaderProgram program)
    {
        string hlsl;

        if (program.Stage == ShaderStage.Geometry)
        {
            hlsl = GeometryProgramBuilder.Build(program);
        }
        else
        {
            hlsl = ProgramTranslator.Translate(program, program.Instructions, program.Stage, copyShader: false);
        }

        return new TranslationResult(program.Stage, hlsl);
    }
}
