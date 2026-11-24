// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

namespace FiveStar.Shaders.Dxil.Hlsl;

/// <summary>
/// A node of the syntax tree. <see cref="Text"/> is a declared or called name, an operator or a member;
/// <see cref="Children"/> are, by kind:
/// <list type="bullet">
/// <item>Function: parameters, then the body block (none for a declaration); Struct, Cbuffer: fields; Variable: declarators;
/// Declarator: the initializer, if any.</item>
/// <item>If: condition, then, else; For: init, condition, step (each may be Empty), body; While: condition, body; Do:
/// body, condition; Switch: value, cases; Case: labels (a null-text Name for default), then statements under
/// <see cref="Body"/>.</item>
/// <item>Unary, PostIncrement: operand; Binary, Assign: left, right; Conditional: condition, true, false; Call: arguments;
/// MethodCall: target, arguments; Cast: operand; Member: operand; Index: operand, index.</item>
/// </list>
/// </summary>
public sealed class SyntaxNode
{
    public SyntaxNode(SyntaxKind kind, int line)
    {
        Kind = kind;
        Line = line;
    }

    public SyntaxKind Kind { get; }

    public int Line { get; }

    public string Text { get; init; } = string.Empty;

    public List<SyntaxNode> Children { get; } = [];

    /// <summary>A case's statements.</summary>
    public List<SyntaxNode> Body { get; } = [];

    /// <summary>A declaration's type, a function's return type or a cast's target.</summary>
    public TypeSyntax? Type { get; set; }

    /// <summary>Storage and parameter modifiers (static, const, groupshared, in, out, inout, nointerpolation, ...).</summary>
    public HashSet<string> Modifiers { get; } = new(StringComparer.Ordinal);

    public string? Semantic { get; set; }

    /// <summary>Attributes before a function or statement (<c>[numthreads(8, 1, 1)]</c>, <c>[loop]</c>).</summary>
    public List<SyntaxNode> Attributes { get; } = [];

    /// <summary>A literal's token.</summary>
    public Token Literal { get; init; }

    public SyntaxNode this[int index] => Children[index];

    public override string ToString()
    {
        return $"{Kind} {Text}";
    }
}
