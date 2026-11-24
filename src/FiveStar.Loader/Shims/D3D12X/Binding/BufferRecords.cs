// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using FiveStar.Loader.Shims.D3D12X.Resources;
using FiveStar.Loader.Shims.Xg;

namespace FiveStar.Loader.Shims.D3D12X.Binding;

/// <summary>
/// The element stride a buffer view's V# carries in dword 1, which translated programs multiply an indexed access
/// (idxen) by (docs/d3d12x-shim.md, Descriptor window). A constant buffer is indexed in 16-byte rows (the Scaleform
/// batch programs read row batch * 4 + k), a structured view by its structure, a typed view by its format's element;
/// a raw view has none.
/// </summary>
internal static unsafe class BufferRecordStrides
{
    /// <summary>Bytes of a constant-buffer row (a float4).</summary>
    public const uint ConstantBuffer = 16;

    /// <summary>The stride of a D3D12_SHADER_RESOURCE_VIEW_DESC; 0 for a texture view or no desc.</summary>
    [HotPath]
    public static uint ShaderResource(void* description)
    {
        var view = (BufferViewDescription*)description;

        if (view == null || view->Dimension != BufferViewDescription.BufferDimension)
        {
            return 0;
        }

        return Element(view->Format, view->SrvStride, view->SrvFlags);
    }

    /// <summary>The stride of a D3D12_UNORDERED_ACCESS_VIEW_DESC; 0 for a texture view or no desc.</summary>
    [HotPath]
    public static uint UnorderedAccess(void* description)
    {
        var view = (BufferViewDescription*)description;

        if (view == null || view->Dimension != BufferViewDescription.BufferDimension)
        {
            return 0;
        }

        return Element(view->Format, view->UavStride, view->UavFlags);
    }

    [HotPath]
    private static uint Element(int format, uint structure, int flags)
    {
        if ((flags & BufferViewDescription.RawFlag) != 0)
        {
            return 0;
        }

        if (structure != 0)
        {
            return structure;
        }

        if (format == ViewFormats.Unknown)
        {
            return 0;
        }

        return (uint)XgFormats.Find((uint)format).BytesPerElement;
    }
}
