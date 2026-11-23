// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Runtime.InteropServices;
using FiveStar.Loader.Native;

namespace FiveStar.Loader.Shims;

/// <summary>
/// d3d12_xs.dll (D3D12.X) entry points: the root-signature helpers, forwarded to the PC d3d12.dll. Everything else in
/// d3d12_xs (device creation, the D3D12.X interfaces) is not implemented and binds to a logged stub returning
/// E_NOTIMPL: the game tests D3D12XboxCreateDevice with <c>js</c>, where S_OK with no device would fault.
/// </summary>
[ShimModule("d3d12_xs.dll", UnresolvedReturn = HResult.NotImplemented)]
internal static unsafe class D3D12XsShim
{
    [Shim("d3d12_xs.dll", "D3D12SerializeVersionedRootSignature")]
    [UnmanagedCallersOnly]
    private static int D3D12SerializeVersionedRootSignature(nint desc, nint* blob, nint* error)
    {
        return ShimContext.Runtime.D3D12.SerializeVersionedRootSignature(desc, blob, error);
    }

    /// <summary>
    /// D3D12ConfigureVirtualMemoryX(config): the console's GPU virtual-address layout, set before the game reserves its
    /// GPU memory. A desktop GPU has its own, so this accepts any configuration; the game stops on a failure.
    /// </summary>
    [Shim("d3d12_xs.dll", "D3D12ConfigureVirtualMemoryX")]
    [UnmanagedCallersOnly]
    private static int D3D12ConfigureVirtualMemoryX(nint configuration)
    {
        ShimContext.Runtime.Log.Detail($"  [d3d12] D3D12ConfigureVirtualMemoryX(0x{configuration:x}) -> S_OK");

        return HResult.Ok;
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
