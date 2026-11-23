// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

namespace FiveStar.Graphics.Programs;

/// <summary>
/// Picks the form of each program an adapter runs (docs/shader-translation.md, "Wave size"). The driver chooses the
/// hardware wave of pixel, vertex, geometry and mesh shaders anywhere between WaveLaneCountMin and Max, so a floor is
/// met only when WaveLaneCountMin reaches it. A compute program pinned to a lane count the adapter lacks runs its
/// wave32 variant when it has one. A mesh program runs its fallback on an adapter without mesh shaders, and its superstep
/// form (the same .w32 file) where the adapter may run waves narrower than its floor.
/// </summary>
public static class VariantSelector
{
    private const string Compute = "cs";
    private const string Mesh = "ms";

    // Every translated program is compiled for shader model 6.7.
    private static readonly Version RequiredShaderModel = new(6, 7);

    public static ProgramChoice Choose(AdapterInfo adapter, ProgramVariants program)
    {
        if (!Version.TryParse(adapter.ShaderModel, out var model) || model < RequiredShaderModel)
        {
            return new ProgramChoice(program, ChoiceOutcome.Incorrect, NativeFiles(program), "no shader model 6.7");
        }

        switch (program.Stage)
        {
            case Compute:
                return ComputeChoice(adapter, program);
            case Mesh:
                return FloorChecked(adapter, MeshChoice(adapter, program));
            default:
                return FloorChecked(adapter, new ProgramChoice(program, ChoiceOutcome.Native, NativeFiles(program), null));
        }
    }

    /// <summary>The choice of every program, in the given order.</summary>
    public static IReadOnlyList<ProgramChoice> Plan(AdapterInfo adapter, IEnumerable<ProgramVariants> programs)
    {
        return programs.Select(program => Choose(adapter, program)).ToList();
    }

    private static ProgramChoice ComputeChoice(AdapterInfo adapter, ProgramVariants program)
    {
        var pin = program.WavePin;

        if (pin is null || (pin >= adapter.WaveLaneCountMin && pin <= adapter.WaveLaneCountMax))
        {
            return new ProgramChoice(program, ChoiceOutcome.Native, NativeFiles(program), null);
        }

        if (program.GroupWave is not null)
        {
            return new ProgramChoice(program, ChoiceOutcome.Variant, [program.GroupWave], $"no {pin}-lane waves");
        }

        return new ProgramChoice(program, ChoiceOutcome.Incorrect, NativeFiles(program), $"pinned to {pin} lanes, no variant");
    }

    private static ProgramChoice MeshChoice(AdapterInfo adapter, ProgramVariants program)
    {
        if (adapter.MeshShaders && program.Dxil is not null)
        {
            return new ProgramChoice(program, ChoiceOutcome.Native, [program.Dxil], null);
        }

        var reason = "no mesh shaders";

        if (adapter.MeshShaders)
        {
            reason = "over the mesh output limit";
        }

        if (program.MeshFallback is null)
        {
            return new ProgramChoice(program, ChoiceOutcome.Incorrect, NativeFiles(program), reason + ", no fallback");
        }

        return new ProgramChoice(program, ChoiceOutcome.Fallback, [program.MeshFallback.Compute, program.MeshFallback.Vertex], reason);
    }

    /// <summary>The choice, or incorrect when the adapter may run the program on waves narrower than its floor.</summary>
    private static ProgramChoice FloorChecked(AdapterInfo adapter, ProgramChoice choice)
    {
        var floor = choice.Program.Floor;

        if (choice.Outcome == ChoiceOutcome.Incorrect || floor is null || floor <= adapter.WaveLaneCountMin)
        {
            return choice;
        }

        var reason = $"floor {floor} above {adapter.WaveLaneCountMin}-lane waves";

        if (choice.Outcome == ChoiceOutcome.Native && choice.Program.Stage == Mesh && choice.Program.GroupWave is not null)
        {
            // The superstep form gives console results on any wave.
            return choice with { Outcome = ChoiceOutcome.Variant, Files = [choice.Program.GroupWave], Reason = reason };
        }

        if (choice.Reason is not null)
        {
            // Keep why the program runs another form (the mesh fallback) in front.
            reason = $"{choice.Reason}; {reason}";
        }

        return choice with { Outcome = ChoiceOutcome.Incorrect, Reason = reason };
    }

    private static IReadOnlyList<string> NativeFiles(ProgramVariants program)
    {
        if (program.Dxil is null)
        {
            return [];
        }

        return [program.Dxil];
    }
}
