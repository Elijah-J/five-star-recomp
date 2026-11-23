// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using FiveStar.Shaders.Containers;

namespace FiveStar.Loader.Shims.D3D12X.Pipelines;

/// <summary>
/// Renames the game's vertex elements to what a translated vertex shader reads: fetched attribute n is <c>ATTR</c>
/// index n, and attribute n is the n-th non-system input of the shader's ISG1 signature (the cache's input layouts
/// list exactly those, in that order). Elements the shader does not read get unique placeholder names.
/// </summary>
internal static class InputLayoutMapping
{
    public const string AttributeSemantic = "ATTR";
    public const string UnusedSemantic = "UNUSED";

    private const string InputSignature = "ISG1";

    /// <summary>The layout for <paramref name="vertexShader"/>; a cache record's layout is already in attribute order.</summary>
    public static IReadOnlyList<VertexElement> Map(IReadOnlyList<VertexElement> layout, ShaderSource vertexShader)
    {
        if (vertexShader.Kind == ShaderSourceKind.CacheCode)
        {
            return layout.Select((element, index) => element with { Semantic = AttributeSemantic, SemanticIndex = index }).ToList();
        }

        var inputs = Inputs(vertexShader.Bytes);
        var mapped = new List<VertexElement>(layout.Count);
        var unused = 0;

        foreach (var element in layout)
        {
            var attribute = inputs.FindIndex(input =>
                string.Equals(input.Name, element.Semantic, StringComparison.OrdinalIgnoreCase) && input.SemanticIndex == element.SemanticIndex);

            if (attribute < 0)
            {
                mapped.Add(element with { Semantic = UnusedSemantic, SemanticIndex = unused++ });

                continue;
            }

            mapped.Add(element with { Semantic = AttributeSemantic, SemanticIndex = attribute });
        }

        return mapped;
    }

    private static List<SignatureElement> Inputs(byte[] container)
    {
        var signature = DxbcContainer.Parse(container).FindFirst(InputSignature);

        if (signature is null)
        {
            return [];
        }

        return SignatureParser.Parse(signature.Data.Span).Where(input => input.SystemValue.Length == 0).ToList();
    }
}
