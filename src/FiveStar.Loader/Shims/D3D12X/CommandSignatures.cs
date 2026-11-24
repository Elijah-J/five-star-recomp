// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Collections.Concurrent;
using System.Globalization;
using System.Text;

namespace FiveStar.Loader.Shims.D3D12X;

/// <summary>
/// The game's command-signature descriptions by wrapper, as text and stride, for the breadcrumb report
/// (docs/d3d12x-shim.md#queue-progress). Each new one is logged once as a detail.
/// </summary>
internal static unsafe class CommandSignatures
{
    private static readonly ConcurrentDictionary<nint, (uint Stride, string Text)> Known = new();

    /// <summary>Notes the game's <paramref name="description"/> for the created <paramref name="signature"/> wrapper.</summary>
    public static void Created(nint signature, CommandSignatureDescription* description, nint rootSignature)
    {
        string root = "set";

        if (rootSignature == 0)
        {
            root = "none";
        }

        var text = new StringBuilder();
        text.Append(CultureInfo.InvariantCulture, $"stride {description->ByteStride}, root signature {root}:");

        for (uint i = 0; i < description->ArgumentCount; i++)
        {
            var argument = description->Arguments[i];
            text.Append(CultureInfo.InvariantCulture, $" [type 0x{argument.Type:x} {argument.First} {argument.Second} {argument.Third}]");
        }

        string described = text.ToString();
        Known[signature] = (description->ByteStride, described);
        ShimContext.Runtime.Log.Detail($"  [d3d12_xs] command signature 0x{signature:x}: {described}");
    }

    public static string Describe(nint signature)
    {
        if (Known.TryGetValue(signature, out var known))
        {
            return known.Text;
        }

        return string.Create(CultureInfo.InvariantCulture, $"0x{signature:x}");
    }

    public static uint Stride(nint signature)
    {
        return Known.GetValueOrDefault(signature).Stride;
    }
}
