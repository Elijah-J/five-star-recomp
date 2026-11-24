// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using FiveStar.Shaders.Dxil.Ir;

namespace FiveStar.Shaders.Dxil.Lowering;

/// <summary>
/// SSA construction while the code is built (Braun et al., "Simple and Efficient Construction of Static Single
/// Assignment Form"): every variable component is a slot; a read looks for the slot's definition in the block, then
/// through its predecessors, placing a phi where paths meet. A block is sealed once all its predecessors are known; a
/// phi whose operands are one value (or itself) is replaced by that value, and <see cref="Finish"/> rewrites every
/// operand through the replacements.
/// </summary>
public sealed class SsaBuilder
{
    private readonly DxilModule _module;
    private readonly List<IrType> _slotTypes = [];
    private readonly Dictionary<BasicBlock, Dictionary<int, Value>> _definitions = [];
    private readonly Dictionary<BasicBlock, List<BasicBlock>> _predecessors = [];
    private readonly HashSet<BasicBlock> _sealed = [];
    private readonly Dictionary<BasicBlock, List<(int Slot, Instruction Phi)>> _incomplete = [];
    private readonly Dictionary<Value, Value> _replacements = [];
    private readonly List<BasicBlock> _blocks = [];

    public SsaBuilder(DxilModule module)
    {
        _module = module;
    }

    /// <summary>Every block made through <see cref="NewBlock"/>, in creation order.</summary>
    public IReadOnlyList<BasicBlock> Blocks => _blocks;

    public BasicBlock NewBlock()
    {
        var block = _module.NewBlock();
        _blocks.Add(block);

        return block;
    }

    public int NewSlot(IrType type)
    {
        _slotTypes.Add(type);

        return _slotTypes.Count - 1;
    }

    public IrType SlotType(int slot)
    {
        return _slotTypes[slot];
    }

    /// <summary>Records a control-flow edge (call when the branch is made).</summary>
    public void AddEdge(BasicBlock from, BasicBlock to)
    {
        Predecessors(to).Add(from);
    }

    public int PredecessorCount(BasicBlock block)
    {
        return Predecessors(block).Count;
    }

    public void Write(int slot, BasicBlock block, Value value)
    {
        Definitions(block)[slot] = value;
    }

    public Value Read(int slot, BasicBlock block)
    {
        var current = block;

        // Walk single-predecessor chains without recursion.
        while (true)
        {
            if (Definitions(current).TryGetValue(slot, out var value))
            {
                return Find(value);
            }

            var predecessors = Predecessors(current);

            if (!_sealed.Contains(current) || predecessors.Count != 1)
            {
                break;
            }

            current = predecessors[0];
        }

        var result = ReadAtJoin(slot, current);

        if (!ReferenceEquals(current, block))
        {
            Definitions(block)[slot] = result;
        }

        return result;
    }

    public void Seal(BasicBlock block)
    {
        if (!_sealed.Add(block))
        {
            return;
        }

        if (_incomplete.Remove(block, out var phis))
        {
            foreach (var (slot, phi) in phis)
            {
                var value = AddOperands(slot, phi);
                Definitions(block)[slot] = value;
            }
        }
    }

    /// <summary>Removes the remaining trivial phis and rewrites every operand through the replacements.</summary>
    public void Finish()
    {
        var changed = true;

        while (changed)
        {
            changed = false;

            foreach (var block in _blocks)
            {
                changed |= RemoveTrivial(block);
            }
        }

        foreach (var instruction in _blocks.SelectMany(block => block.Phis.Concat(block.Body)))
        {
            for (var index = 0; index < instruction.Operands.Count; index++)
            {
                instruction.Operands[index] = Find(instruction.Operands[index]);
            }
        }

        foreach (var block in _blocks.Where(block => block.Terminator is not null))
        {
            var operands = block.Terminator!.Operands;

            for (var index = 0; index < operands.Count; index++)
            {
                operands[index] = Find(operands[index]);
            }
        }
    }

    private Value ReadAtJoin(int slot, BasicBlock block)
    {
        var type = _slotTypes[slot];

        if (!_sealed.Contains(block))
        {
            var pending = IrBuilder.Phi(block, type);
            Incomplete(block).Add((slot, pending));
            Definitions(block)[slot] = pending;

            return pending;
        }

        var predecessors = Predecessors(block);

        if (predecessors.Count == 0)
        {
            var undefined = _module.Undefined(type);
            Definitions(block)[slot] = undefined;

            return undefined;
        }

        var phi = IrBuilder.Phi(block, type);
        Definitions(block)[slot] = phi;
        var value = AddOperands(slot, phi);
        Definitions(block)[slot] = value;

        return value;
    }

    /// <summary>Fills a phi from the predecessors; returns the phi, or the one value it would always hold.</summary>
    private Value AddOperands(int slot, Instruction phi)
    {
        foreach (var predecessor in Predecessors(phi.Block))
        {
            phi.Operands.Add(Read(slot, predecessor));
            phi.Targets.Add(predecessor);
        }

        return TryRemove(phi);
    }

    private Value TryRemove(Instruction phi)
    {
        Value? same = null;

        foreach (var operand in phi.Operands)
        {
            var resolved = Find(operand);

            if (ReferenceEquals(resolved, phi) || ReferenceEquals(resolved, same))
            {
                continue;
            }

            if (same is not null)
            {
                return phi;
            }

            same = resolved;
        }

        same ??= _module.Undefined(phi.Type);
        _replacements[phi] = same;
        phi.Block.Phis.Remove(phi);

        return same;
    }

    private bool RemoveTrivial(BasicBlock block)
    {
        var changed = false;

        foreach (var phi in block.Phis.ToArray())
        {
            if (!ReferenceEquals(TryRemove(phi), phi))
            {
                changed = true;
            }
        }

        return changed;
    }

    private Value Find(Value value)
    {
        var current = value;

        while (_replacements.TryGetValue(current, out var next))
        {
            current = next;
        }

        // Shorten the chain for the next lookup.
        if (!ReferenceEquals(current, value))
        {
            _replacements[value] = current;
        }

        return current;
    }

    private Dictionary<int, Value> Definitions(BasicBlock block)
    {
        if (!_definitions.TryGetValue(block, out var definitions))
        {
            definitions = [];
            _definitions[block] = definitions;
        }

        return definitions;
    }

    private List<BasicBlock> Predecessors(BasicBlock block)
    {
        if (!_predecessors.TryGetValue(block, out var predecessors))
        {
            predecessors = [];
            _predecessors[block] = predecessors;
        }

        return predecessors;
    }

    private List<(int Slot, Instruction Phi)> Incomplete(BasicBlock block)
    {
        if (!_incomplete.TryGetValue(block, out var phis))
        {
            phis = [];
            _incomplete[block] = phis;
        }

        return phis;
    }
}
