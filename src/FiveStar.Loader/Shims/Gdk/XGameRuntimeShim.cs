// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Runtime.InteropServices;
using FiveStar.Loader.Native;

namespace FiveStar.Loader.Shims.Gdk;

/// <summary>
/// The XGameRuntime module and its initialization entry points (XGameRuntime.h), plus the thunk-library ABI that
/// libHttpClient.GDK.dll resolves with GetProcAddress: InitializeApiImpl, InitializeApiImplEx, InitializeApiImplEx2,
/// QueryApiImpl, UninitializeApiImpl and XErrorReport. Those signatures are inferred from the call sites (the
/// registers set before each indirect call); none is in a public header. Unshimmed X* functions of the module and of
/// the gaming API sets return E_NOTIMPL.
/// </summary>
[ShimModule(GdkModule.Name, UnresolvedReturn = HResult.NotImplemented, AliasPattern = GdkModule.AliasPattern, AliasSymbolPrefix = GdkModule.AliasSymbolPrefix)]
internal static unsafe partial class XGameRuntimeShim
{
    [Shim(GdkModule.Name, "XGameRuntimeInitialize")]
    [UnmanagedCallersOnly]
    private static int XGameRuntimeInitialize()
    {
        return ShimContext.Runtime.Gdk.Runtime.Initialize("XGameRuntimeInitialize");
    }

    /// <summary>HRESULT XGameRuntimeInitializeWithOptions(const XGameRuntimeOptions* options); newer GDKs; the options are not read.</summary>
    [Shim(GdkModule.Name, "XGameRuntimeInitializeWithOptions")]
    [UnmanagedCallersOnly]
    private static int XGameRuntimeInitializeWithOptions(nint options)
    {
        return ShimContext.Runtime.Gdk.Runtime.Initialize($"XGameRuntimeInitializeWithOptions(0x{options:x})");
    }

    [Shim(GdkModule.Name, "XGameRuntimeUninitialize")]
    [UnmanagedCallersOnly]
    private static void XGameRuntimeUninitialize()
    {
        ShimContext.Runtime.Gdk.Runtime.Uninitialize("XGameRuntimeUninitialize");
    }

    /// <summary>bool XGameRuntimeIsFeatureAvailable(XGameRuntimeFeature feature).</summary>
    [Shim(GdkModule.Name, "XGameRuntimeIsFeatureAvailable")]
    [UnmanagedCallersOnly]
    private static byte XGameRuntimeIsFeatureAvailable(uint feature)
    {
        if (ShimContext.Runtime.Gdk.Runtime.IsFeatureAvailable(feature))
        {
            return GdkModule.True;
        }

        return GdkModule.False;
    }

    /// <summary>InitializeApiImpl(ecx, edx): two version words (inferred).</summary>
    [Shim(GdkModule.Name, "InitializeApiImpl")]
    [UnmanagedCallersOnly]
    private static int InitializeApiImpl(ulong first, ulong second)
    {
        return ShimContext.Runtime.Gdk.Runtime.InitializeApi($"InitializeApiImpl(0x{first:x}, 0x{second:x})");
    }

    /// <summary>InitializeApiImplEx(rcx, rdx, r8 = flags) (inferred).</summary>
    [Shim(GdkModule.Name, "InitializeApiImplEx")]
    [UnmanagedCallersOnly]
    private static int InitializeApiImplEx(ulong first, ulong second, ulong flags)
    {
        return ShimContext.Runtime.Gdk.Runtime.InitializeApi($"InitializeApiImplEx(0x{first:x}, 0x{second:x}, flags 0x{flags:x})");
    }

    /// <summary>InitializeApiImplEx2(rcx, rdx, r8 = flags, r9 = the XGameRuntimeInitializeWithOptions options pointer) (inferred).</summary>
    [Shim(GdkModule.Name, "InitializeApiImplEx2")]
    [UnmanagedCallersOnly]
    private static int InitializeApiImplEx2(ulong first, ulong second, ulong flags, nint options)
    {
        return ShimContext.Runtime.Gdk.Runtime.InitializeApi($"InitializeApiImplEx2(0x{first:x}, 0x{second:x}, flags 0x{flags:x}, options 0x{options:x})");
    }

    [Shim(GdkModule.Name, "UninitializeApiImpl")]
    [UnmanagedCallersOnly]
    private static void UninitializeApiImpl()
    {
        ShimContext.Runtime.Gdk.Runtime.Uninitialize("UninitializeApiImpl");
    }

    /// <summary>XErrorReport(ecx = HRESULT, rdx = const char* message) (inferred).</summary>
    [Shim(GdkModule.Name, "XErrorReport")]
    [UnmanagedCallersOnly]
    private static void XErrorReport(int hr, byte* message)
    {
        ShimContext.Runtime.Gdk.Runtime.ReportError(hr, message);
    }

    /// <summary>HRESULT QueryApiImpl(const GUID* clsid, const GUID* iid, void** object) (inferred).</summary>
    [Shim(GdkModule.Name, "QueryApiImpl")]
    [UnmanagedCallersOnly]
    private static int QueryApiImpl(Guid* clsid, Guid* iid, nint* result)
    {
        var gdk = ShimContext.Runtime.Gdk;

        try
        {
            return gdk.Runtime.Query(clsid, iid, result);
        }
        catch (Exception e)
        {
            return gdk.Boundary.Failed(nameof(QueryApiImpl), e);
        }
    }
}
