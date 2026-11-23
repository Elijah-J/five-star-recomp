// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Runtime.InteropServices;
using FiveStar.Loader.Native;

namespace FiveStar.Loader.Shims.Storage;

/// <summary>The implemented slots of the <see cref="DStorageFactoryStub"/> vtable.</summary>
internal static class DStorageFactoryMethods
{
    /// <summary>The factory lives as long as the process, so the reference count is not kept.</summary>
    private const uint LiveReferences = 1;

    [UnmanagedCallersOnly]
    public static uint AddRef(nint self)
    {
        return LiveReferences;
    }

    [UnmanagedCallersOnly]
    public static uint Release(nint self)
    {
        return LiveReferences;
    }

    /// <summary>Takes (this, uint32 bytes); nothing is staged, so any size is accepted.</summary>
    [UnmanagedCallersOnly]
    public static int SetStagingBufferSize(nint self, uint bytes)
    {
        ShimContext.Runtime.Log.Detail($"  [xcrdapi] IDStorageFactoryX::SetStagingBufferSize(0x{bytes:x}) -> S_OK (no-op)");

        return HResult.Ok;
    }
}
