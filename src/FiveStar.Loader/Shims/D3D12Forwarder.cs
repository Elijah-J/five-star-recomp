// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Runtime.InteropServices;
using FiveStar.Loader.Diagnostics;

namespace FiveStar.Loader.Shims;

/// <summary>
/// Forwards the d3d12_xs.dll root-signature functions to the PC d3d12.dll, whose C signatures are the same. This is
/// where struct translation goes once D3D12.X-only root-signature flags or desc versions show up.
/// </summary>
internal sealed unsafe class D3D12Forwarder
{
    private const string PcLibrary = "d3d12.dll";

    private readonly object _gate = new();
    private readonly LoaderLog _log;
    private nint _library;

    public D3D12Forwarder(LoaderLog log)
    {
        _log = log;
    }

    /// <summary>HRESULT D3D12SerializeVersionedRootSignature(const D3D12_VERSIONED_ROOT_SIGNATURE_DESC*, ID3DBlob**, ID3DBlob**).</summary>
    public int SerializeVersionedRootSignature(nint desc, nint* blob, nint* error)
    {
        uint version = 0;

        if (desc != 0)
        {
            version = *(uint*)desc;
        }

        var target = (delegate* unmanaged<nint, nint*, nint*, int>)Export("D3D12SerializeVersionedRootSignature");
        int hr = target(desc, blob, error);
        _log.Detail($"  [d3d12_xs] D3D12SerializeVersionedRootSignature(version=0x{version:x}) -> 0x{hr:x8} (forwarded to d3d12.dll)");

        return hr;
    }

    /// <summary>HRESULT D3D12SerializeRootSignature(const D3D12_ROOT_SIGNATURE_DESC*, D3D_ROOT_SIGNATURE_VERSION, ID3DBlob**, ID3DBlob**).</summary>
    public int SerializeRootSignature(nint desc, uint version, nint* blob, nint* error)
    {
        var target = (delegate* unmanaged<nint, uint, nint*, nint*, int>)Export("D3D12SerializeRootSignature");
        int hr = target(desc, version, blob, error);
        _log.Detail($"  [d3d12_xs] D3D12SerializeRootSignature(version=0x{version:x}) -> 0x{hr:x8} (forwarded)");

        return hr;
    }

    /// <summary>HRESULT D3D12Create[Versioned]RootSignatureDeserializer(const void* data, SIZE_T size, REFIID, void** out).</summary>
    public int CreateDeserializer(string function, nint data, nuint size, Guid* iid, nint* result)
    {
        var target = (delegate* unmanaged<nint, nuint, Guid*, nint*, int>)Export(function);
        int hr = target(data, size, iid, result);
        _log.Detail($"  [d3d12_xs] {function}(size=0x{size:x}, iid={*iid}) -> 0x{hr:x8} (forwarded)");

        return hr;
    }

    private nint Export(string name)
    {
        lock (_gate)
        {
            if (_library == 0)
            {
                _library = NativeLibrary.Load(PcLibrary);
            }

            return NativeLibrary.GetExport(_library, name);
        }
    }
}
