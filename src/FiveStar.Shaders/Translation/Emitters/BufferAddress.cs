// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using FiveStar.Shaders.Translation.Core;
using static FiveStar.Shaders.Translation.Emitters.Operands;

namespace FiveStar.Shaders.Translation.Emitters;

/// <summary>
/// The view and byte address of a MUBUF/MTBUF access. The view comes from the V#'s base (prelude <c>buf_view</c>): a
/// heap index while base_hi is 0, else a console GPU address in game memory. The address is soffset + instruction
/// offset, plus index * stride (idxen, stride in V# dword1 bits 16-29) and/or the VGPR offset (offen); with both, vaddr
/// holds the index then the offset.
/// </summary>
internal static class BufferAddress
{
    private const int BaseDword = 0;
    private const int StrideDword = 1;

    /// <summary>A uint2 temporary holding the V#'s heap index (x) and the byte offset of its base in that view (y).</summary>
    public static string View(OperandAccess operands, Operand resource, bool writable)
    {
        var rw = writable ? "true" : "false";

        return operands.SnapPair($"buf_view({operands.Read(resource, BaseDword)}, {operands.Read(resource, StrideDword)}, {rw})");
    }

    public static string Build(OperandAccess operands, Operand vaddr, Operand resource, Operand soffset, InstructionModifiers modifiers)
    {
        var address = $"({operands.Read(soffset)} + {modifiers.Integer("offset", 0)}u";
        var indexed = modifiers.Has("idxen");
        var offset = modifiers.Has("offen");

        if (indexed && offset)
        {
            var index = operands.Read(vaddr, Low);
            address += $" + {index} * {Stride(operands, resource)} + {operands.Read(vaddr, High)}";
        }
        else if (indexed)
        {
            var index = operands.Read(vaddr);
            address += $" + {index} * {Stride(operands, resource)}";
        }
        else if (offset)
        {
            address += $" + {operands.Read(vaddr)}";
        }

        return address + ")";
    }

    private static string Stride(OperandAccess operands, Operand resource)
    {
        return $"buf_stride({operands.Read(resource, StrideDword)})";
    }
}
