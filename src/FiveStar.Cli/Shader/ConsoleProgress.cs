// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Diagnostics;
using System.Globalization;
using FiveStar.Shaders.Compilation;

namespace FiveStar.Cli.Shader;

/// <summary>
/// Prints a corpus run's progress every 10 seconds and at the end: programs done, elapsed time, the time left at the
/// rate of the last 5 minutes (cached programs finish much faster than new ones), the estimated finish and a detail
/// from the caller (cache counts).
/// </summary>
internal sealed class ConsoleProgress : IProgress<CorpusProgress>
{
    private const int PercentScale = 100;

    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan RateWindow = TimeSpan.FromMinutes(5);

    private readonly Func<string> _detail;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly Lock _gate = new();
    private readonly Queue<(TimeSpan Time, int Done)> _samples = new([(TimeSpan.Zero, 0)]);
    private TimeSpan _printed;

    public ConsoleProgress(Func<string> detail)
    {
        _detail = detail;
    }

    public void Report(CorpusProgress value)
    {
        var elapsed = _clock.Elapsed;
        (TimeSpan Time, int Done) oldest;

        lock (_gate)
        {
            if (elapsed - _printed < Interval && value.Done != value.Total)
            {
                return;
            }

            _printed = elapsed;
            _samples.Enqueue((elapsed, value.Done));

            while (_samples.Count > 1 && elapsed - _samples.Peek().Time > RateWindow)
            {
                _samples.Dequeue();
            }

            oldest = _samples.Peek();
        }

        var rate = Math.Max(1, value.Done - oldest.Done) / Math.Max(1, (elapsed - oldest.Time).TotalSeconds);
        var left = TimeSpan.FromSeconds((value.Total - value.Done) / rate);
        var percent = (double)value.Done * PercentScale / value.Total;
        var line = string.Create(
            CultureInfo.InvariantCulture,
            $"{value.Done}/{value.Total} ({percent:F1}%)  elapsed {Format(elapsed)}  left ~{Format(left)}  finish ~{DateTime.Now + left:HH:mm}  {_detail()}");
        Console.WriteLine(line);
    }

    private static string Format(TimeSpan time)
    {
        return time.ToString(@"h\:mm\:ss", CultureInfo.InvariantCulture);
    }
}
