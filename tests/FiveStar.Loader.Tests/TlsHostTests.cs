// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Runtime.InteropServices;
using FiveStar.Loader.Codegen;
using FiveStar.Loader.Loading;
using FiveStar.Loader.Native;
using Xunit;

namespace FiveStar.Loader.Tests;

/// <summary>The TLS host of an executable on ntdll's real allocation of TLS blocks.</summary>
public sealed unsafe class TlsHostTests
{
    /// <summary>The game executable's template: 0x1480 bytes, IMAGE_SCN_ALIGN_16BYTES; a zero fill on top.</summary>
    private const int RawSize = 0x1480;
    private const uint ZeroFill = 0x40;
    private const uint Align16Bytes = 0x00500000;

    /// <summary><c>mov rax,gs:[58h]; ret</c>.</summary>
    private static readonly byte[] ReadTlsArray = [0x65, 0x48, 0x8B, 0x04, 0x25, 0x58, 0, 0, 0, 0xC3];

    [DllImport("kernel32")]
    private static extern nint GetProcessHeap();

    [DllImport("kernel32")]
    private static extern nuint HeapSize(nint heap, uint flags, nint block);

    [DllImport("kernel32")]
    private static extern int FreeLibrary(nint module);

    [Fact]
    public void A_new_threads_block_with_zero_fill_and_saved_slot_zero_lies_inside_its_heap_allocation()
    {
        uint* imageIndex = (uint*)NativeMemory.AllocZeroed(sizeof(uint));
        var template = new TlsTemplate(new byte[RawSize], ZeroFill, Align16Bytes, [], 0, (ulong)imageIndex, true);
        string path = Path.Combine(Path.GetTempPath(), $"fs_tlshost_{Environment.ProcessId}.dll");
        File.WriteAllBytes(path, TlsHostBuilder.Build(template));

        var heap = new CodeHeap();
        var readArray = (delegate* unmanaged<nint*>)heap.Write(ReadTlsArray);
        heap.Seal();
        nint host;

        fixed (char* name = path)
        {
            host = Kernel32.LoadLibraryExW(name, 0, 0);
        }

        Assert.NotEqual(0, host);
        uint index = *(uint*)(host + (nint)(TlsHostBuilder.DataRva + TlsHostBuilder.HostIndexOffset));
        nint block = 0, slotZero = 0, allocation = 0;
        nuint allocated = 0;

        // ntdll keeps each block's heap allocation in the 8 bytes before it (LdrpFreeTls frees that pointer).
        var thread = new Thread(() =>
        {
            nint* array = readArray();
            block = array[index];
            slotZero = array[0];
            allocation = *(nint*)(block - sizeof(nint));
            allocated = HeapSize(GetProcessHeap(), 0, allocation);
        });
        thread.Start();
        thread.Join();
        _ = FreeLibrary(host);
        File.Delete(path);

        int size = TlsHostBuilder.TemplateSize(template);
        Assert.Equal(block, slotZero);
        Assert.True(size >= RawSize + ZeroFill + TlsSlotZero.SaveSize);
        Assert.True(block + size <= allocation + (nint)allocated, $"block 0x{block:x} + 0x{size:x} ends past allocation 0x{allocation:x} + 0x{allocated:x}");
    }
}
