// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Runtime.InteropServices;
using FiveStar.Loader.Native;

namespace FiveStar.Loader.Shims.D3D12X;

/// <summary>
/// The D3D12.X descriptor slots that take no resource object (call sites in docs/d3d12x-shim.md). Slot 70 describes a
/// view by a D3D12_RESOURCE_DESC plus a 64-byte view desc: the desktop SRV desc (0x28 bytes) and the GPU address at
/// +0x28. Slot 61 takes a 0x6c-byte console sampler desc. Until resources exist at console GPU addresses and the
/// sampler fields are known, both write a valid desktop descriptor in the game's slot: a null SRV of the view's
/// dimension and format, and a trilinear wrapping sampler.
/// </summary>
internal static unsafe class XboxAddressDescriptors
{
    public const int CreateShaderResourceViewAtAddressSlot = 70;
    public const int CreateSamplerXSlot = 61;

    /// <summary>ID3D12Device::CreateShaderResourceView and ::CreateSampler (d3d12.h).</summary>
    private const int DesktopShaderResourceViewSlot = 18;
    private const int DesktopSamplerSlot = 22;

    /// <summary>
    /// D3D12_SAMPLER_DESC (13 dwords): MIN_MAG_MIP_LINEAR, WRAP on U, V and W, no LOD bias, anisotropy 1,
    /// COMPARISON_FUNC_NEVER, a transparent black border, LOD 0 to D3D12_FLOAT32_MAX.
    /// </summary>
    private const uint LinearFilter = 0x15;
    private const uint WrapAddress = 1;
    private const uint NoAnisotropy = 1;
    private const uint NeverCompare = 1;
    private const int SamplerDwords = 13;
    private const int AddressUDword = 1;
    private const int AddressVDword = 2;
    private const int AddressWDword = 3;
    private const int AnisotropyDword = 5;
    private const int ComparisonDword = 6;
    private const int MaxLodDword = 12;

    [UnmanagedCallersOnly]
    public static void CreateShaderResourceViewAtAddress(XboxObject* self, ResourceDescription* resource, void* view, nint handle)
    {
        D3D12X.Warnings.Warn("slot70", "[d3d12_xs] ID3D12Device.X::Slot70 (SRV at a GPU address): no desktop resource there yet, null descriptors written");
        nint inner = self->Inner;
        var create = (delegate* unmanaged<nint, nint, void*, nint, void>)ComObject.Slot(inner, DesktopShaderResourceViewSlot);
        create(inner, 0, view, D3D12X.DescriptorHeaps.Translate(handle));
    }

    [UnmanagedCallersOnly]
    public static void CreateSamplerX(XboxObject* self, void* description, nint handle)
    {
        D3D12X.Warnings.Warn("slot61", "[d3d12_xs] ID3D12Device.X::Slot61 (console sampler desc): fields unknown, trilinear wrapping samplers written");
        uint* sampler = stackalloc uint[SamplerDwords];
        new Span<uint>(sampler, SamplerDwords).Clear();

        sampler[0] = LinearFilter;
        sampler[AddressUDword] = WrapAddress;
        sampler[AddressVDword] = WrapAddress;
        sampler[AddressWDword] = WrapAddress;

        sampler[AnisotropyDword] = NoAnisotropy;
        sampler[ComparisonDword] = NeverCompare;
        *(float*)(sampler + MaxLodDword) = float.MaxValue;

        nint inner = self->Inner;
        var create = (delegate* unmanaged<nint, uint*, nint, void>)ComObject.Slot(inner, DesktopSamplerSlot);
        create(inner, sampler, D3D12X.DescriptorHeaps.Translate(handle));
    }

    private static XboxDeviceFactory D3D12X => ShimContext.Runtime.D3D12X;
}
