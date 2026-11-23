// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Runtime.InteropServices;

namespace FiveStar.Loader.Native;

/// <summary>The kernelbase functions the loader calls.</summary>
internal static unsafe class KernelBase
{
    private const string Library = "kernelbase";

    /// <summary>BOOL IsApiSetImplemented(PCSTR contract) (apiquery2.h, Windows 10+): whether this OS answers the API set.</summary>
    [DllImport(Library)]
    public static extern int IsApiSetImplemented(byte* contract);
}
