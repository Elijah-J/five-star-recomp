// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using FiveStar.Formats.Audio;
using FiveStar.Formats.Common;
using FiveStar.Formats.Compression;
using FiveStar.Formats.Pso;
using FiveStar.Formats.Rpf;
using FiveStar.Formats.Tests.Support;
using FiveStar.Formats.Ttpf;
using Xunit;

namespace FiveStar.Formats.Tests;

/// <summary>The readers on the user's own dump (FIVESTAR_DUMP); the counts are those of the shipped package.</summary>
public sealed class DumpTests
{
    [DumpFact]
    public void Script_rpf_carves_into_1013_pso_files_that_all_walk()
    {
        using var source = new MappedFileSource(Path.Combine(DumpFactAttribute.Folder!, "script.rpf"));
        var carver = new ZlibEntryCarver(source, new ZlibStreamProbe(source, new ZlibInflater()));

        var blobs = carver.Carve(0).ToList();
        var roots = new HashSet<uint>();

        foreach (var blob in blobs.Where(blob => blob.Kind == BlobKind.Psin))
        {
            var document = PsoReader.Load(blob.Data);
            new PsoWalker(document).WalkRoot(PsoWalker.UnlimitedBudget);
            roots.Add(document.BlockAt(document.Root!.Value).Type);
        }

        Assert.Equal(1013, blobs.Count);
        Assert.Equal(1012, blobs.Count(blob => blob.Kind == BlobKind.Psin));
        Assert.Equal([0xDE396FE2u], roots);
    }

    [DumpFact]
    public void An_audio_bank_stream_holds_opus_packets_that_cover_its_sample_count()
    {
        using var source = new MappedFileSource(Path.Combine(DumpFactAttribute.Folder!, "audio.rpf"));

        var container = AwcReader.TryRead(source, 0x9FAA000)!;
        var track = new AwcTrackReader(source).Read(container).Single();

        Assert.Equal(0x30B0, container.Size);
        Assert.Equal(0x053B07D5u, track.Id);
        Assert.Null(track.Problem);

        Assert.Equal(AwcCodec.Opus, track.Codec);
        Assert.Equal(new AwcFormat(135_555, 48_000, 109, 0x0B, 1), track.Format);
        Assert.Equal(144, track.Packets.Count);
        Assert.Equal(144 * 960, track.PacketSamples);
    }

    [DumpFact]
    public void A_multichannel_bank_splits_into_two_stereo_tracks_across_its_blocks()
    {
        using var source = new MappedFileSource(Path.Combine(DumpFactAttribute.Folder!, "audio.rpf"));

        var container = AwcReader.TryRead(source, 0x2CCBF0000)!;
        var tracks = new AwcTrackReader(source).Read(container).ToList();

        Assert.Equal([0x1DE4E830u, 0x0AA7DD46u], tracks.Select(track => track.Id));
        Assert.All(tracks, track => Assert.Null(track.Problem));
        Assert.All(tracks, track => Assert.Equal(2, track.Format.Channels));
        Assert.All(tracks, track => Assert.Equal(16_778_489u, track.Format.Frames));
        Assert.All(tracks, track => Assert.InRange(track.PacketSamples - track.Format.Frames, OggOpusWriter.PreSkip, 2880));
    }

    [DumpFact]
    public void The_graphics_page_file_extracts_902_textures()
    {
        var path = Path.Combine(DumpFactAttribute.Folder!, "data", "ttdb", "graphics", "ttdb_graphics.ttpf");
        using var source = new MappedFileSource(path);
        var file = new TexturePageFile(source, new ZlibInflater());
        var extractor = new TextureExtractor(file);

        var statuses = file.Descriptors.Select(descriptor => extractor.ReadMip0(descriptor).Status.Name).ToList();

        Assert.Equal(912, statuses.Count);
        Assert.Equal(902, statuses.Count(status => status == "ok"));
        Assert.Equal(5, statuses.Count(status => status == "skip_unsupported_codec"));
    }
}
