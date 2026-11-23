// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using FiveStar.Loader.Native;

namespace FiveStar.Loader.Shims;

/// <summary>
/// xg_xs.dll, the console texture layout library (XG). Both imports return HRESULTs:
/// <c>XGCreateTextureComputer(const XG_RESOURCE_DESC*, XGTextureAddressComputer** out)</c> and
/// <c>XGSuggestSwizzleMode(const XG_RESOURCE_DESC*, uint32_t, uint32_t* mode)</c> (the second from its call sites).
/// TODO: a texture address computer needs the console tiling (swizzle) layouts; until then both fail with E_NOTIMPL.
/// Two of the four XGCreateTextureComputer call sites test the result; the other two do not test it before using the
/// computer, so they fault on the null pointer when they run.
/// </summary>
[ShimModule(Module, UnresolvedReturn = HResult.NotImplemented)]
internal static class XgModule
{
    public const string Module = "xg_xs.dll";

    private const long NotImplemented = HResult.NotImplemented;

    [NotImplementedTable(Module)]
    private static readonly (string Name, long ReturnValue)[] Catalog =
    [
        ("XGCreateTextureComputer", NotImplemented), ("XGSuggestSwizzleMode", NotImplemented),
    ];
}
