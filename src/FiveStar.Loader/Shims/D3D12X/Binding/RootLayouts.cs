// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Collections.Concurrent;
using System.Runtime.InteropServices;

namespace FiveStar.Loader.Shims.D3D12X.Binding;

/// <summary>
/// The <see cref="RootLayout"/> of every root signature the game created (device slot 16), by desktop object. A layout
/// stays allocated for the process: lists may hold it after the root signature is gone, and a reused address gets a new
/// one.
/// </summary>
internal sealed unsafe class RootLayouts
{
    private readonly ConcurrentDictionary<nint, nint> _layouts = new();

    /// <summary>Registers the layout of <paramref name="blob"/> for <paramref name="rootSignature"/>; false when the blob is not one.</summary>
    public bool Register(nint rootSignature, ReadOnlySpan<byte> blob)
    {
        if (rootSignature == 0 || !RootSignatureParser.TryParse(blob, out var layout))
        {
            return false;
        }

        var native = (RootLayout*)NativeMemory.Alloc((nuint)sizeof(RootLayout));
        *native = layout;
        _layouts[rootSignature] = (nint)native;

        return true;
    }

    /// <summary>The layout of a registered root signature; null for any other pointer.</summary>
    [HotPath]
    public RootLayout* Find(nint rootSignature)
    {
        if (rootSignature != 0 && _layouts.TryGetValue(rootSignature, out nint layout))
        {
            return (RootLayout*)layout;
        }

        return null;
    }
}
