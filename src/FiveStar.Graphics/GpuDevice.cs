// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using Vortice.Direct3D;
using Vortice.Direct3D12;
using Vortice.Direct3D12.Debug;
using Vortice.DXGI;

namespace FiveStar.Graphics;

/// <summary>
/// A D3D12 device on one DXGI adapter. With the debug layer the device's info queue keeps the validation messages,
/// which <see cref="TakeErrors"/> returns.
/// </summary>
public sealed class GpuDevice : IDisposable
{
    private readonly ID3D12Device _device;
    private readonly ID3D12InfoQueue? _infoQueue;

    public GpuDevice(int adapterIndex, bool debugLayer)
    {
        if (debugLayer)
        {
            D3D12.D3D12GetDebugInterface(out ID3D12Debug? debug).CheckError();
            debug!.EnableDebugLayer();
            debug.Dispose();
        }

        using var factory = DXGI.CreateDXGIFactory2<IDXGIFactory4>(false);
        factory.EnumAdapters1((uint)adapterIndex, out var adapter).CheckError();

        using (adapter)
        {
            AdapterName = adapter.Description1.Description;
            D3D12.D3D12CreateDevice(adapter, FeatureLevel.Level_12_0, out ID3D12Device? device).CheckError();
            _device = device!;
        }

        if (debugLayer)
        {
            _infoQueue = _device.QueryInterfaceOrNull<ID3D12InfoQueue>();
        }
    }

    public ID3D12Device Device => _device;

    public string AdapterName { get; }

    /// <summary>Whether the device was removed (a driver fault); it must then be recreated.</summary>
    public bool Removed => _device.DeviceRemovedReason.Failure;

    /// <summary>
    /// Turns off the driver's implicit shader cache for this process (ID3D12Device9::ShaderCacheControl, Windows
    /// developer mode only), so one-off pipelines are not written to the driver's disk cache. False when not possible.
    /// </summary>
    public bool DisableDriverShaderCache()
    {
        using var device9 = _device.QueryInterfaceOrNull<ID3D12Device9>();

        if (device9 is null)
        {
            return false;
        }

        try
        {
            device9.ShaderCacheControl(ShaderCacheKindFlags.ImplicitDriverManaged, ShaderCacheControlFlags.Disable);
        }
        catch (SharpGen.Runtime.SharpGenException)
        {
            return false;
        }

        return true;
    }

    /// <summary>Drops the stored debug-layer messages.</summary>
    public void ClearMessages()
    {
        _infoQueue?.ClearStoredMessages();
    }

    /// <summary>The error and corruption messages stored since the last call, then clears them; empty without the debug layer.</summary>
    public IReadOnlyList<string> TakeErrors()
    {
        if (_infoQueue is null)
        {
            return [];
        }

        var errors = new List<string>();

        for (ulong index = 0; index < _infoQueue.NumStoredMessages; index++)
        {
            var message = _infoQueue.GetMessage(index);

            if (message.Severity is MessageSeverity.Error or MessageSeverity.Corruption)
            {
                errors.Add(message.Description.TrimEnd('\0'));
            }
        }

        _infoQueue.ClearStoredMessages();

        return errors;
    }

    public void Dispose()
    {
        _infoQueue?.Dispose();
        _device.Dispose();
    }
}
