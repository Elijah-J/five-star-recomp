// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Runtime.InteropServices;

namespace FiveStar.Loader.Shims.D3D12X;

/// <summary>
/// The device object the game receives (our own layout; the game sees only the vtable pointer): the D3D12.X vtable
/// and the desktop ID3D12Device the forwarding slots call, at <see cref="D3D12XLayout.InnerOffset"/>.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct XboxDeviceObject
{
    public nint Vtable;
    public nint Inner;
}
