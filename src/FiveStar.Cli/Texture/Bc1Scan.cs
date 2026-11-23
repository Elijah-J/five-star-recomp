// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using FiveStar.Cli.Archive;
using FiveStar.Formats.Common;
using FiveStar.Formats.Ttpf;
using FiveStar.Formats.Ttpf.BcPack;
using Microsoft.Win32.SafeHandles;

namespace FiveStar.Cli.Texture;

/// <summary>
/// <c>fivestar texture bcpack-scan</c>: decodes every BCPack BC1 page of the given page files on all cores, then
/// reports convergence per file and in total with the decode speed. <c>--hashes</c> writes one line per page
/// (path, virtual address, status, SHA-256 of the blocks, unfitted blocks) for comparing engines or builds.
/// </summary>
internal sealed class Bc1Scan
{
    private const int FileNameWidth = 36;
    private const int CountWidth = 7;
    private const int FailedWidth = 5;
    private const int ListedFailures = 20;
    private const double BytesPerMegabyte = 1e6;
    private const double BytesPerGigabyte = 1e9;
    private const int TwoDecimals = 2;
    private const string NoHash = "-";

    private readonly TextWriter _output;

    public Bc1Scan(TextWriter output)
    {
        _output = output;
    }

    /// <summary>Scans the pages; false when any page failed.</summary>
    public bool Run(IReadOnlyList<string> paths, CommandArguments arguments)
    {
        var files = Bc1PageSet.Files(paths);
        var engine = Bc1PageSet.Engine(arguments.Option("--engine"));
        var jobs = (int)Math.Max(1, arguments.LongOption("--jobs", Environment.ProcessorCount));
        var pages = Bc1PageSet.Pages(files, Math.Max(1, arguments.LongOption("--every", 1)));
        var hashes = arguments.Option("--hashes");

        var clock = Stopwatch.StartNew();
        var outcomes = DecodeAll(files, pages, engine, jobs, hashes is not null);
        var wall = clock.Elapsed.TotalSeconds;

        var tallies = new Bc1ScanTally[files.Count];
        var failures = new List<string>();
        long ticks = 0;

        for (var index = 0; index < files.Count; index++)
        {
            tallies[index] = new Bc1ScanTally();
        }

        for (var index = 0; index < pages.Count; index++)
        {
            var (file, page) = pages[index];
            var result = outcomes[index].Result;
            tallies[file].Add(result, page.FileLength, page.VirtualSize);
            ticks += outcomes[index].Ticks;

            if (!result.Success)
            {
                failures.Add($"  {Path.GetFileName(files[file])} vaddr 0x{page.VirtualAddress:x} vsize {page.VirtualSize}: {Describe(page, result)}");
            }
        }

        var total = PrintFiles(files, tallies, arguments.HasFlag("--quiet"));
        PrintFailures(failures);
        PrintTotals(engine, files.Count, jobs, total, wall, ticks / (double)Stopwatch.Frequency);

        if (hashes is not null)
        {
            File.WriteAllLines(hashes, HashLines(files, pages, outcomes));
        }

        return failures.Count == 0;
    }

    private static Bc1PageOutcome[] DecodeAll(List<string> files, List<(int File, TexturePage Page)> pages, Bc1Engine engine, int jobs, bool hash)
    {
        var handles = new SafeFileHandle[files.Count];
        var outcomes = new Bc1PageOutcome[pages.Count];

        try
        {
            for (var index = 0; index < files.Count; index++)
            {
                handles[index] = File.OpenHandle(files[index], options: FileOptions.RandomAccess);
            }

            var parallel = new ParallelOptions { MaxDegreeOfParallelism = jobs };
            Parallel.For(0, pages.Count, parallel, () => new Bc1ScanBuffers(), (index, loop, buffers) =>
            {
                outcomes[index] = DecodePage(handles[pages[index].File], pages[index].Page, engine, hash, buffers);

                return buffers;
            }, KeepBuffers);
        }
        finally
        {
            foreach (var handle in handles)
            {
                handle?.Dispose();
            }
        }

        return outcomes;
    }

