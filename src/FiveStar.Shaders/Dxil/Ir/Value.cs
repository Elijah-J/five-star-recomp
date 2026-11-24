// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

namespace FiveStar.Shaders.Dxil.Ir;

/// <summary>An LLVM value: a constant, a global variable, a function or an instruction result.</summary>
public abstract class Value
{
    protected Value(IrType type)
    {
        Type = type;
    }

    public IrType Type { get; }
}
