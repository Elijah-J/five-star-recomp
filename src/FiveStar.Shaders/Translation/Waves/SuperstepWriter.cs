// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using FiveStar.Shaders.Translation.Core;
using FiveStar.Shaders.Translation.Stages;

namespace FiveStar.Shaders.Translation.Waves;

/// <summary>
/// The body of a mesh program in the superstep form: the blocks of <see cref="SuperstepSplitter"/> in a dispatch loop that
/// runs each thread to its next stop, then one group step (<c>vw_ss_step</c>) per round, until every thread has ended. A
/// two-phase program leaves the first phase (outputs dropped) at the first s_barrier released after GS_ALLOC_REQ, then
/// calls SetMeshOutputCounts; every barrier stays outside the dispatch switch.
/// </summary>
internal static class SuperstepWriter
{
    private const string Indent = "    ";

    private static readonly string[] Step = ["    if (!vw_ss_step(pc, pc_resume)) break;"];

    public static List<string> Body(TranslationState state, IReadOnlyList<string> hoisted, MeshDeclarations mesh)
    {
        var lines = new List<string> { "  vw_ss_begin(in_gi);" };
        lines.AddRange(hoisted);
        lines.Add("  uint pc = 0, pc_resume = 0;");

        if (!state.MeshYield)
        {
            lines.AddRange(Round(state, false, Step));

            return lines;
        }

        lines.AddRange(Round(state, true, [.. Step, "    if (vw_ss_released() && g_ms_alloc != 0) break;"]));
        lines.Add($"  SetMeshOutputCounts(min(g_ms_nv, {mesh.MaxVertices}u), min(g_ms_np, {mesh.MaxPrimitives}u));");
        lines.AddRange(Round(state, false, Step));

        return lines;
    }

    private static List<string> Round(TranslationState state, bool dropOutputs, IEnumerable<string> end)
    {
        // Every thread enters the dispatch loop (a stopped one through the idle case, which gives its pc back), so all
        // reach the step's barrier on the same path: otherwise WARP waits at the barrier for threads that skipped the
        // loop and are at another copy of it.
        var lines = new List<string>
        {
            "  [loop] for (;;) {", "  uint pc_stopped = pc;", "  if (pc >= PC_WAVE) { pc = PC_IDLE; }", "  [loop] while (pc < PC_WAVE) {", "    switch (pc) {",
            "    case PC_IDLE: { pc = pc_stopped; } break;",
        };

        foreach (var block in state.Blocks)
        {
            lines.Add($"    case {Hlsl.Hex(block.Address)}: {{");
            lines.AddRange(block.Lines.Where(line => !dropOutputs || !MeshDeclarations.WritesOutput(line)).Select(line => Indent + line));
            lines.Add("    } break;");
        }

        lines.AddRange(["    default: pc = PC_END; break;", "    }", "  }"]);
        lines.AddRange(end);
        lines.Add("  }");

        return lines;
    }
}
