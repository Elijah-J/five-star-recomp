// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using FiveStar.Loader.Native;
using FiveStar.Loader.Shims.D3D12X.Pipelines;

namespace FiveStar.Loader.Shims.D3D12X;

/// <summary>
/// Resource descs as the desktop takes them: <see cref="ResourceDescription.ToDesktop"/>, console depth formats in their
/// desktop family (<see cref="DepthFormats.Resource"/>), and ALLOW_UNORDERED_ACCESS
/// dropped from a texture whose format has no typed UAV on this GPU (the console sets it on formats such as
/// B5G5R5A1_UNORM, which the desktop then rejects). Format support is asked once per format; a format the runtime
/// refuses anyway is recorded through <see cref="RefuseUav"/>.
/// </summary>
internal sealed unsafe class DesktopResourceDescriptions
{
    /// <summary>ID3D12Device::CheckFeatureSupport and D3D12_FEATURE_FORMAT_SUPPORT (d3d12.h).</summary>
    private const int CheckFeatureSupportSlot = 13;
    private const int FormatSupportFeature = 2;
    private const uint FormatSupportSize = 12;
    private const int Support1Dword = 1;
    private const int Support2Dword = 2;

    /// <summary>D3D12_FORMAT_SUPPORT1_TYPED_UNORDERED_ACCESS_VIEW and D3D12_RESOURCE_FLAG_ALLOW_UNORDERED_ACCESS.</summary>
    private const uint TypedUnorderedAccess = 0x40000000;

    /// <summary>D3D12_FORMAT_SUPPORT2_UAV_TYPED_STORE: the runtime also wants stores (B5G5R5A1_UNORM reports the view only).</summary>
    private const uint TypedStore = 0x80;
    private const int AllowUnorderedAccess = 0x4;

    /// <summary>DXGI formats the cache covers; the desktop's end below 256.</summary>
    private const int FormatCount = 256;

    private const byte Unknown = 0;
    private const byte Supported = 1;
    private const byte Unsupported = 2;

    private readonly byte[] _uavSupport = new byte[FormatCount];

    /// <summary>The desktop form of <paramref name="description"/> for <paramref name="device"/>.</summary>
    public ResourceDescription ToDesktop(nint device, ResourceDescription* description)
    {
        var desktop = description->ToDesktop();
        desktop.Format = DepthFormats.Resource(desktop.Format);

        if (desktop.Dimension != ResourceDescription.BufferDimension && (desktop.Flags & AllowUnorderedAccess) != 0 && !HasTypedUav(device, desktop.Format))
        {
            desktop.Flags &= ~AllowUnorderedAccess;
        }

        return desktop;
    }

    /// <summary>
    /// Records that the runtime refused a UAV texture of <paramref name="description"/>'s format although the driver
    /// reports typed UAV support (B5G5R5A1_UNORM); true when that changes the desktop desc.
    /// </summary>
    public bool RefuseUav(ResourceDescription* description)
    {
        int format = description->Format;
        bool texture = description->Dimension != ResourceDescription.BufferDimension && (description->Flags & AllowUnorderedAccess) != 0;

        if (!texture || (uint)format >= FormatCount || Volatile.Read(ref _uavSupport[format]) == Unsupported)
        {
            return false;
        }

        Volatile.Write(ref _uavSupport[format], Unsupported);

        return true;
    }

    private bool HasTypedUav(nint device, int format)
    {
        if ((uint)format >= FormatCount)
        {
            return true;
        }

        byte known = Volatile.Read(ref _uavSupport[format]);

        if (known != Unknown)
        {
            return known == Supported;
        }

        // D3D12_FEATURE_DATA_FORMAT_SUPPORT: Format, Support1, Support2. A typeless format fails the query and keeps the flag.
        uint* data = stackalloc uint[] { (uint)format, 0, 0 };
        var check = (delegate* unmanaged<nint, int, void*, uint, int>)ComObject.Slot(device, CheckFeatureSupportSlot);
        int hr = check(device, FormatSupportFeature, data, FormatSupportSize);
        bool supported = HResult.Failed(hr) || ((data[Support1Dword] & TypedUnorderedAccess) != 0 && (data[Support2Dword] & TypedStore) != 0);
        byte state = Unsupported;

        if (supported)
        {
            state = Supported;
        }

        Volatile.Write(ref _uavSupport[format], state);

        return supported;
    }
}
