// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using FiveStar.Cli.Archive;
using FiveStar.Formats.Audio;
using FiveStar.Formats.Common;

namespace FiveStar.Cli.Audio;

/// <summary>The <c>fivestar audio</c> reports: AWC coverage of an archive and export of playable files.</summary>
internal sealed class AudioReports
{
    private const int OffsetDigits = 10;
    private const int IdDigits = 8;
    private const int TwoDecimals = 2;
    private const int FlagDigits = 4;
    private const int ProblemsShown = 20;
    private const double Percent = 100;
    private const double BytesPerGibibyte = 1 << 30;

    private readonly TextWriter _output;

    public AudioReports(TextWriter output)
    {
        _output = output;
    }

    /// <summary>Reads every container and track and prints the coverage per codec.</summary>
    public void Scan(string path, CommandArguments arguments)
    {
        using var source = new MappedFileSource(path);
        var limit = arguments.LongOption("--limit", long.MaxValue);
        var reader = new AwcTrackReader(source);
        var coverage = new CodecCoverage();

        var flags = new Tally<string>();
        var rates = new Tally<string>();
        var unclean = 0;
        long containers = 0;
        long bytes = 0;

        foreach (var container in new AwcScanner(source).Scan(0).TakeWhile(_ => containers < limit))
        {
            containers++;
            bytes += container.Size;
            flags.Add(ReportText.Hex(container.Flags, FlagDigits));

            foreach (var track in reader.Read(container))
            {
                coverage.Add(track);
                rates.Add($"{CodecCoverage.CodecName(track)} {track.Format.SampleRate} Hz {track.Format.Channels} ch");

                if (!CodecCoverage.IsClean(track))
                {
                    ReportSkipped(track, ++unclean);
                }
            }
        }

        var share = DecimalText.Fixed(bytes * Percent / source.Length, TwoDecimals);
        _output.WriteLine($"{containers} containers, {DecimalText.Fixed(bytes / BytesPerGibibyte, TwoDecimals)} GiB ({share}% of the archive)");
        _output.WriteLine("flags " + ReportText.CountMap(flags));
        coverage.Print(_output);
        _output.WriteLine("sample-rate fields " + ReportText.CountMap(rates.MostCommon()));
    }

    /// <summary>Writes every track (the first <c>--limit</c>, of codec <c>--codec</c>, from <c>--from</c>) under <paramref name="outputDirectory"/>.</summary>
    public void Export(string path, string outputDirectory, CommandArguments arguments)
    {
        using var source = new MappedFileSource(path);
        var limit = arguments.LongOption("--limit", long.MaxValue);
        var reader = new AwcTrackReader(source);
        var written = new CodecCoverage();

        var codec = arguments.Option("--codec");
        var skipped = 0;
        long tracks = 0;

        foreach (var container in new AwcScanner(source).Scan(StartOffset(arguments)))
        {
            var folder = Path.Combine(outputDirectory, ReportText.Hex(container.Offset, OffsetDigits));
            var seen = new Tally<uint>();

            foreach (var track in reader.Read(container).TakeWhile(_ => tracks < limit))
            {
                if (codec is not null && CodecCoverage.CodecName(track) != codec)
                {
                    continue;
                }

                tracks++;

                if (!CodecCoverage.IsClean(track) && Enum.IsDefined(track.Codec))
                {
                    ReportSkipped(track, ++skipped);

                    continue;
                }

                Directory.CreateDirectory(folder);
                WriteTrack(source, track, Path.Combine(folder, FileStem(track.Id, seen[track.Id])));
                seen.Add(track.Id);
                written.Add(track);
            }

            if (tracks >= limit)
            {
                break;
            }
        }

        _output.WriteLine($"{tracks} tracks read, {skipped} skipped; written:");
        written.Print(_output);
    }

    /// <summary>The <c>--from</c> archive offset, in hex like the folder names, or 0.</summary>
    private static long StartOffset(CommandArguments arguments)
    {
        var from = arguments.Option("--from");

        if (from is null)
        {
            return 0;
        }

        return long.Parse(from, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
    }

    /// <summary>The stream id in hex, with a counter when a container repeats an id.</summary>
    private static string FileStem(uint id, long repeat)
    {
        var stem = ReportText.Hex(id, IdDigits);

        if (repeat > 0)
        {
            stem += "_" + ReportText.Number(repeat);
        }

        return stem;
    }

    private static void WriteTrack(IByteSource source, AwcTrack track, string name)
    {
        if (track.Codec == AwcCodec.Opus)
        {
            using var file = File.Create(name + ".opus");
            OggOpusWriter.Write(file, source, track, track.Id);
        }
        else if (WaveWriter.Supports(track.Codec))
        {
            using var file = File.Create(name + ".wav");
            WaveWriter.Write(file, track.Format, source.Read(track.DataOffset, (int)track.DataSize));
        }
        else
        {
            File.WriteAllBytes($"{name}.{CodecCoverage.CodecName(track)}.raw", source.Read(track.DataOffset, (int)track.DataSize).ToArray());
        }
    }

    private void ReportSkipped(AwcTrack track, int count)
    {
        if (count <= ProblemsShown)
        {
            var where = ReportText.Hex(track.ContainerOffset, OffsetDigits);
            _output.WriteLine($"problem {where}/{ReportText.Hex(track.Id, IdDigits)}: {track.Problem ?? "short packets"}");
        }
    }
}
