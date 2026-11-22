// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using FiveStar.Shaders.Hardware;

namespace FiveStar.Shaders.Cache;

/// <summary>
/// Finds the extracted FSSM programs of a cached shader. Both code blocks start with the same 24 bytes carrying the
/// shader hash; the code after them is compiled again by the driver and differs.
/// </summary>
public sealed class ProgramIndex
{
    /// <summary>Length of the hash prefix of a code block.</summary>
    public const int PrefixSize = 0x18;

    private const string ProgramPattern = "*.fssm";

    private readonly Dictionary<string, List<string>> _byPrefix;
    private readonly Dictionary<string, HardwareStage> _stages;

    private ProgramIndex(Dictionary<string, List<string>> byPrefix, Dictionary<string, HardwareStage> stages)
    {
        _byPrefix = byPrefix;
        _stages = stages;
    }

    /// <summary>Gets the number of programs indexed.</summary>
    public int Count => _stages.Count;

    /// <summary>An index without programs.</summary>
    public static ProgramIndex Empty()
    {
        return new ProgramIndex(new Dictionary<string, List<string>>(StringComparer.Ordinal), new Dictionary<string, HardwareStage>(StringComparer.Ordinal));
    }

    /// <summary>Indexes every <c>.fssm</c> program of a folder by its code block's hash prefix.</summary>
    public static ProgramIndex Load(string folder)
    {
        var byPrefix = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var stages = new Dictionary<string, HardwareStage>(StringComparer.Ordinal);

        foreach (var path in Directory.EnumerateFiles(folder, ProgramPattern).Order(StringComparer.Ordinal))
        {
            var program = File.ReadAllBytes(path);

            if (!FssmHeader.HasMagic(program))
            {
                continue;
            }

            var config = FssmHeader.Decode(program);
            var codeOffset = config.CodeOffset ?? program.Length;

            if (config.Stage is null || codeOffset + PrefixSize > program.Length)
            {
                continue;
            }

            var name = Path.GetFileNameWithoutExtension(path);
            var prefix = Convert.ToHexStringLower(program.AsSpan((int)codeOffset, PrefixSize));
            stages[name] = config.Stage.Value;
            byPrefix.TryAdd(prefix, []);
            byPrefix[prefix].Add(name);
        }

        return new ProgramIndex(byPrefix, stages);
    }

    /// <summary>The names (file stems) of the programs whose code starts like <paramref name="code"/>.</summary>
    public IReadOnlyList<string> Find(ReadOnlySpan<byte> code)
    {
        if (code.Length < PrefixSize)
        {
            return [];
        }

        if (_byPrefix.TryGetValue(Convert.ToHexStringLower(code[..PrefixSize]), out var names))
        {
            return names;
        }

        return [];
    }

    /// <summary>The hardware stage of an indexed program.</summary>
    public HardwareStage Stage(string name)
    {
        return _stages[name];
    }
}
