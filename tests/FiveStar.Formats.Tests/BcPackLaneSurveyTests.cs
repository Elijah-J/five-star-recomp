// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using FiveStar.Formats.Common;
using FiveStar.Formats.Compression;
using FiveStar.Formats.Tests.Support;
using FiveStar.Formats.Ttpf;
using Xunit;

namespace FiveStar.Formats.Tests;

/// <summary>The BCPack lane survey: model split, first-byte offsets and the twin-lane check.</summary>
public sealed class BcPackLaneSurveyTests
{
    private const int Bc1 = 1;
    private const int Bc5 = 5;

    [Fact]
    public void A_model_byte_splits_into_family_and_parameter()
    {
        Assert.Equal(new BcPackModel(6, 30), BcPackModel.FromByte(0xDE));
        Assert.Equal("1.16", BcPackModel.FromByte(0x30).ToString());
    }

    [Fact]
    public void Lanes_that_read_bytes_take_one_each_after_the_header_and_twins_are_compared()
    {
        var head = new byte[BcPackLaneSurvey.HeadBytes];

        // Budgets: lanes 0 and 2 read (13 and the wrapped 2), lane 1 reads nothing; lanes 3-7 equal lane 0.
        byte[] budgets = [13, 3, 2, 13, 13, 13, 13, 13];

        for (var lane = 0; lane < BcPackHeader.Lanes; lane++)
        {
            head[lane] = 0x5F;
            head[BcPackHeader.Lanes + lane] = budgets[lane];
            head[(2 * BcPackHeader.Lanes) + lane] = 1;
        }

        // Distinct first bytes, except that lanes 0 and 3 (offsets 48 and 50) agree.
        for (var offset = BcPackHeader.Size; offset < head.Length; offset++)
        {
            head[offset] = (byte)offset;
        }

        head[BcPackHeader.Size + 2] = head[BcPackHeader.Size];

        var lanes = BcPackHeader.Parse(head);
        var offsets = BcPackLaneSurvey.FirstByteOffsets(lanes);
        var survey = new BcPackLaneSurvey();
        survey.Add(head, Bc1);

        Assert.Equal([48, -1, 49, 50, 51, 52, 53, 54], offsets);
        Assert.Equal(15, survey.TwinPairs);
        Assert.Equal(1, survey.TwinMatches);
        Assert.Equal(1, survey.Models[(Bc1, 1, new BcPackModel(2, 31))]);
    }

    [DumpFact]
    public void Twin_lanes_in_the_graphics_page_file_read_equal_first_bytes()
    {
        var path = Path.Combine(DumpFactAttribute.Folder!, "data", "ttdb", "graphics", "ttdb_graphics.ttpf");
        using var source = new MappedFileSource(path);
        var file = new TexturePageFile(source, new ZlibInflater());
        var survey = new BcPackLaneSurvey();

        foreach (var page in file.Pages)
        {
            if (page.IsBcPack && page.VirtualSize != 0)
            {
                survey.Add(source.Read(page.FileOffset, BcPackLaneSurvey.HeadBytes), page.Codec);
            }
        }

        long bc5Pages = 0;

        foreach (var pair in survey.Models)
        {
            if (pair.Key.Codec == Bc5 && pair.Key.Lane == 1)
            {
                Assert.Equal(new BcPackModel(6, 27), pair.Key.Model);
                bc5Pages += pair.Value;
            }
        }

        Assert.True(bc5Pages > 0);
        Assert.True(survey.TwinPairs == 0 || survey.TwinMatches * 10 >= survey.TwinPairs * 9);
    }
}
