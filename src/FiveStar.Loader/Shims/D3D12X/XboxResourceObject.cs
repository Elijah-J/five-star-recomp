// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Runtime.InteropServices;

namespace FiveStar.Loader.Shims.D3D12X;

/// <summary>
/// A resource wrapper: <see cref="XboxObject"/> plus the console GPU address the game chose for it (device slot 133),
/// which GetGPUVirtualAddress returns instead of the desktop one; 0 for resources the desktop placed.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct XboxResourceObject
{
    public XboxObject Header;
    public ulong GameAddress;
}
