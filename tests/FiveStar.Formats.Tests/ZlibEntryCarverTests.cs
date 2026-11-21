// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using FiveStar.Formats.Common;
using FiveStar.Formats.Compression;
using FiveStar.Formats.Rpf;
using FiveStar.Formats.Tests.Support;
using Xunit;

namespace FiveStar.Formats.Tests;

/// <summary>The carver on a hand-built archive data area.</summary>
public sealed class ZlibEntryCarverTests
{
    private const int Chunk = 0x10000;

    [Fact]
    public void An_archive_carves_into_its_entries_with_raw_pso_tails_cut_at_the_declared_size()
    {
        // A text entry of two zlib chunks, a PSO file whose incompressible tail is stored raw, an XML entry.
        var text = Deflate.Pattern(Chunk + 1000);
        var pso = PsoFile(Chunk + 0x300);
        var xml = "<?xml version=\"1.0\"?><a/>"u8.ToArray();

        var archive = new ByteWriter().PadTo(0x10);
        var textAt = AddEntry(archive, text);
        long psoAt = archive.Position;
        archive.Bytes(Deflate.Zlib(pso[..Chunk])).Align(16, 0).Bytes(pso.AsSpan(Chunk)).Align(16, 0);
        var xmlAt = AddEntry(archive, xml);
        archive.PadTo(archive.Position + Chunk);

        using var source = new MemoryByteSource(archive.ToArray());
        var carver = new ZlibEntryCarver(source, new ZlibStreamProbe(source, new ZlibInflater()));

        var blobs = carver.Carve(0).ToList();

        Assert.Equal([textAt, psoAt, xmlAt], blobs.Select(blob => blob.Offset));
        Assert.Equal([BlobKind.Txt, BlobKind.Psin, BlobKind.Xml], blobs.Select(blob => blob.Kind));
        Assert.Equal(text, blobs[0].Data);
        Assert.Equal(pso, blobs[1].Data);
        Assert.Equal(1, blobs[1].RawChunks);
        Assert.Equal(xml, blobs[2].Data);
    }

    private static long AddEntry(ByteWriter archive, byte[] data)
    {
        long start = archive.Position;

        for (var offset = 0; offset < data.Length; offset += Chunk)
        {
            var chunk = data.AsSpan(offset, Math.Min(Chunk, data.Length - offset)).ToArray();
            archive.Bytes(Deflate.Zlib(chunk)).Align(16, 0);
        }

        return start;
    }

    // PSIN chunk of noise, then CHKS {BE total size, checksum, 'zppp'}.
    private static byte[] PsoFile(int total)
    {
        var file = new ByteWriter().Bytes("PSIN"u8).U32BigEndian((uint)(total - 20));
        file.Bytes(Deflate.Noise(total - 20 - 8, 99));
        file.Bytes("CHKS"u8).U32BigEndian(20).U32BigEndian((uint)total).U32(0x12345678).Bytes("zppp"u8);

        return file.ToArray();
    }
}
