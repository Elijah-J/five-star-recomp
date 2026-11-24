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
/// <para>
/// The code is structured, so a join closes a region (if, switch, loop, inlined call) that starts at one block. The
/// lowering records which slots each region writes (<see cref="BeginRegion"/>, <see cref="EndRegion"/>); a read of any
/// other slot at the join continues at the region's start instead of visiting every path through it.
/// </para>
/// </summary>
public sealed class SsaBuilder
{
    private readonly DxilModule _module;
    private readonly List<IrType> _slotTypes = [];
    private readonly List<BlockState?> _states = [];
    private readonly List<BasicBlock> _blocks = [];
    private readonly Stack<HashSet<int>> _regions = new();

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
        State(to).Predecessors.Add(from);
    }

    public int PredecessorCount(BasicBlock block)
    {
        return State(block).Predecessors.Count;
    }

    public void Write(int slot, BasicBlock block, Value value)
    {
        State(block).Definitions[slot] = value;

        if (_regions.Count > 0)
        {
            _regions.Peek().Add(slot);
        }
    }

    /// <summary>Starts recording the slots a region writes.</summary>
    public void BeginRegion()
    {
        _regions.Push([]);
    }

    /// <summary>
    /// Ends the innermost region: reads at <paramref name="join"/> of slots it did not write continue at
    /// <paramref name="start"/>, the block every path into the join came from.
    /// </summary>
    public void EndRegion(BasicBlock start, BasicBlock join)
    {
        var written = _regions.Pop();

        if (_regions.Count > 0)
        {
            _regions.Peek().UnionWith(written);
        }

        var state = State(join);
        state.RegionStart = start;
        state.RegionWrites = written;
    }

    public Value Read(int slot, BasicBlock block)
    {
        var current = block;
        Value value;

        while (true)
        {
            var state = State(current);

            if (state.Definitions.TryGetValue(slot, out var found))
            {
                value = Find(found);

                break;
            }

            var next = Skip(state, slot);

            if (next is null)
            {
                value = ReadAtJoin(slot, current);

                break;
            }

            current = next;
        }

        if (!ReferenceEquals(current, block))
        {
            State(block).Definitions[slot] = value;
        }

        return value;
    }

    public void Seal(BasicBlock block)
    {
        var state = State(block);

        if (state.Sealed)
        {
            return;
        }

        state.Sealed = true;

        if (state.Incomplete is null)
        {
            return;
        }

        foreach (var (slot, phi) in state.Incomplete)
        {
            state.Definitions[slot] = AddOperands(slot, phi);
        }

        state.Incomplete = null;
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

        foreach (var block in _blocks)
        {
            Rewrite(block.Phis);
            Rewrite(block.Body);

            if (block.Terminator is not null)
            {
                Rewrite(block.Terminator);
            }
        }
    }

    /// <summary>The block a read continues in without a phi: the single predecessor, or the start of a region that leaves the slot alone.</summary>
    private static BasicBlock? Skip(BlockState state, int slot)
    {
        if (!state.Sealed)
        {
            return null;
        }

        if (state.Predecessors.Count == 1)
        {
            return state.Predecessors[0];
        }

        if (state.RegionStart is not null && !state.RegionWrites!.Contains(slot))
        {
            return state.RegionStart;
        }

        return null;
    }

    private static void Rewrite(List<Instruction> instructions)
    {
        foreach (var instruction in instructions)
        {
            Rewrite(instruction);
        }
    }

    private static void Rewrite(Instruction instruction)
    {
        var operands = instruction.Operands;

        for (var index = 0; index < operands.Count; index++)
        {
            operands[index] = Find(operands[index]);
        }
    }

    private Value ReadAtJoin(int slot, BasicBlock block)
    {
        var type = _slotTypes[slot];
        var state = State(block);

        if (!state.Sealed)
        {
            var pending = IrBuilder.Phi(block, type);
            state.Incomplete ??= [];
            state.Incomplete.Add((slot, pending));
            state.Definitions[slot] = pending;

            return pending;
        }

        if (state.Predecessors.Count == 0)
        {
            var initial = Initial(type);
            state.Definitions[slot] = initial;

            return initial;
        }

        var phi = IrBuilder.Phi(block, type);
        state.Definitions[slot] = phi;
        var value = AddOperands(slot, phi);
        state.Definitions[slot] = value;

        return value;
    }

    /// <summary>Fills a phi from the predecessors; returns the phi, or the one value it would always hold.</summary>
    private Value AddOperands(int slot, Instruction phi)
    {
        foreach (var predecessor in State(phi.Block).Predecessors)
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

        same ??= Initial(phi.Type);
        phi.Replacement = same;
        var phis = phi.Block.Phis;

        // Usually the newest phi of its block.
        if (phis.Count > 0 && ReferenceEquals(phis[^1], phi))
        {
            phis.RemoveAt(phis.Count - 1);
        }
        else
        {
            phis.Remove(phi);
        }

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

    /// <summary>
    /// The value of a slot read before any write: zero (the validator refuses instructions that read undef), or undef
    /// for a handle.
    /// </summary>
    private Value Initial(IrType type)
    {
        if (type.IsInteger)
        {
            return _module.Int(type, 0);
        }

        if (type.IsFloatingPoint)
        {
            return _module.FloatBits(type, 0);
        }

        return _module.Undefined(type);
    }

    private static Value Find(Value value)
    {
        var current = value;

        while (current is Instruction instruction && instruction.Replacement is not null)
        {
            current = instruction.Replacement;
        }

        // Shorten the chain for the next lookup.
        if (value is Instruction first && first.Replacement is not null)
        {
            first.Replacement = current;
        }

        return current;
    }

    private BlockState State(BasicBlock block)
    {
        while (_states.Count <= block.Number)
        {
            _states.Add(null);
        }

        var state = _states[block.Number];

        if (state is null)
        {
            state = new BlockState();
            _states[block.Number] = state;
        }

        return state;
    }

    /// <summary>Per block: definitions by slot, predecessors, sealed, phis waiting for the seal, the region it closes.</summary>
    private sealed class BlockState
    {
        public Dictionary<int, Value> Definitions { get; } = [];

        public List<BasicBlock> Predecessors { get; } = [];

        public bool Sealed { get; set; }

        public List<(int Slot, Instruction Phi)>? Incomplete { get; set; }

        public BasicBlock? RegionStart { get; set; }

        public HashSet<int>? RegionWrites { get; set; }
    }
}
