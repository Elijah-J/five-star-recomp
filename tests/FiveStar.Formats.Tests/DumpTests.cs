// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

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
    public void The_graphics_page_file_extracts_892_textures()
    {
        var path = Path.Combine(DumpFactAttribute.Folder!, "data", "ttdb", "graphics", "ttdb_graphics.ttpf");
        using var source = new MappedFileSource(path);
        var file = new TexturePageFile(source, new ZlibInflater());
        var extractor = new TextureExtractor(file);

        var statuses = file.Descriptors.Select(descriptor => extractor.ReadMip0(descriptor).Status.Name).ToList();

        Assert.Equal(912, statuses.Count);
        Assert.Equal(892, statuses.Count(status => status == "ok"));
        Assert.Equal(15, statuses.Count(status => status == "skip_unsupported_codec"));
    }
}
