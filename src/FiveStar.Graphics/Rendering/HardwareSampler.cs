// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using Vortice.Direct3D12;
using Vortice.Mathematics;

namespace FiveStar.Graphics.Rendering;

/// <summary>
/// Turns the four words of an RDNA sampler descriptor (S#, field names from AMD's RDNA2 register reference) into a
/// D3D12 sampler. Translated programs list the S#s they build from literal constants in a comment
/// (<c>// static sampler k: S# = w0 w1 w2 w3</c>); the runtime puts sampler k at heap slot STATIC_SAMPLER_BASE + k.
/// Not mapped: the filter mode (min/max reduction), the comparison filter (the function is kept) and border colours
/// from the border-colour table.
/// </summary>
public static class HardwareSampler
{
    /// <summary>The first sampler heap slot of the static samplers (<c>STATIC_SAMPLER_BASE</c> in rdna_prelude.hlsli).</summary>
    public const int StaticSamplerBase = 2032;

    /// <summary>Sampler heap slots needed for static samplers (D3D12's limit for a shader-visible sampler heap).</summary>
    public const int SamplerHeapSlots = 2048;

    private const int ClampBits = 3;
    private const int ClampY = 3;
    private const int ClampZ = 6;
    private const int AnisoShift = 9;
    private const int CompareShift = 12;
    private const int MaxLodShift = 12;
    private const int LodBits = 12;
    private const int LodBiasBits = 14;
    private const int MagFilterShift = 20;
    private const int MinFilterShift = 22;
    private const int MipFilterShift = 26;
    private const int BorderTypeShift = 30;
    private const int TwoBits = 3;
    private const int ThreeBits = 7;
    private const float LodScale = 256f;
    private const int AnisoFilterKind = 2;
    private const int MipLinear = 2;
    private const int MipNone = 0;
    private const int MinShift = 4;
    private const int MagShift = 2;
    private const int AnisotropicFilter = 0x55;
    private const int OpaqueBlack = 1;
    private const int OpaqueWhite = 2;
    private const int WordBits = 32;
    private const int FilterWord = 2;
    private const int BorderWord = 3;
    private const int HexBase = 16;

    // SQ_TEX_CLAMP: wrap, mirror, clamp last texel, mirror once last texel, clamp half border, mirror once half
    // border, clamp border, mirror once border.
    private static readonly TextureAddressMode[] AddressModes =
    [
        TextureAddressMode.Wrap,
        TextureAddressMode.Mirror,
        TextureAddressMode.Clamp,
        TextureAddressMode.MirrorOnce,
        TextureAddressMode.Clamp,
        TextureAddressMode.MirrorOnce,
        TextureAddressMode.Border,
        TextureAddressMode.MirrorOnce,
    ];

    public static SamplerDescription Describe(IReadOnlyList<uint> words)
    {
        var (w0, w1, w2, w3) = (words[0], words[1], words[FilterWord], words[BorderWord]);
        var anisotropy = 1 << (int)((w0 >> AnisoShift) & ThreeBits);
        var magnify = (int)((w2 >> MagFilterShift) & TwoBits);
        var minify = (int)((w2 >> MinFilterShift) & TwoBits);
        var mip = (int)((w2 >> MipFilterShift) & TwoBits);
        var maxLod = ((w1 >> MaxLodShift) & Mask(LodBits)) / LodScale;

        if (mip == MipNone)
        {
            maxLod = 0;
        }

        return new SamplerDescription
        {
            Filter = FilterOf(minify, magnify, mip, anisotropy),
            AddressU = AddressModes[w0 & ThreeBits],
            AddressV = AddressModes[(w0 >> ClampY) & ThreeBits],
            AddressW = AddressModes[(w0 >> ClampZ) & ThreeBits],
            MipLODBias = SignExtend(w2 & Mask(LodBiasBits), LodBiasBits) / LodScale,
            MaxAnisotropy = (uint)anisotropy,
            ComparisonFunction = (ComparisonFunction)(((w0 >> CompareShift) & ThreeBits) + 1),
            BorderColor = Border((int)(w3 >> BorderTypeShift)),
            MinLOD = (w1 & Mask(LodBits)) / LodScale,
            MaxLOD = maxLod,
        };
    }

    /// <summary>The S# words of each <c>// static sampler k: S# = ...</c> line of a translated program, in slot order.</summary>
    public static IReadOnlyList<uint[]> StaticSamplers(string hlsl)
    {
        const string Marker = "// static sampler ";
        const string WordsMarker = "S# = ";

        return hlsl.Split('\n')
            .Where(line => line.StartsWith(Marker, StringComparison.Ordinal))
            .Select(line => line[(line.IndexOf(WordsMarker, StringComparison.Ordinal) + WordsMarker.Length)..].Trim())
            .Select(text => text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(word => Convert.ToUInt32(word, HexBase)).ToArray())
            .ToList();
    }

    private static Filter FilterOf(int minify, int magnify, int mip, int anisotropy)
    {
        if (anisotropy > 1 && (minify >= AnisoFilterKind || magnify >= AnisoFilterKind))
        {
            return (Filter)AnisotropicFilter;
        }

        var linearMip = 0;

        if (mip == MipLinear)
        {
            linearMip = 1;
        }

        return (Filter)(((minify & 1) << MinShift) | ((magnify & 1) << MagShift) | linearMip);
    }

    private static Color4 Border(int type)
    {
        if (type == OpaqueBlack)
        {
            return new Color4(0, 0, 0, 1);
        }

        if (type == OpaqueWhite)
        {
            return new Color4(1, 1, 1, 1);
        }

        return new Color4(0, 0, 0, 0);
    }

    private static uint Mask(int bits)
    {
        return (1u << bits) - 1;
    }

    private static float SignExtend(uint value, int bits)
    {
        var shift = WordBits - bits;

        return (int)(value << shift) >> shift;
    }
}
