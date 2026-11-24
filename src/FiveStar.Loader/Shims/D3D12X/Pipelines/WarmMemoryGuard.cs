// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using FiveStar.Loader.Native;

namespace FiveStar.Loader.Shims.D3D12X.Pipelines;

/// <summary>
/// Keeps pipeline builds within the machine's memory: the driver's compile of one translated program can take tens of
/// gigabytes, so no build (warm-up or game) starts while less than <see cref="ReserveBytes"/> of commit is available,
/// and in the warm-up a pipeline of <see cref="PipelineFactory.HeavyShaderBytes"/> or more starts only with
/// <see cref="HeavyBytes"/> available.
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

    private static ulong AvailableCommit()
    {
        MemoryStatusEx status = default;
        status.Length = (uint)sizeof(MemoryStatusEx);

        if (KernelBase.GlobalMemoryStatusEx(&status) == 0)
        {
            return ulong.MaxValue;
        }

        return status.AvailablePageFile;
    }
}
