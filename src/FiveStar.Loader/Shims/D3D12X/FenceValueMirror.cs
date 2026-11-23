// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Runtime.InteropServices;
using FiveStar.Loader.Native;

namespace FiveStar.Loader.Shims.D3D12X;

/// <summary>
/// Fence slot 12 returns the address of the fence's completed value, which the game then polls (wait loop at
/// 0x144fb0ad0; UINT64_MAX means removed). The desktop only answers GetCompletedValue, so a mirror thread keeps one
/// cell per such fence current: each fence has SetEventOnCompletion(value + 1) on a shared event, and every wake-up
/// re-reads all fences. Released fences leave the set first.
/// </summary>
internal sealed unsafe class FenceValueMirror : IDisposable
{
    public const int CompletedValueAddressSlot = 12;

    /// <summary>ID3D12Fence::GetCompletedValue and ::SetEventOnCompletion (d3d12.h).</summary>
    private const int GetCompletedValueSlot = 8;
    private const int SetEventOnCompletionSlot = 9;

    /// <summary>A re-read even without a completion, in case an event registration was missed.</summary>
    private const int IdleMilliseconds = 16;

    private readonly object _gate = new();
    private readonly HashSet<nint> _fences = [];
    private readonly AutoResetEvent _changed = new(false);
    private Thread? _thread;
    private volatile bool _stopping;

    /// <summary>Adds the fence (once) and returns its cell, current as of now.</summary>
    public ulong* Track(XboxFenceObject* fence)
    {
        lock (_gate)
        {
            if (_fences.Add((nint)fence))
            {
                Refresh(fence, true);
                EnsureThread();
            }
        }

        return &fence->CompletedValue;
    }

    /// <summary>Takes a fence out before its wrapper is freed.</summary>
    public void Forget(XboxFenceObject* fence)
    {
        lock (_gate)
        {
            _fences.Remove((nint)fence);
        }
    }

    public void Dispose()
    {
        _stopping = true;
        _changed.Set();
        _thread?.Join();
        _changed.Dispose();
    }

    /// <summary>Fence slot 12: the address the game reads the completed value from.</summary>
    [UnmanagedCallersOnly]
    public static ulong* CompletedValueAddress(XboxFenceObject* self)
    {
        return ShimContext.Runtime.D3D12X.Fences.Track(self);
    }

    /// <summary>Fence slot 2: Release that drops the fence from the mirror with the last reference.</summary>
    [UnmanagedCallersOnly]
    public static uint ReleaseFence(XboxFenceObject* self)
    {
        nint inner = self->Header.Inner;
        uint left = ((delegate* unmanaged<nint, uint>)ComObject.Slot(inner, ComObject.ReleaseSlot))(inner);

        if (left == 0)
        {
            ShimContext.Runtime.D3D12X.Fences.Forget(self);
            NativeMemory.Free(self);
        }

        return left;
    }

    private void EnsureThread()
    {
        if (_thread is not null)
        {
            return;
        }

        _thread = new Thread(Run) { IsBackground = true, Name = "fivestar fence mirror" };
        _thread.Start();
    }

    private void Run()
    {
        while (!_stopping)
        {
            _changed.WaitOne(IdleMilliseconds);

            lock (_gate)
            {
                foreach (nint fence in _fences)
                {
                    Refresh((XboxFenceObject*)fence, false);
                }
            }
        }
    }

    /// <summary>Copies the completed value into the cell; when it moved, waits for the next one on the shared event.</summary>
    private void Refresh(XboxFenceObject* fence, bool first)
    {
        nint inner = fence->Header.Inner;
        ulong value = ((delegate* unmanaged<nint, ulong>)ComObject.Slot(inner, GetCompletedValueSlot))(inner);

        if (!first && value == Volatile.Read(ref fence->CompletedValue))
        {
            return;
        }

        Volatile.Write(ref fence->CompletedValue, value);

        if (value != ulong.MaxValue)
        {
            nint handle = _changed.SafeWaitHandle.DangerousGetHandle();
            _ = ((delegate* unmanaged<nint, ulong, nint, int>)ComObject.Slot(inner, SetEventOnCompletionSlot))(inner, value + 1, handle);
        }
    }
}
