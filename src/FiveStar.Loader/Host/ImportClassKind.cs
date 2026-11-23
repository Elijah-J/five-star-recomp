// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

namespace FiveStar.Loader.Host;

/// <summary>How the loader would resolve an import, as seen from the shim table alone (no images mapped).</summary>
internal enum ImportClassKind
{
    /// <summary>A shim of the loader answers the function (a forward to a PC DLL is one of these).</summary>
    Shim,

    /// <summary>An Xbox-only module with no shim for the function: a logged stub returning a fixed value.</summary>
    XboxStub,

    /// <summary>No shim and not an Xbox-only module: the PC host (System32) would answer, or nothing would.</summary>
    Host,
}
