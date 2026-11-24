// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using FiveStar.Shaders.Dxil.Hlsl;

namespace FiveStar.Shaders.Dxil.Lowering;

/// <summary>Texture, typed-buffer and structured-buffer methods.</summary>
public sealed class TextureLowerer
{
    private readonly LoweringContext _context;

    public TextureLowerer(LoweringContext context)
    {
        _context = context;
    }

    public RValue Method(SyntaxNode call, RValue target)
    {
        throw SourceError.At(call, $"{target.Type.Name}.{call.Text} is not supported ({_context.Flags})");
    }
}
