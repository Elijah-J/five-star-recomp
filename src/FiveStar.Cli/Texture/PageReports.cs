// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using FiveStar.Cli.Archive;
using FiveStar.Formats.Common;
using FiveStar.Formats.Compression;
using FiveStar.Formats.Ttpf;

namespace FiveStar.Cli.Texture;

/// <summary>Page-level texture reports: the page-table sample dump and the BCPack header checks.</summary>
internal sealed class PageReports
{
    private const int ExamplesPerFlag = 3;
    private const int EntropyProbe = 4096;
    private const int HeadBytes = 12;
    private const int FlagDigits = 2;
    private const int TwoDecimals = 2;
    private const int RatioDecimals = 3;
    private const int AddressDigits = 10;
    private const int SizeWidth = 8;
    private const int BudgetWidth = 3;
    private const int StateDigits = 8;
    private const int StatusWidth = 20;
    private const int HeaderReadLimit = 256;
    private const long DefaultLimit = 20;

    private readonly TextWriter _output;

    public PageReports(TextWriter output)
    {
        _output = output;
    }

    /// <summary>The first three entries of each flag value with their storage kind, then the count per (flag, kind).</summary>
    public void Pages(string path)
    {
        using var source = new MappedFileSource(path);
        var survey = new PageTableSurvey(source, new ZlibInflater());
        var entries = survey.Entries();
        var shown = new Dictionary<byte, int>();
        var kinds = new Tally<(byte Flag, string Kind)>();

        for (var index = 0; index < entries.Count; index++)
        {
            var entry = entries[index];
            var stored = StoredBytes(source, entries, index);
            var kind = survey.Probe(stored);
            kinds.Add((entry.Flag, kind.Split(' ')[0]));
            var seen = shown.GetValueOrDefault(entry.Flag);

            if (seen >= ExamplesPerFlag)
            {
                continue;
            }

            shown[entry.Flag] = seen + 1;
            var next = index + 1 < entries.Count ? entries[index + 1].VirtualPage : 0;
            var entropy = DecimalText.Fixed(ByteEntropy.BitsPerByte(stored[..Math.Min(EntropyProbe, stored.Length)]), TwoDecimals);
            var head = Convert.ToHexStringLower(stored[..Math.Min(HeadBytes, stored.Length)]);
            _output.WriteLine($"#{index} flag=0x{ReportText.Hex(entry.Flag, FlagDigits)} off={DecimalText.Hex(entry.FileOffset)} len={DecimalText.Hex(stored.Length)} hi20={DecimalText.Hex(entry.Tag)} vstep={next - entry.VirtualPage} ent={entropy} {kind} head={SpacedHex(head)}");
        }

        var parts = new List<string>();

        foreach (var pair in kinds.MostCommon())
        {
            parts.Add($"({pair.Key.Flag}, {ReportText.Quote(pair.Key.Kind)}): {pair.Value}");
        }

        _output.WriteLine($"Counter({{{string.Join(", ", parts)}}})");
    }

    /// <summary>Per BCPack page: its header lanes and the check of each near-constant lane.</summary>
    public void BcPackPages(string path, CommandArguments arguments)
    {
        using var source = new MappedFileSource(path);
        var file = new TexturePageFile(source, new ZlibInflater());
        var verifier = new BcPackVerifier();
        var limit = arguments.LongOption("--limit", DefaultLimit);
        var onlyCodec = arguments.LongOption("--codec", 0);
        long printed = 0;

        foreach (var page in BcPackPagesOf(file))
        {
            if (onlyCodec != 0 && page.Codec != onlyCodec)
            {
                continue;
            }

            var data = source.Read(page.FileOffset, (int)Math.Min(page.FileLength, HeaderReadLimit));
            var checks = verifier.CheckPage(data, page.Codec, page.VirtualSize);
            var ratio = DecimalText.Fixed((double)page.FileLength / page.VirtualSize, RatioDecimals);
            _output.WriteLine($"vaddr 0x{ReportText.Hex(page.VirtualAddress, AddressDigits)} codec {page.Codec} vsize {page.VirtualSize,SizeWidth} stored {page.FileLength,SizeWidth} ({ratio})");

            foreach (var lane in BcPackHeader.Parse(data))
            {
                var extra = string.Empty;

                if (checks.TryGetValue(lane.Lane, out var check))
                {
                    var symbols = check.Symbols.HasValue ? check.Symbols.Value.ToString(CultureInfo.InvariantCulture) : "None";
                    extra = $"  near-constant: {check.Status} (N={symbols})";
                }

                _output.WriteLine($"   lane{lane.Lane} mode {ReportText.Hex(lane.Mode, FlagDigits)} budget {lane.Budget,BudgetWidth} state {ReportText.Hex(lane.State, StateDigits)}{extra}");
            }

            printed++;

            if (limit > 0 && printed >= limit)
            {
                break;
            }
        }
    }

    /// <summary>Lane check results over every page file below a folder.</summary>
    public void BcPackStats(string root)
    {
        var verifier = new BcPackVerifier();
        var totals = new Dictionary<(int Codec, string Status), long>();

        foreach (var path in TextureReports.PageFiles(root))
        {
            using var source = new MappedFileSource(path);
            var file = new TexturePageFile(source, new ZlibInflater());

            foreach (var page in BcPackPagesOf(file))
            {
                var data = source.Read(page.FileOffset, (int)Math.Min(page.FileLength, HeaderReadLimit));

                foreach (var check in verifier.CheckPage(data, page.Codec, page.VirtualSize).Values)
                {
                    totals[(page.Codec, check.Status)] = totals.GetValueOrDefault((page.Codec, check.Status)) + 1;
                }
            }

            _output.WriteLine(Path.GetFileName(path));
            _output.Flush();
        }

        var keys = new List<(int Codec, string Status)>(totals.Keys);
        keys.Sort((left, right) => CompareKeys(left, right));

        foreach (var key in keys)
        {
            _output.WriteLine($"codec {key.Codec}  {key.Status,-StatusWidth} {totals[key]}");
        }
    }

    private static int CompareKeys((int Codec, string Status) left, (int Codec, string Status) right)
    {
        var byCodec = left.Codec.CompareTo(right.Codec);

        if (byCodec != 0)
        {
            return byCodec;
        }

        return string.CompareOrdinal(left.Status, right.Status);
    }

    private static List<TexturePage> BcPackPagesOf(TexturePageFile file)
    {
        var pages = new List<TexturePage>();

        foreach (var page in file.Pages)
        {
            if (page.IsBcPack && page.VirtualSize != 0)
            {
                pages.Add(page);
            }
        }

        return pages;
    }

    // An entry's stored bytes run to the next entry's offset; a later entry that does not move forward ends the
    // range at the end of the file.
    private static ReadOnlySpan<byte> StoredBytes(IByteSource source, IReadOnlyList<PageTableEntry> entries, int index)
    {
        var offset = entries[index].FileOffset;
        var end = source.Length;

        if (index + 1 < entries.Count && entries[index + 1].FileOffset >= offset)
        {
            end = entries[index + 1].FileOffset;
        }

        return source.Read(offset, (int)Math.Clamp(end - offset, 0, int.MaxValue));
    }

    private static string SpacedHex(string hex)
    {
        var pairs = new List<string>();

        for (var index = 0; index + 1 < hex.Length; index += FlagDigits)
        {
            pairs.Add(hex.Substring(index, FlagDigits));
        }

        return string.Join(' ', pairs);
    }
}
