// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using FiveStar.Loader.Native;

namespace FiveStar.Loader.Shims.D3D12X.Pipelines;

/// <summary>
/// Keeps the warm-up within the machine's memory: the driver's compile of one translated program can take tens of
/// gigabytes, so no build starts while less than <see cref="ReserveBytes"/> of commit is available, and a pipeline of
/// <see cref="PipelineFactory.HeavyShaderBytes"/> or more starts only with <see cref="HeavyBytes"/> available.
/// </summary>
internal static unsafe class WarmMemoryGuard
{
    /// <summary>Commit left to the rest of the machine while the warm-up builds.</summary>
    public const ulong ReserveBytes = 8UL << 30;

    /// <summary>
    /// Commit a heavy pipeline needs to start: one 10 MB ray-traced shadow program passed 30 GB in the NVIDIA driver
    /// (docs/d3d12x-pipelines.md, Warm-up).
    /// </summary>
    public const ulong HeavyBytes = 32UL << 30;

    /// <summary>Physical memory left to the game while pipelines build during a run.</summary>
    public const ulong PhysicalReserveBytes = 4UL << 30;

    private const int PauseMilliseconds = 2000;

    /// <summary>Waits until <see cref="ReserveBytes"/> of commit is available or <paramref name="stopping"/> says so.</summary>
    public static void WaitForRoom(Func<bool> stopping)
    {
        while (AvailableCommit() < ReserveBytes && !stopping())
        {
            Thread.Sleep(PauseMilliseconds);
        }
    }

    /// <summary>Whether a heavy pipeline may start now.</summary>
    public static bool RoomForHeavy()
    {
        return AvailableCommit() >= HeavyBytes + ReserveBytes;
    }

    /// <summary>
    /// Whether a pipeline may start while the game runs: <see cref="ReserveBytes"/> of commit and
    /// <see cref="PhysicalReserveBytes"/> of physical memory available, so the compiles do not page the game out.
    /// </summary>
    public static bool RoomForBuild()
    {
        var status = Status();

        return status.AvailablePageFile >= ReserveBytes && status.AvailablePhysical >= PhysicalReserveBytes;
    }

    /// <summary>Whether a heavy pipeline may start while the game runs.</summary>
    public static bool RoomForHeavyBuild()
    {
        return RoomForBuild() && RoomForHeavy();
    }

    private static ulong AvailableCommit()
    {
        return Status().AvailablePageFile;
    }

    /// <summary>The machine's memory; all available when it cannot be read.</summary>
    private static MemoryStatusEx Status()
    {
        MemoryStatusEx status = default;
        status.Length = (uint)sizeof(MemoryStatusEx);

        if (KernelBase.GlobalMemoryStatusEx(&status) == 0)
        {
            return new MemoryStatusEx { AvailablePageFile = ulong.MaxValue, AvailablePhysical = ulong.MaxValue };
        }

        return status;
    }
}
