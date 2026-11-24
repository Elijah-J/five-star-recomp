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

    public static ListActivity* Of(XboxObject* list)
    {
        return (ListActivity*)((byte*)list + Offset);
    }
}
