// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using FiveStar.Shaders.Dxil.Bitcode;

namespace FiveStar.Shaders.Dxil.Ir;

/// <summary>
/// Writes the entry function's block: blocks in reverse postorder from the entry, live instructions only, operands as
/// relative value numbers (a phi's signed, since it may read a later value).
/// </summary>
internal sealed class FunctionBodyWriter
{
    private const int Width = 4;

    private readonly BitstreamWriter _out;
    private readonly IrFunction _function;
    private readonly TypeTable _types;
    private readonly List<Instruction> _live = [];
    private List<BasicBlock> _layout = [];
    private int _base;
    private int _next;

    public FunctionBodyWriter(BitstreamWriter output, IrFunction function, TypeTable types)
    {
        _out = output;
        _function = function;
        _types = types;
    }

    /// <summary>Lays the blocks out and finds the live instructions.</summary>
    public void Analyze()
    {
        _layout = ReversePostorder(_function.Blocks[0]);

        for (var index = 0; index < _layout.Count; index++)
        {
            _layout[index].Position = index;
        }

        MarkLive();
    }

    /// <summary>The functions the live instructions call and the globals they address.</summary>
    public HashSet<Value> Referenced()
    {
        var referenced = new HashSet<Value>();

        foreach (var instruction in _live)
        {
            if (instruction.Callee is not null)
            {
                referenced.Add(instruction.Callee);
            }

            referenced.UnionWith(instruction.Operands.OfType<GlobalVariable>());
        }

        return referenced;
    }

    /// <summary>Numbers the live results after the <paramref name="moduleValues"/> module-level values.</summary>
    public void NumberInstructions(int moduleValues)
    {
        _base = moduleValues;
        var next = _base;

        foreach (var block in _layout)
        {
            next = Number(block.Phis, next);
            next = Number(block.Body, next);
        }
    }

    private static int Number(List<Instruction> instructions, int next)
    {
        foreach (var instruction in instructions)
        {
            if (instruction.Live && !instruction.Type.IsVoid)
            {
                instruction.Id = next;
                next++;
            }
        }

        return next;
    }

    public void Write()
    {
        _out.Enter(BlockId.Function, Width);
        _out.Record((int)FunctionCode.DeclareBlocks, [(ulong)_layout.Count]);
        _next = _base;

        foreach (var block in _layout)
        {
            foreach (var instruction in Instructions(block))
            {
                WriteInstruction(instruction);

                if (!instruction.Type.IsVoid)
                {
                    _next++;
                }
            }

            WriteInstruction(block.Terminator!);
        }

        _out.Exit();
    }

    /// <summary>The live phis and body instructions of a block, in order.</summary>
    private static IEnumerable<Instruction> Instructions(BasicBlock block)
    {
        return block.Phis.Concat(block.Body).Where(instruction => instruction.Live);
    }

    private static List<BasicBlock> ReversePostorder(BasicBlock entry)
    {
        var seen = new HashSet<BasicBlock> { entry };
        var post = new List<BasicBlock>();
        var stack = new Stack<(BasicBlock Block, int Next)>();
        stack.Push((entry, 0));

        while (stack.Count > 0)
        {
            var (block, next) = stack.Pop();
            var successors = block.Terminator!.Targets;

            if (next < successors.Count)
            {
                stack.Push((block, next + 1));

                if (seen.Add(successors[next]))
                {
                    stack.Push((successors[next], 0));
                }

                continue;
            }

            post.Add(block);
        }

        post.Reverse();

        return post;
    }

    private void MarkLive()
    {
        var work = new Stack<Instruction>();

        foreach (var block in _layout)
        {
            foreach (var instruction in block.Body.Where(instruction => instruction.HasEffect))
            {
                Mark(instruction, work);
            }

            foreach (var operand in block.Terminator!.Operands)
            {
                Mark(operand, work);
            }
        }

        while (work.Count > 0)
        {
            foreach (var operand in work.Pop().Operands)
            {
                Mark(operand, work);
            }
        }
    }

    private void Mark(Value value, Stack<Instruction> work)
    {
        if (value is Instruction instruction && !instruction.Live)
        {
            instruction.Live = true;
            _live.Add(instruction);
            work.Push(instruction);
        }
    }

    private ulong Relative(Value value)
    {
        var id = value.Id;

        if (id < 0)
        {
            throw new InvalidOperationException($"an operand that is not written: {value}");
        }

        if (id >= _next)
        {
            throw new InvalidOperationException($"forward reference outside a phi: {value}");
        }

        return (ulong)(_next - id);
    }

