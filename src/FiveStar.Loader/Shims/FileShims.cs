// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Runtime.InteropServices;
using FiveStar.Loader.Native;

namespace FiveStar.Loader.Shims;

/// <summary>
/// The file-open functions of kernel32 and the CRT, bound for every importing module ("*"). Each asks the
/// <see cref="Mods.FileOverlay"/> for a mod file and opens that instead, else opens the path as given. A redirected
/// ANSI call opens the mod file through the wide function. The kernel32 shims pass the system's last error through.
/// </summary>
internal static unsafe class FileShims
{
    [Shim("*", "CreateFileW", SetsLastError = true)]
    [UnmanagedCallersOnly]
    private static nint CreateFileW(char* name, uint access, uint share, nint security, uint disposition, uint flags, nint template)
    {
        var runtime = ShimContext.Runtime;
        string? target = runtime.Overlay.Redirect(name, nameof(CreateFileW));
        nint handle;

        fixed (char* redirected = target)
        {
            handle = Kernel32.CreateFileW(Choose(name, redirected), access, share, security, disposition, flags, template);
        }

        runtime.LastError.Set(Marshal.GetLastPInvokeError());

        return handle;
    }

    [Shim("*", "CreateFileA", SetsLastError = true)]
    [UnmanagedCallersOnly]
    private static nint CreateFileA(byte* name, uint access, uint share, nint security, uint disposition, uint flags, nint template)
    {
        var runtime = ShimContext.Runtime;
        string? target = runtime.Overlay.Redirect(name, nameof(CreateFileA));
        nint handle;

        if (target is null)
        {
            handle = Kernel32.CreateFileA(name, access, share, security, disposition, flags, template);
        }
        else
        {
            fixed (char* redirected = target)
            {
                handle = Kernel32.CreateFileW(redirected, access, share, security, disposition, flags, template);
            }
        }

        runtime.LastError.Set(Marshal.GetLastPInvokeError());

        return handle;
    }

    [Shim("*", "CreateFile2", SetsLastError = true)]
    [UnmanagedCallersOnly]
    private static nint CreateFile2(char* name, uint access, uint share, uint disposition, nint parameters)
    {
        var runtime = ShimContext.Runtime;
        string? target = runtime.Overlay.Redirect(name, nameof(CreateFile2));
        nint handle;

        fixed (char* redirected = target)
        {
            handle = Kernel32.CreateFile2(Choose(name, redirected), access, share, disposition, parameters);
        }

        runtime.LastError.Set(Marshal.GetLastPInvokeError());

        return handle;
    }

    [Shim("*", "GetFileAttributesW", SetsLastError = true)]
    [UnmanagedCallersOnly]
    private static uint GetFileAttributesW(char* name)
    {
        var runtime = ShimContext.Runtime;
        string? target = runtime.Overlay.Redirect(name, nameof(GetFileAttributesW));
        uint attributes;

        fixed (char* redirected = target)
        {
            attributes = Kernel32.GetFileAttributesW(Choose(name, redirected));
        }

        runtime.LastError.Set(Marshal.GetLastPInvokeError());

        return attributes;
    }

    [Shim("*", "GetFileAttributesExW", SetsLastError = true)]
    [UnmanagedCallersOnly]
    private static int GetFileAttributesExW(char* name, int level, void* information)
    {
        var runtime = ShimContext.Runtime;
        string? target = runtime.Overlay.Redirect(name, nameof(GetFileAttributesExW));
        int result;

        fixed (char* redirected = target)
        {
            result = Kernel32.GetFileAttributesExW(Choose(name, redirected), level, information);
        }

        runtime.LastError.Set(Marshal.GetLastPInvokeError());

        return result;
    }

    [Shim("*", "_wfopen")]
    [UnmanagedCallersOnly]
    private static nint WFOpen(char* name, char* mode)
    {
        string? target = ShimContext.Runtime.Overlay.Redirect(name, "_wfopen");

        fixed (char* redirected = target)
        {
            return Ucrt.WFOpen(Choose(name, redirected), mode);
        }
    }

    [Shim("*", "_wfsopen")]
    [UnmanagedCallersOnly]
    private static nint WFsOpen(char* name, char* mode, int share)
    {
        string? target = ShimContext.Runtime.Overlay.Redirect(name, "_wfsopen");

        fixed (char* redirected = target)
        {
            return Ucrt.WFsOpen(Choose(name, redirected), mode, share);
        }
    }

    [Shim("*", "_wopen")]
    [UnmanagedCallersOnly]
    private static int WOpen(char* name, int flags, int mode)
    {
        string? target = ShimContext.Runtime.Overlay.Redirect(name, "_wopen");

        fixed (char* redirected = target)
        {
            return Ucrt.WOpen(Choose(name, redirected), flags, mode);
        }
    }

    [Shim("*", "fopen")]
    [UnmanagedCallersOnly]
    private static nint FOpen(byte* name, byte* mode)
    {
        string? target = ShimContext.Runtime.Overlay.Redirect(name, "fopen");

        if (target is null)
        {
            return Ucrt.FOpen(name, mode);
        }

        fixed (char* redirected = target, wideMode = Marshal.PtrToStringAnsi((nint)mode))
        {
            return Ucrt.WFOpen(redirected, wideMode);
        }
    }

    [Shim("*", "_fsopen")]
    [UnmanagedCallersOnly]
    private static nint FsOpen(byte* name, byte* mode, int share)
    {
        string? target = ShimContext.Runtime.Overlay.Redirect(name, "_fsopen");

        if (target is null)
        {
            return Ucrt.FsOpen(name, mode, share);
        }

        fixed (char* redirected = target, wideMode = Marshal.PtrToStringAnsi((nint)mode))
        {
            return Ucrt.WFsOpen(redirected, wideMode, share);
        }
    }

    /// <summary>_open is variadic; the third argument (the permission mode) is read only with _O_CREAT.</summary>
    [Shim("*", "_open")]
    [UnmanagedCallersOnly]
    private static int Open(byte* name, int flags, int mode)
    {
        string? target = ShimContext.Runtime.Overlay.Redirect(name, "_open");

        if (target is null)
        {
            return Ucrt.Open(name, flags, mode);
        }

        fixed (char* redirected = target)
        {
            return Ucrt.WOpen(redirected, flags, mode);
        }
    }

    /// <summary>The redirected path when there is one, else the caller's.</summary>
    private static char* Choose(char* original, char* redirected)
    {
        if (redirected == null)
        {
            return original;
        }

        return redirected;
    }
}
