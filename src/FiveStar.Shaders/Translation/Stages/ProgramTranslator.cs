// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using FiveStar.Shaders.Disassembly;
using FiveStar.Shaders.Translation.Core;

namespace FiveStar.Shaders.Translation.Stages;

/// <summary>Translates one instruction stream of a program into a complete HLSL program.</summary>
internal static class ProgramTranslator
{
    /// <summary>Translates <paramref name="instructions"/> as <paramref name="stage"/>; a copy shader is emitted as a function.</summary>
    public static string Translate(ShaderProgram program, IReadOnlyList<Instruction> instructions, ShaderStage stage, bool copyShader)
    {
        var state = new TranslationState(program, instructions, stage, copyShader);
        var operands = new OperandAccess(state, new ImplicitInputs(state));
        var translator = new InstructionTranslator(state, operands, InstructionEmitters.Create(state, operands));
        new ControlFlowTranslator(state, operands, translator).Run();

        var mesh = new MeshDeclarations(state);
        var emitter = new ProgramEmitter(state, new OutputWriter(state), mesh, new BodyWriter(state, mesh));

        return emitter.Emit();
    }
}