    private void WriteInstruction(Instruction instruction)
    {
        var operands = instruction.Operands;

        switch (instruction.Opcode)
        {
            case Opcode.Binary:
                _out.Record((int)FunctionCode.Binary, [Relative(operands[0]), Relative(operands[1]), (ulong)instruction.Code]);
                break;
            case Opcode.Cast:
                _out.Record((int)FunctionCode.Cast, [Relative(operands[0]), (ulong)instruction.Type.Id, (ulong)instruction.Code]);
                break;
            case Opcode.Compare:
                _out.Record((int)FunctionCode.Compare, [Relative(operands[0]), Relative(operands[1]), (ulong)instruction.Code]);
                break;
            case Opcode.Select:
                _out.Record((int)FunctionCode.Select, [Relative(operands[1]), Relative(operands[2]), Relative(operands[0])]);
                break;
            case Opcode.ExtractValue:
                _out.Record((int)FunctionCode.ExtractValue, [Relative(operands[0]), (ulong)instruction.Code]);
                break;
            default:
                WriteMemoryOrControl(instruction);
                break;
        }
    }

    private void WriteMemoryOrControl(Instruction instruction)
    {
        var operands = instruction.Operands;

        switch (instruction.Opcode)
        {
            case Opcode.Load:
                _out.Record((int)FunctionCode.Load, [Relative(operands[0]), (ulong)instruction.Type.Id, ModuleWriter.Records.Alignment, 0]);
                break;
            case Opcode.Store:
                _out.Record((int)FunctionCode.Store, [Relative(operands[0]), Relative(operands[1]), ModuleWriter.Records.Alignment, 0]);
                break;
            case Opcode.AtomicRmw:
                _out.Record((int)FunctionCode.AtomicRmw, [Relative(operands[0]), Relative(operands[1]), (ulong)instruction.Code, 0, ModuleWriter.Records.Ordering, ModuleWriter.Records.Scope]);
                break;
            case Opcode.CompareExchange:
                _out.Record((int)FunctionCode.CompareExchange, [Relative(operands[0]), Relative(operands[1]), Relative(operands[2]), 0, ModuleWriter.Records.Ordering, ModuleWriter.Records.Scope, ModuleWriter.Records.Ordering, 0]);
                break;
            case Opcode.GetElementPtr:
                _out.Record((int)FunctionCode.GetElementPtr, [1, (ulong)instruction.SourceType!.Id, .. operands.Select(Relative)]);
                break;
            case Opcode.Alloca:
                _out.Record((int)FunctionCode.Alloca, [(ulong)instruction.SourceType!.Id, (ulong)_types.Int32.Id, (ulong)operands[0].Id, ModuleWriter.Records.Alignment | ModuleWriter.Records.AllocaFlags]);
                break;
            case Opcode.Call:
                WriteCall(instruction);
                break;
            case Opcode.Phi:
                WritePhi(instruction);
                break;
            default:
                WriteTerminator(instruction);
                break;
        }
    }

    private void WriteCall(Instruction call)
    {
        var callee = call.Callee!;
        _out.Record((int)FunctionCode.Call, [0, ModuleWriter.Records.ExplicitType, (ulong)callee.FunctionType.Id, Relative(callee), .. call.Operands.Select(Relative)]);
    }

    private void WritePhi(Instruction phi)
    {
        var record = new List<ulong> { (ulong)phi.Type.Id };

        for (var index = 0; index < phi.Operands.Count; index++)
        {
            var position = phi.Targets[index].Position;

            if (position < 0)
            {
                continue;
            }

            if (phi.Operands[index].Id < 0)
            {
                throw new InvalidOperationException($"a phi operand that is not written: {phi.Operands[index]}");
            }

            record.Add(BitstreamWriter.Signed((long)_next - phi.Operands[index].Id));
            record.Add((ulong)position);
        }

        _out.Record((int)FunctionCode.Phi, [.. record]);
    }

    private void WriteTerminator(Instruction terminator)
    {
        var targets = terminator.Targets.Select(target => (ulong)target.Position).ToArray();

        switch (terminator.Opcode)
        {
            case Opcode.Return:
                _out.Record((int)FunctionCode.Return, []);
                break;
            case Opcode.Branch:
                _out.Record((int)FunctionCode.Branch, [targets[0]]);
                break;
            case Opcode.ConditionalBranch:
                _out.Record((int)FunctionCode.Branch, [targets[0], targets[1], Relative(terminator.Operands[0])]);
                break;
            default:
                WriteSwitch(terminator, targets);
                break;
        }
    }

    private void WriteSwitch(Instruction terminator, ulong[] targets)
    {
        var condition = terminator.Operands[0];
        var record = new List<ulong> { (ulong)condition.Type.Id, Relative(condition), targets[0] };

        for (var index = 0; index < terminator.Cases.Count; index++)
        {
            record.Add((ulong)terminator.Cases[index].Id);
            record.Add(targets[index + 1]);
        }

        _out.Record((int)FunctionCode.Switch, [.. record]);
    }

    private enum FunctionCode
    {
        DeclareBlocks = 1,
        Binary = 2,
        Cast = 3,
        Return = 10,
        Branch = 11,
        Switch = 12,
        Phi = 16,
        Alloca = 19,
        Load = 20,
        ExtractValue = 26,
        Compare = 28,
        Select = 29,
        Call = 34,
        AtomicRmw = 38,
        GetElementPtr = 43,
        Store = 44,
        CompareExchange = 46,
    }
}
