// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Runtime.InteropServices;
using FiveStar.Loader.Native;
using FiveStar.Loader.Shims.D3D12X.DebugLayer;

namespace FiveStar.Loader.Shims.D3D12X;

/// <summary>Command-queue slots that take an array of wrapped lists: per-frame, so allocation-free.</summary>
internal static unsafe class XboxQueueMethods
{
    /// <summary>X slot 35, read as ExecuteCommandListsX: (count, lists, frame object, flags 0 or 3).</summary>
    public const int ExecuteCommandListsXSlot = 35;

    /// <summary>X slot 12: desktop ExecuteCommandLists two slots later, like the queue's other desktop methods.</summary>
    public const int ExecuteCommandListsSlot = 12;

    /// <summary>ID3D12CommandQueue::ExecuteCommandLists in the desktop vtable (d3d12.h).</summary>
    private const int DesktopExecuteSlot = 10;

    /// <summary>Lists unwrapped per desktop call; the game submits at most 256 at once (0x142481e90).</summary>
    private const int Batch = 256;

    /// <summary>
    /// Slot 35. The frame object and the flags have no desktop counterpart: the lists are submitted in order and the
    /// call succeeds.
    /// </summary>
    [HotPath]
    [UnmanagedCallersOnly]
    public static int ExecuteCommandListsX(XboxObject* self, uint count, nint* lists, nint frame, uint flags)
    {
        if (QueueTrace.Enabled)
        {
            QueueTrace.Execute(self, count, frame, flags);
        }

        Execute(self, count, lists);

        return HResult.Ok;
    }

    /// <summary>Slot 12, desktop ExecuteCommandLists(count, lists).</summary>
    [HotPath]
    [UnmanagedCallersOnly]
    public static void ExecuteCommandLists(XboxObject* self, uint count, nint* lists)
    {
        if (QueueTrace.Enabled)
        {
            QueueTrace.Execute(self, count, 0, 0);
        }

        Execute(self, count, lists);
    }

    [HotPath]
    private static void Execute(XboxObject* self, uint count, nint* lists)
    {
        if (lists == null)
        {
            return;
        }

        var d3d12 = ShimContext.Runtime.D3D12X;
        var objects = d3d12.Objects;
        d3d12.Textures.RefreshWatched(objects.DesktopDevice, d3d12.Presenter.Presents);
        nint inner = self->Inner;
        var execute = (delegate* unmanaged<nint, uint, nint*, void>)ComObject.Slot(inner, DesktopExecuteSlot);
        nint* desktop = stackalloc nint[Batch];

        for (uint first = 0; first < count; first += Batch)
        {
            uint size = count - first;

            if (size > Batch)
            {
                size = Batch;
            }

            for (uint i = 0; i < size; i++)
            {
                nint list = lists[first + i];
                desktop[i] = objects.Unwrap(list);

                if (desktop[i] != list)
                {
                    ListSubmissions.WaitIfRunning(list);
                }
            }

            RetireWaits.BeforeExecute((XboxQueueObject*)self, lists + first, size);
            execute(inner, size, desktop);
            ListSubmissions.Submitted((XboxQueueObject*)self, lists + first, size, objects);
        }
    }
}
