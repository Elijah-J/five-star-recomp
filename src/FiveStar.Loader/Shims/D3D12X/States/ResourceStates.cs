// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

namespace FiveStar.Loader.Shims.D3D12X.States;

/// <summary>
/// The legacy D3D12_RESOURCE_STATES a desktop resource rests in between command lists (its home state). The console
/// game creates render targets in other layouts (UNORDERED_ACCESS, SHADER_RESOURCE) and clears and draws to them
/// without a barrier, so a texture the GPU writes rests in the state of its first write flag: depth-stencil, then
/// render target, then unordered access. Every other resource rests in the state it was created in; a write state
/// without the matching flag (ALLOW_UNORDERED_ACCESS dropped for the format) becomes COMMON.
/// </summary>
internal static class ResourceStates
{
    /// <summary>D3D12_RESOURCE_STATES (d3d12.h).</summary>
    public const int Common = 0;
    public const int RenderTarget = 0x4;
    public const int UnorderedAccess = 0x8;
    public const int DepthWrite = 0x10;
    public const int DepthRead = 0x20;
    public const int NonPixelShaderResource = 0x40;
    public const int PixelShaderResource = 0x80;
    public const int CopyDest = 0x400;
    public const int CopySource = 0x800;
    public const int ResolveDest = 0x1000;
    public const int ResolveSource = 0x2000;
    public const int GenericRead = 0xAC3;

    /// <summary>D3D12_RESOURCE_DIMENSION_UNKNOWN and _BUFFER.</summary>
    public const int UnknownDimension = 0;
    public const int BufferDimension = 1;

    /// <summary>D3D12_RESOURCE_FLAG_ALLOW_RENDER_TARGET, _DEPTH_STENCIL and _UNORDERED_ACCESS.</summary>
    private const int AllowRenderTarget = 0x1;
    private const int AllowDepthStencil = 0x2;
    private const int AllowUnorderedAccess = 0x4;

    /// <summary>
    /// The home state of a desktop resource of <paramref name="dimension"/> and desktop <paramref name="flags"/> the
    /// game asked to create in <paramref name="created"/>.
    /// </summary>
    public static int Home(int dimension, int flags, int created)
    {
        if (dimension == BufferDimension)
        {
            return created;
        }

        if ((flags & AllowDepthStencil) != 0)
        {
            return DepthWrite;
        }

        if ((flags & AllowRenderTarget) != 0)
        {
            return RenderTarget;
        }

        if ((flags & AllowUnorderedAccess) != 0)
        {
            return UnorderedAccess;
        }

        if ((created & (RenderTarget | UnorderedAccess | DepthWrite | DepthRead)) != 0)
        {
            return Common;
        }

        return created;
    }

    /// <summary>Records the dimension and home state on a resource wrapper (null is ignored).</summary>
    public static unsafe void Record(XboxResourceObject* resource, int dimension, int home)
    {
        if (resource == null)
        {
            return;
        }

        resource->Dimension = dimension;
        resource->HomeState = home;
    }
}
