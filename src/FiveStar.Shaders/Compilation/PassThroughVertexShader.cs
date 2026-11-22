// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text;
using FiveStar.Shaders.Containers;

namespace FiveStar.Shaders.Compilation;

/// <summary>
/// Writes a vertex shader whose outputs match the input signature of a compiled PS or GS: the same elements in
/// declaration order with the same types, widths, array rows and interpolation modes, so dxc's prefix-stable packing
/// puts them in the same registers. Values the rasterizer generates (front face, sample index, primitive id, ...) are
/// left out; SV_Position is appended when the consumer does not read it but needs it.
/// </summary>
public static class PassThroughVertexShader
{
    // HLSL modifier per DXIL::InterpolationMode (0 undefined, 1 constant, 2 linear, 3 centroid, 4 noperspective,
    // 5 noperspective centroid, 6 sample, 7 noperspective sample).
    private static readonly string[] InterpolationModifiers =
    [
        string.Empty, "nointerpolation ", string.Empty, "centroid ", "noperspective ", "noperspective centroid ", "sample ",
        "noperspective sample ",
    ];

    // HLSL scalar type per DXIL signature component type (1 uint, 2 int, 3 float, 4..6 their 16-bit forms).
    private static readonly string[] ScalarTypes = [string.Empty, "uint", "int", "float", "min16uint", "min16int", "min16float"];

    private static readonly Dictionary<PsvSemanticKind, string> Passed = new()
    {
        [PsvSemanticKind.Position] = "SV_Position",
        [PsvSemanticKind.ClipDistance] = "SV_ClipDistance",
        [PsvSemanticKind.CullDistance] = "SV_CullDistance",
        [PsvSemanticKind.RenderTargetArrayIndex] = "SV_RenderTargetArrayIndex",
        [PsvSemanticKind.ViewportArrayIndex] = "SV_ViewportArrayIndex",
    };

    private static readonly HashSet<PsvSemanticKind> Generated =
    [
        PsvSemanticKind.PrimitiveId, PsvSemanticKind.GsInstanceId, PsvSemanticKind.SampleIndex, PsvSemanticKind.IsFrontFace,
        PsvSemanticKind.Coverage, PsvSemanticKind.InnerCoverage, PsvSemanticKind.ViewId, PsvSemanticKind.Barycentrics,
        PsvSemanticKind.ShadingRate,
    ];

    /// <summary>The HLSL source (entry point main) for the consumer's inputs.</summary>
    public static string Source(IReadOnlyList<PsvElement> consumerInputs, bool needsPosition)
    {
        var members = new StringBuilder();
        var hasPosition = false;
        var count = 0;

        foreach (var element in consumerInputs)
        {
            if (Generated.Contains(element.SemanticKind))
            {
                continue;
            }

            hasPosition |= element.SemanticKind == PsvSemanticKind.Position;
            members.Append(Member(element, $"e{count}"));
            count++;
        }

        if (needsPosition && !hasPosition)
        {
            members.Append("    float4 position : SV_Position;\n");
        }

        return $"struct PassOut\n{{\n{members}}};\n\nPassOut main()\n{{\n    PassOut o = (PassOut)0;\n    return o;\n}}\n";
    }

    private static string Member(PsvElement element, string name)
    {
        var semantic = element.Name + element.SemanticIndex;
        var modifier = string.Empty;

        if (element.SemanticKind == PsvSemanticKind.Arbitrary)
        {
            modifier = InterpolationModifiers[element.InterpolationMode];
        }
        else if (Passed.TryGetValue(element.SemanticKind, out var systemName))
        {
            semantic = systemName + element.SemanticIndex;
        }
        else
        {
            throw new NotSupportedException($"pass-through of semantic kind {element.SemanticKind}");
        }

        var type = ScalarTypes[element.ComponentType];

        if (element.Columns > 1)
        {
            type += element.Columns;
        }

        var array = string.Empty;

        if (element.Rows > 1)
        {
            array = $"[{element.Rows}]";
        }

        return $"    {modifier}{type} {name}{array} : {semantic};\n";
    }
}
