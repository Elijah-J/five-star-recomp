// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using FiveStar.Shaders.Dxil.Container;
using FiveStar.Shaders.Dxil.Ir;
using FiveStar.Shaders.Dxil.Ops;

namespace FiveStar.Shaders.Dxil.Lowering;

/// <summary>
/// The shader flags a module's instructions need, as the validator recomputes them: 64-bit integers, doubles, and the
/// double extensions (double division, fma, and conversions between doubles and integers).
/// </summary>
public static class FeatureScan
{
    public static ShaderFlagBits Flags(IrFunction main)
    {
        var flags = ShaderFlagBits.None;

        foreach (var instruction in main.Blocks.SelectMany(block => block.Phis.Concat(block.Body)))
        {
            flags |= Of(instruction);
        }

        return flags;
    }

    private static ShaderFlagBits Of(Instruction instruction)
    {
        var flags = ShaderFlagBits.None;
        var types = instruction.Operands.Select(operand => operand.Type).Append(instruction.Type);

        foreach (var type in types)
        {
            if (type.Kind == IrTypeKind.Integer && type.Bits == sizeof(long) * BitsPerByte)
            {
                flags |= ShaderFlagBits.Int64Ops;
            }

            if (type.Kind == IrTypeKind.Double)
            {
                flags |= ShaderFlagBits.EnableDoublePrecision;
            }
        }

        if (flags.HasFlag(ShaderFlagBits.EnableDoublePrecision) && NeedsExtensions(instruction))
        {
            flags |= ShaderFlagBits.EnableDoubleExtensions;
        }

        return flags;
    }

    private static bool NeedsExtensions(Instruction instruction)
    {
        if (instruction.Opcode == Opcode.Binary)
        {
            return instruction.Code is (int)BinaryOp.SDiv or (int)BinaryOp.SRem && instruction.Type.Kind == IrTypeKind.Double;
        }

        if (instruction.Opcode == Opcode.Cast)
        {
            return instruction.Code is (int)CastOp.FpToSi or (int)CastOp.FpToUi or (int)CastOp.SiToFp or (int)CastOp.UiToFp;
        }

        return instruction.Opcode == Opcode.Call && instruction.Operands.Count > 0
            && instruction.Operands[0] is Constant code && code.Bits == (long)DxOpCode.Fma;
    }

    private const int BitsPerByte = 8;
}
