// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using FiveStar.Shaders.Translation.Core;
using static FiveStar.Shaders.Translation.Emitters.Operands;

namespace FiveStar.Shaders.Translation.Emitters;

/// <summary>
/// The byte address of a MUBUF/MTBUF access: soffset + instruction offset, plus index * stride (idxen, stride in
/// V# dword1) and/or the VGPR offset (offen). With both, vaddr holds the index then the offset.
/// </summary>
internal static class BufferAddress
{
    private const int StrideDword = 1;

    public static string Build(OperandAccess operands, Operand vaddr, Operand resource, Operand soffset, InstructionModifiers modifiers)
    {
        var address = $"({operands.Read(soffset)} + {modifiers.Integer("offset", 0)}u";
        var indexed = modifiers.Has("idxen");
        var offset = modifiers.Has("offen");

        if (indexed && offset)
        {
            var index = operands.Read(vaddr, Low);
            var stride = operands.Read(resource, StrideDword);
            address += $" + {index} * {stride} + {operands.Read(vaddr, High)}";
        }
        else if (indexed)
        {
            var index = operands.Read(vaddr);
            address += $" + {index} * {operands.Read(resource, StrideDword)}";
        }
        else if (offset)
        {
            address += $" + {operands.Read(vaddr)}";
        }

        return address + ")";
    }
}