    /// <summary>Reads one page's stored bytes and decodes them; the time is the decode's alone.</summary>
    private static Bc1PageOutcome DecodePage(SafeFileHandle file, TexturePage page, Bc1Engine engine, bool hash, Bc1ScanBuffers buffers)
    {
        var length = (int)page.FileLength;
        var blockBytes = page.Bc1Blocks * Bc1Decoder.BlockSize;
        buffers.Reserve(length, blockBytes);
        var chunk = buffers.Stored.AsSpan(0, length);
        RandomAccess.Read(file, chunk, page.FileOffset);

        var start = Stopwatch.GetTimestamp();
        var result = new Bc1DecodeResult(Bc1DecodeStatus.InvalidArgument, 0, length, 0, 0, 0);

        if (page.Bc1Blocks > 0)
        {
            result = Bc1Decoder.Decode(chunk, page.Bc1Blocks, buffers.Blocks, engine);
        }

        var ticks = Stopwatch.GetTimestamp() - start;
        string? digest = null;

        if (hash && result.Success)
        {
            digest = Convert.ToHexStringLower(SHA256.HashData(buffers.Blocks.AsSpan(0, blockBytes)));
        }

        return new Bc1PageOutcome(result, ticks, digest);
    }

    /// <summary>A worker's buffers live until the scan ends.</summary>
    private static void KeepBuffers(Bc1ScanBuffers buffers)
    {
    }

    private static string Describe(TexturePage page, Bc1DecodeResult result)
    {
        if (page.Bc1Blocks == 0)
        {
            return $"page virtual size {page.VirtualSize} is not a whole number of BC1 blocks";
        }

        return result.Describe();
    }

    private static List<string> HashLines(List<string> files, List<(int File, TexturePage Page)> pages, Bc1PageOutcome[] outcomes)
    {
        var lines = new List<string>(pages.Count);

        for (var index = 0; index < pages.Count; index++)
        {
            var (file, page) = pages[index];
            var result = outcomes[index].Result;
            var status = result.Success ? "ok" : result.Status.ToString();
            var address = page.VirtualAddress.ToString("x", CultureInfo.InvariantCulture);
            lines.Add($"{Path.GetFullPath(files[file])}\t{address}\t{status}\t{outcomes[index].Hash ?? NoHash}\t{result.UnfittedBlocks}");
        }

        return lines;
    }

    private Bc1ScanTally PrintFiles(List<string> files, Bc1ScanTally[] tallies, bool quiet)
    {
        var total = new Bc1ScanTally();

        for (var index = 0; index < files.Count; index++)
        {
            var tally = tallies[index];
            total.Merge(tally);

            if (quiet || tally.Pages == 0)
            {
                continue;
            }

            var megabytes = DecimalText.Fixed(tally.BlockBytes / BytesPerMegabyte, 1);
            _output.WriteLine($"{Path.GetFileName(files[index]),-FileNameWidth} {tally.Converged,CountWidth} converged {tally.Failed,FailedWidth} failed {tally.Unfitted,FailedWidth} unfitted-slot pages {megabytes} MB");
        }

        return total;
    }

    private void PrintFailures(List<string> failures)
    {
        failures.Sort(StringComparer.Ordinal);

        foreach (var line in failures.Take(ListedFailures))
        {
            _output.WriteLine(line);
        }
    }

    private void PrintTotals(Bc1Engine engine, int files, int jobs, Bc1ScanTally total, double wall, double decodeSeconds)
    {
        var blockMegabytes = total.BlockBytes / BytesPerMegabyte;
        var outGigabytes = DecimalText.Fixed(total.BlockBytes / BytesPerGigabyte, TwoDecimals);
        var storedGigabytes = DecimalText.Fixed(total.StoredBytes / BytesPerGigabyte, TwoDecimals);
        var wallRate = DecimalText.Fixed(blockMegabytes / wall, 0);
        var threadRate = DecimalText.Fixed(blockMegabytes / decodeSeconds, 1);

        _output.WriteLine($"{Bc1PageSet.EngineName(engine)} engine: {files} files, {total.Pages} BC1 pages: {total.Converged} converged, {total.Failed} failed; {total.Unfitted} pages touch an unfitted window slot");
        _output.WriteLine($"  {outGigabytes} GB BC1 from {storedGigabytes} GB stored; wall {DecimalText.Fixed(wall, 1)} s on {jobs} threads = {wallRate} MB/s with reads; decode time over all threads {DecimalText.Fixed(decodeSeconds, 1)} s = {threadRate} MB/s per thread");
    }
}
