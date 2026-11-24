// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;

namespace FiveStar.Loader.Shims.Input;

/// <summary>
/// The <c>--input-script</c> file: key presses at fixed times after the loader started, one per line,
/// <c>&lt;seconds&gt; &lt;key&gt; [hold &lt;s&gt;] [repeat &lt;n&gt; every &lt;s&gt;]</c>, '#' starting a comment. Keys are
/// the names of the input config; a scripted key reaches the game like a held desktop key, so the keyboard and the
/// keyboard-and-mouse gamepad both see it (Space is A, Escape is Menu). <c>capture</c> in place of a key reads back
/// the next present at that time (with <c>--capture-dir</c>); <c>cursor &lt;dx&gt; &lt;dy&gt;</c> moves the cursor that
/// many pixels a second for the hold time (the gamepad's right stick, full at 1000). A time written <c>+&lt;s&gt;</c>
/// is that many seconds after the previous line's time.
/// </summary>
internal sealed class InputScript
{
    private const char CommentMark = '#';
    private const char RelativeMark = '+';
    private const double DefaultHold = 0.2;
    private const string HoldWord = "hold";
    private const string RepeatWord = "repeat";
    private const string EveryWord = "every";
    private const string CaptureWord = "capture";
    private const string CursorWord = "cursor";

    /// <summary>A line starts with the time and the key; the options follow as word and value pairs.</summary>
    private const int FixedFields = 2;
    private const int OptionFields = 2;

    /// <summary>A cursor line has the two speeds after the word.</summary>
    private const int SpeedXField = 2;
    private const int SpeedYField = 3;
    private const int CursorFields = 4;

    private InputScript(IReadOnlyList<ScriptedPress> presses, IReadOnlyList<ScriptedMotion> motions, IReadOnlyList<double> captures)
    {
        Presses = presses;
        Motions = motions;
        Captures = captures;
    }

    /// <summary>Every press, in start order.</summary>
    public IReadOnlyList<ScriptedPress> Presses { get; }

    /// <summary>Every cursor motion.</summary>
    public IReadOnlyList<ScriptedMotion> Motions { get; }

    /// <summary>The times of the <c>capture</c> lines, in order.</summary>
    public IReadOnlyList<double> Captures { get; }

    public static InputScript Load(string path)
    {
        return Parse(File.ReadLines(path));
    }

    public static InputScript Parse(IEnumerable<string> lines)
    {
        var presses = new List<ScriptedPress>();
        var motions = new List<ScriptedMotion>();
        var captures = new List<double>();
        double previous = 0;

        foreach (string raw in lines)
        {
            string[] fields = raw.Split(CommentMark)[0].Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

            if (fields.Length == 0)
            {
                continue;
            }

            if (fields.Length < FixedFields)
            {
                throw new FormatException($"input script: '{string.Join(' ', fields)}' needs a time and a key");
            }

            previous = LineTime(fields[0], previous);
            fields[0] = previous.ToString(CultureInfo.InvariantCulture);

            if (fields[1].Equals(CaptureWord, StringComparison.OrdinalIgnoreCase))
            {
                captures.Add(Number(fields[0]));
            }
            else if (fields[1].Equals(CursorWord, StringComparison.OrdinalIgnoreCase))
            {
                motions.AddRange(ParseMotion(fields));
            }
            else
            {
                presses.AddRange(ParseLine(fields));
            }
        }

        captures.Sort();

        return new InputScript([.. presses.OrderBy(p => p.Start)], motions, captures);
    }

    /// <summary>How far the scripted cursor motions moved the cursor by <paramref name="seconds"/>, in pixels.</summary>
    [HotPath]
    public CursorPosition CursorOffset(double seconds)
    {
        double x = 0;
        double y = 0;

        for (int i = 0; i < Motions.Count; i++)
        {
            double elapsed = Motions[i].Elapsed(seconds);
            x += elapsed * Motions[i].PerSecondX;
            y += elapsed * Motions[i].PerSecondY;
        }

        return new CursorPosition((long)x, (long)y);
    }

    /// <summary>Marks the scripted keys held at <paramref name="seconds"/> as down.</summary>
    [HotPath]
    public void Apply(PressedKeys keys, double seconds)
    {
        for (int i = 0; i < Presses.Count; i++)
        {
            if (Presses[i].IsDown(seconds))
            {
                keys.Set(Presses[i].Key, true);
            }
        }
    }

    /// <summary>How many presses started before <paramref name="seconds"/>.</summary>
    [HotPath]
    public int StartedBefore(double seconds)
    {
        int count = 0;

        while (count < Presses.Count && Presses[count].Start <= seconds)
        {
            count++;
        }

        return count;
    }

    private static IEnumerable<ScriptedPress> ParseLine(string[] fields)
    {
        int key = KeyNames.Parse(fields[1]) ?? throw new FormatException("input script: 'none' is not a key");
        var (start, hold, repeat, every) = Timing(fields, FixedFields);

        return Enumerable.Range(0, repeat).Select(n => new ScriptedPress(start + (n * every), hold, key, fields[1]));
    }

    private static IEnumerable<ScriptedMotion> ParseMotion(string[] fields)
    {
        if (fields.Length < CursorFields)
        {
            throw new FormatException("input script: cursor needs two speeds, x and y in pixels a second");
        }

        double perSecondX = Number(fields[SpeedXField]);
        double perSecondY = Number(fields[SpeedYField]);
        var (start, hold, repeat, every) = Timing(fields, CursorFields);

        return Enumerable.Range(0, repeat).Select(n => new ScriptedMotion(start + (n * every), hold, perSecondX, perSecondY));
    }

    /// <summary>The line's time and its hold, repeat and every options, which start at field <paramref name="first"/>.</summary>
    private static (double Start, double Hold, int Repeat, double Every) Timing(string[] fields, int first)
    {
        double start = Number(fields[0]);
        double hold = DefaultHold;
        int repeat = 1;
        double every = 0;

        for (int i = first; i + 1 < fields.Length; i += OptionFields)
        {
            switch (fields[i].ToLowerInvariant())
            {
                case HoldWord:
                    hold = Number(fields[i + 1]);
                    break;
                case RepeatWord:
                    repeat = int.Parse(fields[i + 1], CultureInfo.InvariantCulture);
                    break;
                case EveryWord:
                    every = Number(fields[i + 1]);
                    break;
                default:
                    throw new FormatException($"input script: unknown word '{fields[i]}'");
            }
        }

        return (start, hold, repeat, every);
    }

    /// <summary>The line's time: absolute, or after <paramref name="previous"/> when it starts with '+'.</summary>
    private static double LineTime(string text, double previous)
    {
        if (text[0] == RelativeMark)
        {
            return previous + Number(text[1..]);
        }

        return Number(text);
    }

    private static double Number(string text)
    {
        return double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
    }
}
