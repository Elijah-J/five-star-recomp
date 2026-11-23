// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

namespace FiveStar.Graphics.Programs;

/// <summary>
/// Picks the form of each program an adapter runs (docs/shader-translation.md, "Wave size"). The driver chooses the
/// hardware wave of pixel, vertex, geometry and mesh shaders anywhere between WaveLaneCountMin and Max, so a floor is
/// met only when WaveLaneCountMin reaches it. A compute program pinned to a lane count the adapter lacks runs its
/// wave32 variant when it has one. A mesh program runs its fallback on an adapter without mesh shaders, and its superstep
/// form (the same .w32 file) where the adapter may run waves narrower than its floor. A fallback below its floor runs
/// pinned to the console wave where the adapter offers it, else as the fallback of the superstep form.
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
                return MeshChoice(adapter, program);
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

        if (pin is null || Offers(adapter, pin.Value))
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
        var fallback = program.MeshFallback;

        if (adapter.MeshShaders && program.Dxil is not null)
        {
            var native = FloorChecked(adapter, new ProgramChoice(program, ChoiceOutcome.Native, [program.Dxil], null));

            if (native.Outcome != ChoiceOutcome.Incorrect || fallback is null)
            {
                return native;
            }

            // No superstep form: a fallback that holds the console wave still does.
            return WideFallback(adapter, program, fallback, native.Reason!) ?? native;
        }

        var reason = "no mesh shaders";

        if (adapter.MeshShaders)
        {
            reason = "over the mesh output limit";
        }

        if (fallback is null)
        {
            return new ProgramChoice(program, ChoiceOutcome.Incorrect, NativeFiles(program), reason + ", no fallback");
        }

        var plain = new ProgramChoice(program, ChoiceOutcome.Fallback, [fallback.Compute, fallback.Vertex], reason);

        if (!BelowFloor(adapter, program))
        {
            return plain;
        }

        var narrow = $"{reason}; floor {program.Floor} above {adapter.WaveLaneCountMin}-lane waves";

        return WideFallback(adapter, program, fallback, narrow) ?? plain with { Outcome = ChoiceOutcome.Incorrect, Reason = narrow };
    }

    /// <summary>
    /// The fallback that gives console results below the program's floor: pinned to the console wave where the adapter
    /// offers it, else the fallback of the superstep form; null when neither exists.
    /// </summary>
    private static ProgramChoice? WideFallback(AdapterInfo adapter, ProgramVariants program, MeshFallbackFiles fallback, string reason)
    {
        if (fallback.PinnedCompute is not null && fallback.PinnedLanes is not null && Offers(adapter, fallback.PinnedLanes.Value))
        {
            return new ProgramChoice(program, ChoiceOutcome.Fallback, [fallback.PinnedCompute, fallback.Vertex], $"{reason}; pinned to {fallback.PinnedLanes} lanes");
        }

        if (fallback.Supersteps is not null)
        {
            return new ProgramChoice(program, ChoiceOutcome.Fallback, [fallback.Supersteps.Compute, fallback.Supersteps.Vertex], $"{reason}; supersteps");
        }

        return null;
    }

    /// <summary>The choice, or incorrect when the adapter may run the program on waves narrower than its floor.</summary>
    private static ProgramChoice FloorChecked(AdapterInfo adapter, ProgramChoice choice)
    {
        if (!BelowFloor(adapter, choice.Program))
        {
            return choice;
        }

        var reason = $"floor {choice.Program.Floor} above {adapter.WaveLaneCountMin}-lane waves";

        if (choice.Program.Stage == Mesh && choice.Program.GroupWave is not null)
        {
            // The superstep form gives console results on any wave.
            return choice with { Outcome = ChoiceOutcome.Variant, Files = [choice.Program.GroupWave], Reason = reason };
        }

        return choice with { Outcome = ChoiceOutcome.Incorrect, Reason = reason };
    }

    /// <summary>Whether the adapter may run the program on hardware waves narrower than its floor.</summary>
    private static bool BelowFloor(AdapterInfo adapter, ProgramVariants program)
    {
        return program.Floor is not null && program.Floor > adapter.WaveLaneCountMin;
    }

    /// <summary>Whether a compute shader pinned to <paramref name="lanes"/> loads on the adapter.</summary>
    private static bool Offers(AdapterInfo adapter, int lanes)
    {
        return lanes >= adapter.WaveLaneCountMin && lanes <= adapter.WaveLaneCountMax;
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
