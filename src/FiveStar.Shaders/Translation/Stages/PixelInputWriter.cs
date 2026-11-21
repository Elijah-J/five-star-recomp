// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using FiveStar.Shaders.Containers;
using FiveStar.Shaders.Translation.Core;

namespace FiveStar.Shaders.Translation.Stages;

/// <summary>
/// Writes the PSIn struct of a pixel shader and the statements that assemble each attrN from it. An attribute read
/// only through v_interp_mov p0 is nointerpolation; one read per vertex uses GetAttributeAtVertex.
/// </summary>
internal sealed class PixelInputWriter
{
    private const string VertexModePrefix = "vtx";

    private readonly TranslationState _state;

    public PixelInputWriter(TranslationState state)
    {
        _state = state;
    }

    public void Write(List<string> lines)
    {
        var registers = SignatureLayout.Registers(_state.Signatures.Inputs, SignatureLayout.PixelSystemInputs);
        lines.Add("struct PSIn {");

        foreach (var attribute in _state.Attributes.Keys.Order())
        {
            WriteAttribute(lines, registers, attribute);
        }

        lines.Add("};");
    }

    private void WriteAttribute(List<string> lines, List<List<SignatureElement>> registers, int attribute)
    {
        var modes = new HashSet<string>(_state.Attributes[attribute]);
        var vertices = modes.Where(mode => mode.StartsWith(VertexModePrefix, StringComparison.Ordinal))
            .Select(mode => int.Parse(mode[VertexModePrefix.Length..], CultureInfo.InvariantCulture)).Order().ToList();

        if (vertices.Count > 0 && modes.Contains("linear"))
        {
            throw new UnsupportedProgramException("attribute both interpolated and read per-vertex");
        }

        if (vertices.Count > 0 && attribute >= registers.Count)
        {
            throw new UnsupportedProgramException("per-vertex attribute without signature");
        }

        if (vertices.Count > 0)
        {
            modes.Add("flat");
        }

        var linear = modes.Contains("linear");
        var flatOnly = modes.Contains("flat") && !linear;

        if (linear || modes.Contains("flat"))
        {
            WriteInterpolated(lines, registers, attribute, vertices, flatOnly);
        }

        if (modes.Contains("f16lo"))
        {
            lines.Add($"  float4 attrlo{attribute} : ATTRLO{attribute};");
            _state.InputMap.Add($"  float4 attrlo{attribute} = pin.attrlo{attribute};");
        }

        if (modes.Contains("f16hi"))
        {
            lines.Add($"  float4 attrhi{attribute} : ATTRHI{attribute};");
            _state.InputMap.Add($"  float4 attrhi{attribute} = pin.attrhi{attribute};");
        }
    }

    private void WriteInterpolated(List<string> lines, List<List<SignatureElement>> registers, int attribute, List<int> vertices, bool flatOnly)
    {
        var map = _state.InputMap;

        if (attribute >= registers.Count)
        {
            var qualifier = flatOnly ? "nointerpolation " : string.Empty;
            lines.Add($"  {qualifier}float4 attr{attribute} : TEXCOORD{attribute};");
            map.Add($"  float4 attr{attribute} = pin.attr{attribute};");

            return;
        }

        map.Add($"  float4 attr{attribute} = 0;");

        foreach (var vertex in vertices)
        {
            map.Add($"  float4 attr{attribute}v{vertex} = 0;");
        }

        foreach (var element in registers[attribute])
        {
            var mask = element.Mask;
            var member = $"a{attribute}_{mask}";

            foreach (var vertex in vertices)
            {
                map.Add($"  attr{attribute}v{vertex}.{mask} = {SignatureLayout.ToFloat(element, $"GetAttributeAtVertex(pin.{member}, {vertex})")};");
            }

            var type = SignatureLayout.TypeName(element);
            var qualifier = flatOnly || type != "float" ? "nointerpolation " : string.Empty;
            lines.Add($"  {qualifier}{type}{SignatureLayout.Width(mask)} {member} : {element.Name}{element.SemanticIndex};");
            map.Add($"  attr{attribute}.{mask} = {SignatureLayout.ToFloat(element, $"pin.{member}")};");
        }
    }
}
