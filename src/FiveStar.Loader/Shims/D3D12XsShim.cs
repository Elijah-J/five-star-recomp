// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Runtime.InteropServices;
using FiveStar.Loader.Native;

namespace FiveStar.Loader.Shims;

/// <summary>
/// d3d12_xs.dll (D3D12.X) entry points: the root-signature helpers, forwarded to the PC d3d12.dll, and device creation
/// on a desktop device (docs/d3d12x-shim.md). Everything else binds to a logged stub returning E_NOTIMPL: S_OK with
/// no result would fault where the game tests the HRESULT.
/// </summary>
[ShimModule("d3d12_xs.dll", UnresolvedReturn = HResult.NotImplemented)]
internal static unsafe class D3D12XsShim
{
    [Shim("d3d12_xs.dll", "D3D12XboxCreateDevice")]
    [UnmanagedCallersOnly]
    private static int D3D12XboxCreateDevice(nint adapter, byte* parameters, Guid* iid, nint* device)
    {
        return ShimContext.Runtime.D3D12X.CreateDevice(adapter, parameters, iid, device);
    }

    [Shim("d3d12_xs.dll", "D3D12ConfigureVirtualMemoryX")]
    [UnmanagedCallersOnly]
    private static int D3D12ConfigureVirtualMemoryX(byte* configuration)
    {
        return ShimContext.Runtime.D3D12X.ConfigureVirtualMemory(configuration);
    }

    [Shim("d3d12_xs.dll", "D3D12SerializeVersionedRootSignature")]
    [UnmanagedCallersOnly]
    private static int D3D12SerializeVersionedRootSignature(nint desc, nint* blob, nint* error)
    {
        return ShimContext.Runtime.D3D12.SerializeVersionedRootSignature(desc, blob, error);
    }

    [Shim("d3d12_xs.dll", "D3D12SerializeRootSignature")]
    [UnmanagedCallersOnly]
    private static int D3D12SerializeRootSignature(nint desc, uint version, nint* blob, nint* error)
    {
        return ShimContext.Runtime.D3D12.SerializeRootSignature(desc, version, blob, error);
    }

    [Shim("d3d12_xs.dll", "D3D12CreateRootSignatureDeserializer")]
    [UnmanagedCallersOnly]
    private static int D3D12CreateRootSignatureDeserializer(nint data, nuint size, Guid* iid, nint* result)
    {
        return ShimContext.Runtime.D3D12.CreateDeserializer("D3D12CreateRootSignatureDeserializer", data, size, iid, result);
    }

    [Shim("d3d12_xs.dll", "D3D12CreateVersionedRootSignatureDeserializer")]
    [UnmanagedCallersOnly]
    private static int D3D12CreateVersionedRootSignatureDeserializer(nint data, nuint size, Guid* iid, nint* result)
    {
        return ShimContext.Runtime.D3D12.CreateDeserializer("D3D12CreateVersionedRootSignatureDeserializer", data, size, iid, result);
    }
}
