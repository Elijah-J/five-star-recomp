// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Runtime.InteropServices;
using FiveStar.Loader.Native;

namespace FiveStar.Loader.Shims.D3D12X.DebugLayer;

/// <summary>
/// Command-list slot 50 (OMSetRenderTargets, desktop 46) while <c>--d3d12-trace-draws</c> is on: the targets go to
/// <see cref="DrawTrace"/>, then to the desktop list unchanged (render-target handles are desktop handles).
/// </summary>
internal static unsafe class DrawTraceSlots
{
    public const int SetRenderTargetsSlot = 50;

    /// <summary>ID3D12GraphicsCommandList::OMSetRenderTargets (d3d12.h).</summary>
    private const int DesktopSetRenderTargets = 46;

    public static void Register(XboxObjects objects)
    {
        delegate* unmanaged<XboxObject*, uint, nint*, int, nint*, void> targets = &SetRenderTargets;
        objects.SetHandler(XboxInterfaceKind.CommandList, SetRenderTargetsSlot, (nint)targets);
        delegate* unmanaged<XboxObject*, uint, float*, void> viewports = &SetViewports;
        delegate* unmanaged<XboxObject*, uint, int*, void> scissors = &SetScissors;
        objects.SetHandler(XboxInterfaceKind.CommandList, SetViewportsSlot, (nint)viewports);
        objects.SetHandler(XboxInterfaceKind.CommandList, SetScissorsSlot, (nint)scissors);
    }

    /// <summary>List slots 21 and 22, RSSetViewports and RSSetScissorRects (desktop 21 and 22).</summary>
    public const int SetViewportsSlot = 21;
    public const int SetScissorsSlot = 22;

    [UnmanagedCallersOnly]
    private static void SetViewports(XboxObject* self, uint count, float* viewports)
    {
        ShimContext.Runtime.D3D12X.DrawTrace?.Viewports(self, count, viewports);
        nint inner = self->Inner;
        ((delegate* unmanaged<nint, uint, float*, void>)ComObject.Slot(inner, SetViewportsSlot))(inner, count, viewports);
    }

    [UnmanagedCallersOnly]
    private static void SetScissors(XboxObject* self, uint count, int* rects)
    {
        ShimContext.Runtime.D3D12X.DrawTrace?.Scissors(self, count, rects);
        nint inner = self->Inner;
        ((delegate* unmanaged<nint, uint, int*, void>)ComObject.Slot(inner, SetScissorsSlot))(inner, count, rects);
    }

    [UnmanagedCallersOnly]
    private static void SetRenderTargets(XboxObject* self, uint count, nint* handles, int single, nint* depthStencil)
    {
        nint depth = 0;

        if (depthStencil != null)
        {
            depth = *depthStencil;
        }

        ShimContext.Runtime.D3D12X.DrawTrace?.RenderTargets(self, count, handles, single != 0, depth);
        nint inner = self->Inner;
        ((delegate* unmanaged<nint, uint, nint*, int, nint*, void>)ComObject.Slot(inner, DesktopSetRenderTargets))(inner, count, handles, single, depthStencil);
    }
}
