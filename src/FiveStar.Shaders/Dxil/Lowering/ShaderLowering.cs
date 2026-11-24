// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using FiveStar.Shaders.Dxil.Container;
using FiveStar.Shaders.Dxil.Hlsl;
using FiveStar.Shaders.Dxil.Ir;

namespace FiveStar.Shaders.Dxil.Lowering;

/// <summary>
/// Lowers a parsed compute program into a DXIL module: declarations, the entry block (globals, the user-data handle,
/// the entry parameters), the body with every call inlined, then SSA cleanup and the flags the module needs.
/// </summary>
public static class ShaderLowering
{
    private const string EntryName = "main";

    public static (DxilModule Module, IrFunction Main, ShaderDescription Description) Compute(IReadOnlyList<SyntaxNode> declarations)
    {
        var module = new DxilModule();
        var types = module.Types;
        var main = module.Function(EntryName, types.Function(types.Void, []), AttributeSet.None, hasBody: true);
        var placeholder = module.NewBlock();
        var context = new LoweringContext(module, placeholder);
        var entry = context.Ssa.NewBlock();
        context.Ssa.Seal(entry);
        context.Builder.Block = entry;
        context.EntryBlock = entry;
        context.Flow.Entry = entry;

        var globals = new GlobalDeclarations(context);
        globals.Collect(declarations);
        var source = Entry(context, declarations);
        globals.Declare(declarations);

        var computeEntry = new ComputeEntry(context);
        var threads = computeEntry.Threads(source);
        var waveSize = computeEntry.WaveSize(source);
        context.Scope.Push();
        computeEntry.BindParameters(source);
        Body(context, source);
        context.Scope.Pop();

        context.Ssa.Finish();
        main.Blocks.AddRange(context.Ssa.Blocks);
        var flags = context.Flags | FeatureScan.Flags(main);

        return (module, main, new ShaderDescription(threads, waveSize, flags, context.UserDataBytes));
    }

    private static SyntaxNode Entry(LoweringContext context, IReadOnlyList<SyntaxNode> declarations)
    {
        if (!context.Functions.TryGetValue(EntryName, out var overloads))
        {
            throw new DxilBackendException("no entry function main");
        }

        return overloads[0];
    }

    private static void Body(LoweringContext context, SyntaxNode main)
    {
        var flow = context.Flow;
        var exit = flow.NewBlock();
        var body = main.Children.Last(child => child.Kind == SyntaxKind.Block);
        flow.PushFrame(new FunctionFrame(exit, HlslType.Void, []));
        context.Statements.LowerBlockContents(body);
        flow.Jump(exit);
        flow.PopFrame();
        flow.Seal(exit);
        flow.Enter(exit);
        context.Builder.Return();
    }
}
