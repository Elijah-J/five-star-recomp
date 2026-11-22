// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using Vortice.Direct3D12;

namespace FiveStar.Graphics.Rendering;

/// <summary>
/// A direct queue with one command list and a fence. <see cref="ExecuteAndWait"/> submits what the list recorded,
/// waits for the GPU and opens the list again.
/// </summary>
public sealed class CommandContext : IDisposable
{
    // Waits of about 1 ms, up to about a minute.
    private const int WaitMilliseconds = 1;
    private const int MaxWaits = 60_000;

    private readonly ID3D12Device _device;
    private readonly ID3D12CommandQueue _queue;
    private readonly ID3D12CommandAllocator _allocator;
    private readonly ID3D12Fence _fence;
    private ulong _fenceValue;

    public CommandContext(ID3D12Device device)
    {
        _device = device;
        _queue = device.CreateCommandQueue(new CommandQueueDescription(CommandListType.Direct));
        _allocator = device.CreateCommandAllocator(CommandListType.Direct);
        _fence = device.CreateFence(0);
        List = device.CreateCommandList<ID3D12GraphicsCommandList>(0, CommandListType.Direct, _allocator, null);
    }

    public ID3D12GraphicsCommandList List { get; }

    /// <summary>Executes the recorded commands, waits until the GPU finished them and resets the list.</summary>
    public void ExecuteAndWait()
    {
        List.Close();
        _queue.ExecuteCommandList(List);
        _fenceValue++;
        _queue.Signal(_fence, _fenceValue).CheckError();

        for (var waited = 0; _fence.CompletedValue < _fenceValue; waited++)
        {
            if (_device.DeviceRemovedReason.Failure)
            {
                throw new InvalidOperationException($"device removed: {_device.DeviceRemovedReason}");
            }

            if (waited > MaxWaits)
            {
                throw new TimeoutException("the GPU did not finish the command list");
            }

            Thread.Sleep(WaitMilliseconds);
        }

        _allocator.Reset();
        List.Reset(_allocator, null);
    }

    public void Dispose()
    {
        List.Dispose();
        _fence.Dispose();
        _allocator.Dispose();
        _queue.Dispose();
    }
}
