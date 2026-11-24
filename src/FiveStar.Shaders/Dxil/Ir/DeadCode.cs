// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

namespace FiveStar.Shaders.Dxil.Ir;

/// <summary>
/// Removes blocks the entry cannot reach, the phi operands that came from them, and instructions without effect whose
/// results nothing reads, so the feature flags and the written module see only the code that runs.
/// </summary>
public static class DeadCode
{
    public static void Remove(IrFunction function)
    {
        var reachable = Reachable(function.Blocks[0]);
        function.Blocks.RemoveAll(block => !reachable.Contains(block));

        foreach (var phi in function.Blocks.SelectMany(block => block.Phis))
        {
            for (var index = phi.Operands.Count - 1; index >= 0; index--)
            {
                if (!reachable.Contains(phi.Targets[index]))
                {
                    phi.Operands.RemoveAt(index);
                    phi.Targets.RemoveAt(index);
                }
            }
        }

        var live = Live(function);

        foreach (var block in function.Blocks)
        {
            block.Phis.RemoveAll(phi => !live.Contains(phi));
            block.Body.RemoveAll(instruction => !live.Contains(instruction));
        }
    }

    private static HashSet<BasicBlock> Reachable(BasicBlock entry)
    {
        var reachable = new HashSet<BasicBlock> { entry };
        var work = new Stack<BasicBlock>();
        work.Push(entry);

        while (work.Count > 0)
        {
            foreach (var successor in work.Pop().Successors)
            {
                if (reachable.Add(successor))
                {
                    work.Push(successor);
                }
            }
        }

        return reachable;
    }

    private static HashSet<Instruction> Live(IrFunction function)
    {
        var live = new HashSet<Instruction>();
        var work = new Stack<Instruction>();

        foreach (var block in function.Blocks)
        {
            foreach (var instruction in block.Body.Where(instruction => instruction.HasEffect).Append(block.Terminator!))
            {
                if (live.Add(instruction))
                {
                    work.Push(instruction);
                }
            }
        }

        while (work.Count > 0)
        {
            foreach (var operand in work.Pop().Operands)
            {
                if (operand is Instruction instruction && live.Add(instruction))
                {
                    work.Push(instruction);
                }
            }
        }

        return live;
    }
}
