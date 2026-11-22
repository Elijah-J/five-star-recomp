// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using Vortice.Direct3D12;

namespace FiveStar.Graphics.Pipelines;

/// <summary>
/// The root signature every translated program runs with. Parameter 0 is a root CBV at b0 (the 128 user-data dwords of
/// <c>cbuffer UserData</c>, too many for root constants); parameter 1 is a descriptor table with the mesh fallback's
/// u0..u2 and t0 in register space 9. Everything else is reached through <c>ResourceDescriptorHeap[]</c> and
/// <c>SamplerDescriptorHeap[]</c>.
/// </summary>
public static class BindlessRootSignature
{
    /// <summary>Root parameter index of the user-data CBV.</summary>
    public const int UserDataParameter = 0;

    /// <summary>Root parameter index of the mesh fallback table.</summary>
    public const int FallbackTableParameter = 1;

    /// <summary>Register space of the mesh fallback bindings (rdna_prelude / MeshComputeFallback).</summary>
    public const int FallbackSpace = 9;

    /// <summary>Number of fallback UAVs (u0 vertex records, u1 indices, u2 draw arguments), followed by one SRV (t0).</summary>
    public const int FallbackUavs = 3;

    private const int UserDataRegister = 0;
    private const int UserDataSpace = 0;
    private const int FallbackSrvs = 1;

    public static RootSignatureDescription1 Description()
    {
        var flags = RootSignatureFlags.ConstantBufferViewShaderResourceViewUnorderedAccessViewHeapDirectlyIndexed
            | RootSignatureFlags.SamplerHeapDirectlyIndexed
            | RootSignatureFlags.AllowInputAssemblerInputLayout;
        var volatileRange = DescriptorRangeFlags.DataVolatile | DescriptorRangeFlags.DescriptorsVolatile;
        DescriptorRange1[] fallbackRanges =
        [
            new(DescriptorRangeType.UnorderedAccessView, FallbackUavs, 0, FallbackSpace, 0, volatileRange),
            new(DescriptorRangeType.ShaderResourceView, FallbackSrvs, 0, FallbackSpace, FallbackUavs, volatileRange),
        ];
        RootParameter1[] parameters =
        [
            new(RootParameterType.ConstantBufferView, new RootDescriptor1(UserDataRegister, UserDataSpace, RootDescriptorFlags.DataVolatile), ShaderVisibility.All),
            new(new RootDescriptorTable1(fallbackRanges), ShaderVisibility.All),
        ];

        return new RootSignatureDescription1(flags, parameters);
    }

    public static ID3D12RootSignature Create(ID3D12Device device)
    {
        return device.CreateRootSignature(Description());
    }
}
