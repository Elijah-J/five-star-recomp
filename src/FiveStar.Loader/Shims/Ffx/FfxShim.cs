// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Runtime.InteropServices;

namespace FiveStar.Loader.Shims.Ffx;

/// <summary>
/// amd_fidelityfx_loader_xbox.dll, the console FidelityFX API loader. Its upscaler provider is console-only (FSR 4
/// shaders cooked for the console GPU), so the exports bind to <see cref="FidelityFxBridge"/> instead of the shipped
/// DLL (docs/middleware.md).
/// </summary>
internal static unsafe class FfxShim
{
    [Shim(FfxLayout.Module, "ffxCreateContext")]
    [UnmanagedCallersOnly]
    private static uint FfxCreateContext(nint* context, byte* desc, nint allocator)
    {
        return ShimContext.Runtime.FidelityFx.CreateContext(context, desc, allocator);
    }

    [Shim(FfxLayout.Module, "ffxDestroyContext")]
    [UnmanagedCallersOnly]
    private static uint FfxDestroyContext(nint* context, nint allocator)
    {
        return ShimContext.Runtime.FidelityFx.DestroyContext(context, allocator);
    }

    [Shim(FfxLayout.Module, "ffxConfigure")]
    [UnmanagedCallersOnly]
    private static uint FfxConfigure(nint* context, byte* desc)
    {
        return ShimContext.Runtime.FidelityFx.Configure(context, desc);
    }

    [Shim(FfxLayout.Module, "ffxQuery")]
    [UnmanagedCallersOnly]
    private static uint FfxQuery(nint* context, byte* desc)
    {
        return ShimContext.Runtime.FidelityFx.Query(context, desc);
    }

    [Shim(FfxLayout.Module, "ffxDispatch")]
    [HotPath]
    [UnmanagedCallersOnly]
    private static uint FfxDispatch(nint* context, byte* desc)
    {
        return ShimContext.Runtime.FidelityFx.Dispatch(context, desc);
    }
}
