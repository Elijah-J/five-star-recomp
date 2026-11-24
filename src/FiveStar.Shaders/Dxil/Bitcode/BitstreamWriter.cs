// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

namespace FiveStar.Shaders.Dxil.Bitcode;

/// <summary>
/// The LLVM bitstream: little-endian 32-bit words filled from bit 0, blocks with a length word, and unabbreviated
/// records only (code, operand count and operands as 6-bit VBR). DXIL fixes the LLVM 3.7 dialect of this format.
/// </summary>
public sealed class BitstreamWriter
{
    private const int TopWidth = 2;
    private const int WordBits = 32;
    private const int BlockIdWidth = 8;
    private const int BlockWidthWidth = 4;
    private const int RecordWidth = 6;
    private const ulong EndBlock = 0;
    private const ulong EnterSubblock = 1;
    private const ulong UnabbreviatedRecord = 3;

    private readonly List<uint> _words = [];
    private readonly Stack<(int Width, int Start)> _blocks = new();
    private ulong _current;
    private int _bits;

    private int Width
    {
        get
        {
            if (_blocks.Count == 0)
            {
                return TopWidth;
            }

            return _blocks.Peek().Width;
        }
    }

    /// <summary>Writes raw bytes (the magic number).</summary>
    public void Bytes(ReadOnlySpan<byte> bytes)
    {
        foreach (var value in bytes)
        {
            Emit(value, BitsPerByte);
        }
    }

    /// <summary>Opens a block whose abbreviation ids are <paramref name="width"/> bits wide.</summary>
    public void Enter(BlockId id, int width)
    {
        Emit(EnterSubblock, Width);
        Vbr((ulong)id, BlockIdWidth);
        Vbr((ulong)width, BlockWidthWidth);
        Align();
        _words.Add(0);
        _blocks.Push((width, _words.Count));
    }

    /// <summary>Closes the innermost block and fills in its length.</summary>
    public void Exit()
    {
        Emit(EndBlock, Width);
        Align();
        var (_, start) = _blocks.Pop();
        _words[start - 1] = (uint)(_words.Count - start);
    }

    /// <summary>One unabbreviated record.</summary>
    public void Record(int code, ReadOnlySpan<ulong> operands)
    {
        Emit(UnabbreviatedRecord, Width);
        Vbr((ulong)code, RecordWidth);
        Vbr((ulong)operands.Length, RecordWidth);

        foreach (var operand in operands)
        {
            Vbr(operand, RecordWidth);
        }
    }

    /// <summary>A record whose operands are the characters of <paramref name="text"/> after <paramref name="prefix"/>.</summary>
    public void TextRecord(int code, ReadOnlySpan<ulong> prefix, string text)
    {
        Span<ulong> operands = new ulong[prefix.Length + text.Length];
        prefix.CopyTo(operands);

        for (var index = 0; index < text.Length; index++)
        {
            operands[prefix.Length + index] = text[index];
        }

        Record(code, operands);
    }

    /// <summary>The finished stream.</summary>
    public byte[] ToArray()
    {
        Align();
        var bytes = new byte[_words.Count * sizeof(uint)];

        for (var index = 0; index < _words.Count; index++)
        {
            BitConverter.TryWriteBytes(bytes.AsSpan(index * sizeof(uint)), _words[index]);
        }

        return bytes;
    }

    /// <summary>LLVM's signed VBR operand: the magnitude shifted left, the sign in bit 0.</summary>
    public static ulong Signed(long value)
    {
        if (value >= 0)
        {
            return (ulong)value << 1;
        }

        return ((ulong)-value << 1) | 1;
    }

    private const int BitsPerByte = 8;

    private void Emit(ulong value, int width)
    {
        _current |= value << _bits;
        _bits += width;

        while (_bits >= WordBits)
        {
            _words.Add((uint)_current);
            _current >>= WordBits;
            _bits -= WordBits;
        }
    }

    private void Vbr(ulong value, int width)
    {
        var high = 1UL << (width - 1);

        while (value >= high)
        {
            Emit((value & (high - 1)) | high, width);
            value >>= width - 1;
        }

        Emit(value, width);
    }

    private void Align()
    {
        if (_bits > 0)
        {
            _words.Add((uint)_current);
            _current = 0;
            _bits = 0;
        }
    }
}
