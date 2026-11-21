// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Diagnostics;
using Vortice.Direct3D;
using Vortice.Direct3D12;
using Vortice.DXGI;

namespace FiveStar.Shaders.Tests.Warp;

/// <summary>
/// A D3D12 device on the WARP software adapter with one direct queue. A shader that removes the device (WARP detects
/// some descriptor mismatches) only fails its own run: the next run recreates the device.
/// </summary>
public sealed class WarpDevice : IDisposable
{
    private static readonly TimeSpan GpuTimeout = TimeSpan.FromSeconds(20);

    private ID3D12Device _device = null!;
    private ID3D12CommandQueue _queue = null!;
    private ID3D12Fence _fence = null!;
    private ulong _fenceValue;

    public WarpDevice()
    {
        Create();
    }

    public ID3D12Device Device => _device;

    /// <summary>Executes a recorded command list and waits for it.</summary>
    public void Execute(ID3D12GraphicsCommandList list)
    {
        list.Close();
        _queue.ExecuteCommandList(list);
        _fenceValue++;
        _queue.Signal(_fence, _fenceValue).CheckError();
        var clock = Stopwatch.StartNew();

        while (_fence.CompletedValue < _fenceValue)
        {
            if (_device.DeviceRemovedReason.Failure)
            {
                throw new InvalidOperationException($"device removed: {_device.DeviceRemovedReason}");
            }

            if (clock.Elapsed > GpuTimeout)
            {
                throw new TimeoutException("GPU timeout (an endless loop in the shader?)");
            }

            Thread.Sleep(0);
        }
    }

    /// <summary>Recreates the device when the last run removed it.</summary>
    public void Recover()
    {
        if (!_device.DeviceRemovedReason.Failure)
        {
            return;
        }

        Release();
        Create();
    }

    public void Dispose()
    {
        Release();
    }

    private void Create()
    {
        using var factory = DXGI.CreateDXGIFactory2<IDXGIFactory4>(false);
        using var adapter = factory.EnumWarpAdapter<IDXGIAdapter>();
        D3D12.D3D12CreateDevice(adapter, FeatureLevel.Level_11_0, out ID3D12Device? device).CheckError();

        _device = device!;
        _queue = _device.CreateCommandQueue(new CommandQueueDescription(CommandListType.Direct));
        _fence = _device.CreateFence(0);
        _fenceValue = 0;
    }

    private void Release()
    {
        _fence.Dispose();
        _queue.Dispose();
        _device.Dispose();
    }
}
