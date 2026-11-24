// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Diagnostics;
using System.Runtime.InteropServices;
using FiveStar.Loader.Native;

namespace FiveStar.Loader.Shims.D3D12X;

/// <summary>
/// Command-list slots 71, 72 and 74 (X-only): values the GPU writes to game memory once the list's earlier work is done.
/// Each becomes desktop WriteBufferImmediate in MARKER_OUT mode at the desktop address the address table gives
/// (<see cref="XboxGameMemoryBuffers"/>); an address outside known memory is skipped, warned once.
/// <list type="bullet">
/// <item>71 (address, dword, 0, 0): one dword, a per-frame marker array (0x144ed46a3; 18 dwords a frame from 0x1475DF268).</item>
/// <item>72 (address, 0, 0, 1): a timestamp query's 64-bit value into its record's result array (0x142482b31, record type 2);
/// written as the console's 100 MHz clock from the CPU's clock when the list is recorded.</item>
/// <item>74 (address, 7, qword, 1): a 64-bit value: the frame number into a pending record (0x144e39e77) and a progress
/// counter (0x14031a70a, which the CPU writes itself at 0x14044b6c3 when no list is at hand).</item>
/// </list>
/// </summary>
internal static unsafe class BottomOfPipeWrites
{
    public const int MarkerSlot = 71;
    public const int TimestampSlot = 72;
    public const int ValueSlot = 74;

    /// <summary>ID3D12GraphicsCommandList2::WriteBufferImmediate and D3D12_WRITEBUFFERIMMEDIATE_MODE_MARKER_OUT (d3d12.h).</summary>
    private const int WriteBufferImmediateSlot = 66;
    private const int MarkerOut = 2;

    /// <summary>The console GPU's timestamp frequency.</summary>
    private const long ConsoleTicksPerSecond = 100_000_000;

    private const int DwordShift = 32;
    private const int QwordDwords = 2;

    public static void Bind(XboxObjects objects)
    {
        delegate* unmanaged<XboxObject*, ulong, uint, ulong, uint, int> marker = &Marker;
        delegate* unmanaged<XboxObject*, ulong, ulong, ulong, uint, int> timestamp = &Timestamp;
        delegate* unmanaged<XboxObject*, ulong, uint, ulong, uint, int> value = &Value;
        objects.SetHandler(XboxInterfaceKind.CommandList, MarkerSlot, (nint)marker);
        objects.SetHandler(XboxInterfaceKind.CommandList, TimestampSlot, (nint)timestamp);
        objects.SetHandler(XboxInterfaceKind.CommandList, ValueSlot, (nint)value);
    }

    [HotPath]
    [UnmanagedCallersOnly]
    public static int Marker(XboxObject* self, ulong address, uint value, ulong unused, uint flags)
    {
        Write(self, address, &value, 1);

        return HResult.Ok;
    }

    [HotPath]
    [UnmanagedCallersOnly]
    public static int Timestamp(XboxObject* self, ulong address, ulong unused, ulong unusedToo, uint flags)
    {
        ulong ticks = (ulong)(Stopwatch.GetTimestamp() * (ConsoleTicksPerSecond / (double)Stopwatch.Frequency));
        Write64(self, address, ticks);

        return HResult.Ok;
    }

    [HotPath]
    [UnmanagedCallersOnly]
    public static int Value(XboxObject* self, ulong address, uint mode, ulong value, uint flags)
    {
        RetireWaits.Recorded(self, address, value);
        Write64(self, address, value);

        return HResult.Ok;
    }

    [HotPath]
    private static void Write64(XboxObject* self, ulong address, ulong value)
    {
        uint* words = stackalloc uint[] { (uint)value, (uint)(value >> DwordShift) };
        Write(self, address, words, QwordDwords);
    }

    /// <summary><paramref name="count"/> dwords from <paramref name="words"/> to console address <paramref name="address"/>.</summary>
    [HotPath]
    private static void Write(XboxObject* self, ulong address, uint* words, int count)
    {
        var memory = ShimContext.Runtime.D3D12X.GameMemory;
        var parameters = stackalloc WriteParameter[count];

        for (int i = 0; i < count; i++)
        {
            ulong at = address + ((ulong)i * sizeof(uint));

            if (!memory.TryTranslate(at, out ulong desktop))
            {
                WarnUnknown(at);

                return;
            }

            parameters[i] = new WriteParameter { Address = desktop, Value = words[i] };
        }

        var modes = stackalloc int[count];

        for (int i = 0; i < count; i++)
        {
            modes[i] = MarkerOut;
        }

        nint inner = self->Inner;
        ((delegate* unmanaged<nint, uint, WriteParameter*, int*, void>)ComObject.Slot(inner, WriteBufferImmediateSlot))(inner, (uint)count, parameters, modes);
    }

    private static void WarnUnknown(ulong address)
    {
        ShimContext.Runtime.D3D12X.Warnings.Warn("bottom-of-pipe", $"[d3d12_xs] a bottom-of-pipe write to 0x{address:x}, outside known game memory: skipped");
    }

    /// <summary>D3D12_WRITEBUFFERIMMEDIATE_PARAMETER.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct WriteParameter
    {
        public ulong Address;
        public uint Value;
    }
}
