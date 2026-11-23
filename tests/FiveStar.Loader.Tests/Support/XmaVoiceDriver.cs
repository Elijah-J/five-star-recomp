// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using Xunit;

namespace FiveStar.Loader.Tests.Support;

/// <summary>
/// Drives one SHAPE XMA context the way the game's XMA voice does: the start-up calls, the input in APU memory, the
/// context filled as 0x1400785d0 fills it, message 5 to start, then per audio frame the output copied from the read
/// offset to the write offset (0x140078120) and acknowledged with message 0x12.
/// </summary>
internal sealed unsafe class XmaVoiceDriver
{
    public const int Blocks = 23;
    public const int BlockSamples = 128;

    private const int Index = 17;

    private readonly delegate* unmanaged<nint, uint, uint, uint, uint*, uint, int> _send;
    private readonly nint _hal;
    private readonly short* _output;

    /// <summary>Sets up a mono context over <paramref name="data"/> (whole packets) with sample rate code <paramref name="rate"/>.</summary>
    public XmaVoiceDriver(TestRuntime test, ReadOnlySpan<byte> data, uint rate)
    {
        var createHeap = (delegate* unmanaged<uint, uint, int>)test.Shim("acphal.dll", "ApuCreateHeap");
        var allocateContexts = (delegate* unmanaged<byte*, int>)test.Shim("acphal.dll", "AcpHalAllocateShapeContexts");
        var allocate = (delegate* unmanaged<nint*, uint*, uint, uint, uint, int>)test.Shim("acphal.dll", "ApuAlloc");
        var create = (delegate* unmanaged<nint*, int>)test.Shim("acphal.dll", "AcpHalCreate");
        Assert.Equal(0, createHeap(0x1000000, 0x226E00));

        var block = stackalloc byte[0x40];
        new Span<byte>(block, 0x40).Clear();
        *(uint*)(block + 0x10) = 350;
        Assert.Equal(0, allocateContexts(block));
        Context = (uint*)(*(nint*)(block + 0x38) + (Index * 64));

        nint hal;
        Assert.Equal(0, create(&hal));
        _hal = hal;
        _send = (delegate* unmanaged<nint, uint, uint, uint, uint*, uint, int>)(*(nint**)hal)[2];

        nint input;
        nint output;
        uint inputApu;
        uint outputApu;
        Assert.Equal(0, allocate(&input, &inputApu, (uint)data.Length, 0x800, 0));
        Assert.Equal(0, allocate(&output, &outputApu, Blocks * 256, 0x100, 0));

        data.CopyTo(new Span<byte>((void*)input, data.Length));
        _output = (short*)output;

        // As the game: packets, buffer 0 valid, 23 output blocks; 4 subframes per decode, rate, mono, output valid;
        // read offset 32 (the first packet's header); both input pointers at the data; the output pointer.
        new Span<uint>(Context, 16).Clear();
        Context[0] = (uint)(data.Length / 2048) | (1u << 20) | (Blocks << 22);
        Context[1] = (4u << 20) | (rate << 27) | (1u << 31);
        Context[2] = 32;
        (Context[5], Context[6], Context[7]) = (inputApu, inputApu, outputApu);
    }

    public uint* Context { get; }

    public uint WriteOffset => Context[0] >> 27;

    public uint ReadOffset => Context[9] & 0x1F;

    public bool OutputValid => (Context[1] >> 31) != 0;

    public bool InputValid => ((Context[0] >> 20) & 1) != 0;

    /// <summary>Blocks the game would see as ready: write minus read, or all of them when they meet and the output is not valid.</summary>
    public int Ready
    {
        get
        {
            int ready = (int)((WriteOffset - ReadOffset + Blocks) % Blocks);

            if (ready == 0 && !OutputValid)
            {
                return Blocks;
            }

            return ready;
        }
    }

    public void Enable()
    {
        Send(5, 0xF6F6F6F6, 0);
    }

    public void Disable()
    {
        Send(7, Index + 1, 0);
    }

    /// <summary>Copies up to <paramref name="most"/> ready blocks from the read offset and acknowledges them.</summary>
    public short[] Take(int most)
    {
        int count = Math.Min(most, Ready);
        var samples = new short[count * BlockSamples];

        for (int block = 0; block < count; block++)
        {
            int at = (int)((ReadOffset + block) % Blocks) * BlockSamples;
            new ReadOnlySpan<short>(_output + at, BlockSamples).CopyTo(samples.AsSpan(block * BlockSamples));
        }

        Send(0x12, 0xF1F1F1F1, (uint)count);

        return samples;
    }

    /// <summary>Takes blocks until the input is used up and nothing is ready.</summary>
    public short[] TakeAll(int most)
    {
        var all = new List<short>();

        while (InputValid || Ready > 0)
        {
            var taken = Take(most);
            Assert.NotEmpty(taken);
            all.AddRange(taken);
        }

        return [.. all];
    }

    private void Send(uint type, uint cookie, uint blocks)
    {
        var payload = stackalloc uint[2];
        (payload[0], payload[1]) = (Index, blocks);

        Assert.Equal(0, _send(_hal, type, cookie, uint.MaxValue, payload, 0));
    }
}
