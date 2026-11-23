// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

namespace FiveStar.Loader.Shims.D3D12X.States;

/// <summary>
/// The enhanced-barrier scope of a legacy resource state (d3d12.h, the layouts the legacy states correspond to). A
/// state with no single layout (a mix of read states other than GENERIC_READ) has none; such a transition stays a
/// legacy barrier.
/// </summary>
internal static class EnhancedBarrierScopes
{
    /// <summary>D3D12_BARRIER_SYNC values.</summary>
    private const int SyncAll = 0x1;
    private const int SyncDepthStencil = 0x20;
    private const int SyncRenderTarget = 0x40;
    private const int SyncCopy = 0x200;
    private const int SyncResolve = 0x400;
    private const int SyncAllShading = 0x1000;

    /// <summary>D3D12_BARRIER_ACCESS values; GENERIC_READ is vertex, constant and index buffer, shader resource, indirect argument and copy source.</summary>
    private const int AccessCommon = 0;
    private const int AccessRenderTarget = 0x8;
    private const int AccessUnorderedAccess = 0x10;
    private const int AccessDepthWrite = 0x20;
    private const int AccessDepthRead = 0x40;
    private const int AccessShaderResource = 0x80;
    private const int AccessCopyDest = 0x400;
    private const int AccessCopySource = 0x800;
    private const int AccessResolveDest = 0x1000;
    private const int AccessResolveSource = 0x2000;
    private const int AccessGenericRead = 0xA87;

    /// <summary>D3D12_BARRIER_LAYOUT values.</summary>
    private const int LayoutCommon = 0;
    private const int LayoutGenericRead = 1;
    private const int LayoutRenderTarget = 2;
    private const int LayoutUnorderedAccess = 3;
    private const int LayoutDepthWrite = 4;
    private const int LayoutDepthRead = 5;
    private const int LayoutShaderResource = 6;
    private const int LayoutCopySource = 7;
    private const int LayoutCopyDest = 8;
    private const int LayoutResolveSource = 9;
    private const int LayoutResolveDest = 10;

    private const int ShaderResourceStates = ResourceStates.NonPixelShaderResource | ResourceStates.PixelShaderResource;

    [HotPath]
    public static bool TryMap(int state, out BarrierScope scope)
    {
        if (state != ResourceStates.Common && (state & ~ShaderResourceStates) == 0)
        {
            scope = new BarrierScope(SyncAllShading, AccessShaderResource, LayoutShaderResource);

            return true;
        }

        scope = state switch
        {
            ResourceStates.Common => new BarrierScope(SyncAll, AccessCommon, LayoutCommon),
            ResourceStates.RenderTarget => new BarrierScope(SyncRenderTarget, AccessRenderTarget, LayoutRenderTarget),
            ResourceStates.UnorderedAccess => new BarrierScope(SyncAllShading, AccessUnorderedAccess, LayoutUnorderedAccess),
            ResourceStates.DepthWrite => new BarrierScope(SyncDepthStencil, AccessDepthWrite, LayoutDepthWrite),
            ResourceStates.DepthRead => new BarrierScope(SyncDepthStencil, AccessDepthRead, LayoutDepthRead),
            ResourceStates.CopyDest => new BarrierScope(SyncCopy, AccessCopyDest, LayoutCopyDest),
            ResourceStates.CopySource => new BarrierScope(SyncCopy, AccessCopySource, LayoutCopySource),
            ResourceStates.ResolveDest => new BarrierScope(SyncResolve, AccessResolveDest, LayoutResolveDest),
            ResourceStates.ResolveSource => new BarrierScope(SyncResolve, AccessResolveSource, LayoutResolveSource),
            ResourceStates.GenericRead => new BarrierScope(SyncAll, AccessGenericRead, LayoutGenericRead),
            _ => default,
        };

        return scope != default;
    }
}
