// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

namespace FiveStar.Loader.Native;

/// <summary>HRESULT values the shims return (winerror.h).</summary>
internal static class HResult
{
    public const int Ok = 0;
    public const int False = 1;
    public const int NotImplemented = unchecked((int)0x80004001);
    public const int Pointer = unchecked((int)0x80004003);
    public const int Abort = unchecked((int)0x80004004);
    public const int Fail = unchecked((int)0x80004005);
    public const int Pending = unchecked((int)0x8000000A);
    public const int IllegalMethodCall = unchecked((int)0x8000000E);
    public const int OutOfMemory = unchecked((int)0x8007000E);
    public const int InvalidArgument = unchecked((int)0x80070057);

    /// <summary>HRESULT_FROM_WIN32(ERROR_INSUFFICIENT_BUFFER).</summary>
    public const int NotSufficientBuffer = unchecked((int)0x8007007A);

    /// <summary>HRESULT_FROM_WIN32(ERROR_NOT_FOUND).</summary>
    public const int NotFound = unchecked((int)0x80070490);

    public const int ClassNotRegistered = unchecked((int)0x80040154);

    public static bool Failed(int hr)
    {
        return hr < 0;
    }

    public static string Format(int hr)
    {
        return $"0x{hr:x8}";
    }
}
