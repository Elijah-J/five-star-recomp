// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Buffers.Binary;
using System.Globalization;
using System.IO.Compression;
using System.Text;

namespace FiveStar.Shaders.Containers;

/// <summary>
/// Finds the shader records in the plaintext SGD5 tables of shaders.rpf. A table starts at a 4 KB page beginning with
/// "SGD5" and runs to the next one (the last to the end of the file). A record is
/// <c>u8 name length (incl. NUL), name, NUL, u8 tag, u32 csize, u32 constant, u32 csize, u32 dsize, raw deflate[csize]</c>
/// and decompresses to a DXBC container of dsize bytes.
/// </summary>
public static class ShaderArchiveScanner
{
    private const string TableMagic = "SGD5";
    private const int PageSize = 0x1000;
    private const string RegionPrefix = "sgd";

    // Name: at least 3 and at most 254 printable ASCII characters; the length byte is at least 5.
    private const int MinLengthByte = 5;
    private const int MinNameLength = 3;
    private const int MaxNameLength = 254;
    private const byte FirstPrintable = 0x20;
    private const byte LastPrintable = 0x7E;

    // Tag byte range of shader records (0x32, 0x3C, 0x40, 0x41, 0x42 are seen).
    private const byte FirstTag = 0x30;
    private const byte LastTag = 0x4F;

    // Header after the name: u8 tag @0, u32 csize @1, u32 constant @5, u32 csize @9, u32 dsize @13; data @17.
    private const int CompressedSizeField = 1;
    private const int ConstantField = 5;
    private const int CompressedSizeCopyField = 9;
    private const int DecompressedSizeField = 13;
    private const int RecordHeaderSize = 17;
    private const int MaxCompressedSize = 0x4000000;
    private const int MaxDecompressedSize = 0x10000000;

    /// <summary>Every record that decompresses to a DXBC container of the declared size, in file order.</summary>
    public static IReadOnlyList<ShaderArchiveRecord> Scan(byte[] archive)
    {
        var tables = FindTables(archive);
        var records = new List<ShaderArchiveRecord>();

        for (var index = 0; index < tables.Count; index++)
        {
            var start = tables[index];
            var end = index + 1 < tables.Count ? tables[index + 1] : archive.Length;
            var region = RegionPrefix + (index + 1).ToString(CultureInfo.InvariantCulture);
            ScanTable(archive, region, start, end, records);
        }

        return records;
    }

    private static List<int> FindTables(byte[] archive)
    {
        var magic = Encoding.Latin1.GetBytes(TableMagic);
        var tables = new List<int>();

        for (var page = 0; page + magic.Length <= archive.Length; page += PageSize)
        {
            if (archive.AsSpan(page, magic.Length).SequenceEqual(magic))
            {
                tables.Add(page);
            }
        }

        return tables;
    }

    private static void ScanTable(byte[] archive, string region, int start, int end, List<ShaderArchiveRecord> records)
    {
        var data = new ReadOnlyMemory<byte>(archive, start, end - start);
        var position = 0;

        while (position < data.Length)
        {
            var nameLength = MatchName(data.Span, position);

            if (nameLength < 0)
            {
                position++;
                continue;
            }

            var tagAt = position + 1 + nameLength + 1;
            var record = TryRead(data, region, start, position, nameLength, tagAt);

            if (record is not null)
            {
                records.Add(record);
            }

            position = tagAt + 1;
        }
    }

    /// <summary>Length of the printable name at position + 1 when a record header starts here, else -1.</summary>
    private static int MatchName(ReadOnlySpan<byte> data, int position)
    {
        if (data[position] < MinLengthByte)
        {
            return -1;
        }

        var run = 0;

        while (position + 1 + run < data.Length && run <= MaxNameLength && IsPrintable(data[position + 1 + run]))
        {
            run++;
        }

        var nulAt = position + 1 + run;
        var tagAt = nulAt + 1;

        if (run < MinNameLength || run > MaxNameLength || tagAt >= data.Length || data[nulAt] != 0)
        {
            return -1;
        }

        if (data[tagAt] < FirstTag || data[tagAt] > LastTag)
        {
            return -1;
        }

        return run;
    }

    private static bool IsPrintable(byte value)
    {
        return value >= FirstPrintable && value <= LastPrintable;
    }

    private static ShaderArchiveRecord? TryRead(
        ReadOnlyMemory<byte> data, string region, int tableStart, int position, int nameLength, int tagAt)
    {
        var span = data.Span;

        if (span[position] != nameLength + 1 || tagAt + RecordHeaderSize > data.Length)
        {
            return null;
        }

        var compressed = BinaryPrimitives.ReadInt32LittleEndian(span[(tagAt + CompressedSizeField)..]);
        var copy = BinaryPrimitives.ReadInt32LittleEndian(span[(tagAt + CompressedSizeCopyField)..]);
        var size = BinaryPrimitives.ReadInt32LittleEndian(span[(tagAt + DecompressedSizeField)..]);

        if (compressed != copy || compressed <= 0 || compressed >= MaxCompressedSize || size <= 0 || size >= MaxDecompressedSize)
        {
            return null;
        }

        var payloadStart = tagAt + RecordHeaderSize;
        var payload = data.Slice(payloadStart, Math.Min(compressed, data.Length - payloadStart));
        var container = Inflate(payload, size);

        if (container is null || container.Length != size || !DxbcContainer.HasMagic(container))
        {
            return null;
        }

        var chunks = DxbcContainer.Parse(container).Chunks.Select(chunk => chunk.FourCc).ToList();
        var name = Encoding.Latin1.GetString(span.Slice(position + 1, nameLength));
        var constant = BinaryPrimitives.ReadUInt32LittleEndian(span[(tagAt + ConstantField)..]);

        return new ShaderArchiveRecord(
            region, tableStart + position, tableStart + tagAt, span[tagAt], name, compressed, size, constant, container, chunks);
    }

    private static byte[]? Inflate(ReadOnlyMemory<byte> payload, int expected)
    {
        try
        {
            using var input = new MemoryStream(payload.ToArray());
            using var deflate = new DeflateStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream(expected);
            deflate.CopyTo(output);

            return output.ToArray();
        }
        catch (InvalidDataException)
        {
            return null;
        }
    }
}
