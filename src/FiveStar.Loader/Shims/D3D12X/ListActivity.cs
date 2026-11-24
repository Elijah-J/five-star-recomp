// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Runtime.InteropServices;

namespace FiveStar.Loader.Shims.D3D12X;

/// <summary>
/// What a command list recorded since its last Reset, kept in its wrapper for <see cref="QueueProgress"/>: the last
/// desktop pipeline it set and how many draws, dispatches and indirect executions it forwarded.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct ListActivity
{
    /// <summary>Offset in the command-list wrapper, after <see cref="ListSubmission"/> (0xE00 to 0xE10).</summary>
    public const int Offset = 0xE40;

    public nint Pipeline;
    public int Draws;
    public int Dispatches;
    public int Indirect;

    /// <summary>With <c>--d3d12-breadcrumbs</c> (<see cref="DebugLayer.GpuBreadcrumbs"/>): the recording's number and its operations so far.</summary>
    public uint Serial;
    public int Operations;

    /// <summary>The list's marker slot (+1) and the pipeline of each operation; kept over Reset.</summary>
    public int Slot;
    public nint* Pipelines;

    public static ListActivity* Of(XboxObject* list)
    {
        return (ListActivity*)((byte*)list + Offset);
    }

    /// <summary>A new recording: the counts start over, the marker slot and pipeline table stay.</summary>
    [HotPath]
    public static void Reset(XboxObject* list)
    {
        var activity = Of(list);

        activity->Pipeline = 0;
        activity->Draws = 0;
        activity->Dispatches = 0;
        activity->Indirect = 0;
        activity->Operations = 0;
        activity->Serial = DebugLayer.GpuBreadcrumbs.NextSerial();
    }
}
