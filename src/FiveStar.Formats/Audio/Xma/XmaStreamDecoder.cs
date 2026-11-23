// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

namespace FiveStar.Formats.Audio.Xma;

/// <summary>
/// Decodes one XMA2 stream (one or two channels) packet by packet into 32-bit float PCM, full scale 1.0. Frames run on
/// across packet boundaries; the packet header says how many bits of a new packet finish the frame begun before it.
/// Every frame gives 512 samples per channel. The output starts with the decoder's own delay (see docs/xma.md), which
/// <see cref="XmaTrackDecoder"/> trims for whole sounds.
/// </summary>
public sealed class XmaStreamDecoder
{
    /// <summary>Most frames one packet can finish: the 6-bit frame count, plus the one begun in the packet before.</summary>
    public const int MaxFramesPerPacket = 1 << XmaLayout.FrameCountBits;

    private const int BitsPerByte = 8;

    private readonly XmaFrameDecoder _frames;
    private readonly XmaFrameAssembler _assembler = new();

    public XmaStreamDecoder(int channels, int sampleRate)
    {
        _frames = new XmaFrameDecoder(channels, sampleRate);
    }

    public int Channels => _frames.Channels;

    /// <summary>Gets the floats one packet can produce at most: <see cref="MaxFramesPerPacket"/> frames of all channels.</summary>
    public int MaxPacketOutput => MaxFramesPerPacket * XmaLayout.FrameSamples * Channels;

    /// <summary>Gets the frames decoded so far.</summary>
    public long Frames { get; private set; }

    /// <summary>Gets the frames that did not decode cleanly: a broken bitstream, or not ending where their length says.</summary>
    public long BadFrames { get; private set; }

    /// <summary>Gets the partial frames dropped because the next packet did not continue them.</summary>
    public long LostFrames { get; private set; }

    /// <summary>Gets the start trim of the last frame that carried one, in samples.</summary>
    public int TrimStart => _frames.TrimStart;

    /// <summary>Gets the end trim of the last frame that carried one, in samples.</summary>
    public int TrimEnd => _frames.TrimEnd;

    /// <summary>
    /// Decodes one packet and writes the frames it completes, 512 interleaved samples per channel each, to
    /// <paramref name="output"/> (at least <see cref="MaxPacketOutput"/> floats). Returns the frames written.
    /// </summary>
    [HotPath]
    public int DecodePacket(ReadOnlySpan<byte> packet, Span<float> output)
    {
        RequirePacket(packet);

        var header = XmaPacketHeader.Read(packet);
        var payloadBits = (XmaLayout.PacketBytes * BitsPerByte) - XmaLayout.PacketHeaderBits;
        var reader = new XmaBitReader(packet, XmaLayout.PacketHeaderBits, payloadBits);
        var written = FinishCarriedFrame(ref reader, header.FirstFrameOffset, output);

        if (header.FirstFrameOffset >= payloadBits)
        {
            return written;
        }

        return written + ReadFrames(ref reader, output[(written * FrameFloats)..]);
    }

    /// <summary>Writes the tail of the last frame's overlap, 256 interleaved samples per channel, and returns that count.</summary>
    public int Flush(Span<float> output)
    {
        _frames.Flush(output);

        return XmaChannel.FrameStart;
    }

    /// <summary>Starts over, as at the stream's first packet.</summary>
    public void Reset()
    {
        _assembler.Clear();
        _frames.Reset();
    }

    private int FrameFloats => XmaLayout.FrameSamples * Channels;

    /// <summary>Adds the first bits of the packet to the frame carried over from the last one and decodes it when whole.</summary>
    [HotPath]
    private int FinishCarriedFrame(ref XmaBitReader reader, int carried, Span<float> output)
    {
        var take = carried < reader.Remaining ? carried : reader.Remaining;

        if (_assembler.Bits == 0 || take == 0)
        {
            DropPartial();
            reader.Advance(take);

            return 0;
        }

        _assembler.Collect(ref reader, take);

        if (_assembler.Complete)
        {
            return DecodeAssembled(output);
        }

        if (take < carried)
        {
            return 0;
        }

        DropPartial();

        return 0;
    }

    /// <summary>Decodes the frames that start in this packet; a last frame that runs on is kept for the next packet.</summary>
    [HotPath]
    private int ReadFrames(ref XmaBitReader reader, Span<float> output)
    {
        var written = 0;

        while (reader.Remaining > 0 && (written + 1) * FrameFloats <= output.Length)
        {
            if (reader.Remaining >= XmaLayout.FrameLengthBits)
            {
                var length = (int)reader.Peek(XmaLayout.FrameLengthBits);

                if (length == XmaLayout.NoFrame || length <= XmaLayout.FrameLengthBits)
                {
                    break;
                }

                if (length <= reader.Remaining)
                {
                    _assembler.Clear();
                    _assembler.Collect(ref reader, length);
                    var more = _assembler.MoreFrames();
                    written += DecodeAssembled(output[(written * FrameFloats)..]);

                    if (!more)
                    {
                        break;
                    }

                    continue;
                }
            }

            _assembler.Clear();
            _assembler.Collect(ref reader, reader.Remaining);
        }

        return written;
    }

    [HotPath]
    private int DecodeAssembled(Span<float> output)
    {
        if (!_frames.Decode(_assembler.Data, _assembler.DeclaredLength, output))
        {
            BadFrames++;
        }

        Frames++;
        _assembler.Clear();

        return 1;
    }

    private static void RequirePacket(ReadOnlySpan<byte> packet)
    {
        if (packet.Length < XmaLayout.PacketBytes)
        {
            throw new ArgumentException("an XMA packet is 2048 bytes", nameof(packet));
        }
    }

    private void DropPartial()
    {
        if (_assembler.Bits > 0)
        {
            LostFrames++;
            _assembler.Clear();
        }
    }
}
