// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;

namespace FiveStar.Loader.Shims.Input;

/// <summary>
/// The <c>--input-script</c> file: key presses at fixed times after the loader started, one per line,
/// <c>&lt;seconds&gt; &lt;key&gt; [hold &lt;s&gt;] [repeat &lt;n&gt; every &lt;s&gt;]</c>, '#' starting a comment. Keys are
/// the names of the input config; a scripted key reaches the game like a held desktop key, so the keyboard and the
/// keyboard-and-mouse gamepad both see it (Space is A, Escape is Menu).
/// </summary>
internal sealed class InputScript
{
    private const char CommentMark = '#';
    private const double DefaultHold = 0.2;
    private const string HoldWord = "hold";
    private const string RepeatWord = "repeat";
    private const string EveryWord = "every";

    /// <summary>A line starts with the time and the key; the options follow as word and value pairs.</summary>
    private const int FixedFields = 2;
    private const int OptionFields = 2;

    private InputScript(IReadOnlyList<ScriptedPress> presses)
    {
        Presses = presses;
    }

    /// <summary>Every press, in start order.</summary>
    public IReadOnlyList<ScriptedPress> Presses { get; }

    public static InputScript Load(string path)
    {
        return Parse(File.ReadLines(path));
    }

    public static InputScript Parse(IEnumerable<string> lines)
    {
        var presses = new List<ScriptedPress>();

        foreach (string raw in lines)
        {
            string[] fields = raw.Split(CommentMark)[0].Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

            if (fields.Length > 0)
            {
                presses.AddRange(ParseLine(fields));
            }
        }

        return new InputScript([.. presses.OrderBy(p => p.Start)]);
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

    private static IEnumerable<ScriptedPress> ParseLine(string[] fields)
    {
        if (fields.Length < FixedFields)
        {
            throw new FormatException($"input script: '{string.Join(' ', fields)}' needs a time and a key");
        }

        double start = Number(fields[0]);
        int key = KeyNames.Parse(fields[1]) ?? throw new FormatException("input script: 'none' is not a key");
        double hold = DefaultHold;
        int repeat = 1;
        double every = 0;

        for (int i = FixedFields; i + 1 < fields.Length; i += OptionFields)
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

        return Enumerable.Range(0, repeat).Select(n => new ScriptedPress(start + (n * every), hold, key, fields[1]));
    }

    private static double Number(string text)
    {
        return double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
    }
}
