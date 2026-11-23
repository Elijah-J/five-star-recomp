// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using FiveStar.Loader.Shims.D3D12X;

namespace FiveStar.Loader.Shims.Ffx;

/// <summary>
/// The resolver for the wrappers the D3D12.X shim hands out today: devices and command lists are
/// <see cref="XboxDeviceObject"/> (vtable, then the desktop object); resources are desktop resources. Replace it when
/// the shim wraps resources or changes the layout.
/// </summary>
internal sealed unsafe class WrapperObjectResolver : IDesktopObjectResolver
{
    public nint Device(nint xboxDevice)
    {
        return Inner(xboxDevice);
    }

    public nint CommandList(nint xboxCommandList)
    {
        return Inner(xboxCommandList);
    }

    public nint Resource(nint xboxResource)
    {
        return xboxResource;
    }

    private static nint Inner(nint wrapper)
    {
        if (wrapper == 0)
        {
            return 0;
        }

        return ((XboxDeviceObject*)wrapper)->Inner;
    }
}
