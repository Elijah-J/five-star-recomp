// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Runtime.InteropServices;

namespace FiveStar.Loader.Native;

/// <summary>The kernel32 functions the loader calls. Every signature is blittable; strings travel as pointers.</summary>
internal static unsafe class Kernel32
{
    private const string Library = "kernel32";

    [DllImport(Library, SetLastError = true)]
    public static extern nint VirtualAlloc(nint address, nuint size, uint allocationType, uint protect);

    [DllImport(Library, SetLastError = true)]
    public static extern int VirtualFree(nint address, nuint size, uint freeType);

    [DllImport(Library, SetLastError = true)]
    public static extern int VirtualProtect(nint address, nuint size, uint newProtect, uint* oldProtect);

    [DllImport(Library)]
    public static extern nuint VirtualQueryEx(nint process, nint address, MemoryBasicInformation* info, nuint length);

    [DllImport(Library)]
    public static extern int FlushInstructionCache(nint process, nint address, nuint size);

    [DllImport(Library)]
    public static extern nint GetCurrentProcess();

    [DllImport(Library, EntryPoint = "LoadLibraryExW", SetLastError = true)]
    public static extern nint LoadLibraryExW(char* name, nint file, uint flags);

    [DllImport(Library, EntryPoint = "LoadLibraryExA")]
    public static extern nint LoadLibraryExA(byte* name, nint file, uint flags);

    [DllImport(Library, SetLastError = true)]
    public static extern int FreeLibrary(nint module);

    /// <summary>GetProcAddress: <paramref name="name"/> is an ANSI name, or an ordinal in its low word.</summary>
    [DllImport(Library, SetLastError = true)]
    public static extern nint GetProcAddress(nint module, byte* name);

    [DllImport(Library)]
    public static extern nint GetModuleHandleW(char* name);

    [DllImport(Library)]
    public static extern nint GetModuleHandleA(byte* name);

    [DllImport(Library)]
    public static extern int GetModuleHandleExW(uint flags, char* name, nint* module);

    [DllImport(Library)]
    public static extern int GetModuleHandleExA(uint flags, byte* name, nint* module);

    [DllImport(Library)]
    public static extern uint GetModuleFileNameW(nint module, char* buffer, uint size);

    [DllImport(Library)]
    public static extern uint GetModuleFileNameA(nint module, byte* buffer, uint size);

    [DllImport(Library)]
    public static extern int WideCharToMultiByte(uint codePage, uint flags, char* source, int sourceLength, byte* destination, int destinationLength, nint defaultChar, nint usedDefaultChar);

    [DllImport(Library)]
    public static extern int K32GetProcessMemoryInfo(nint process, ProcessMemoryCountersEx* counters, uint size);

    [DllImport(Library)]
    public static extern uint TlsAlloc();

    [DllImport(Library)]
    public static extern int TlsFree(uint index);

    [DllImport(Library)]
    public static extern int TlsSetValue(uint index, nint value);
}
