// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text;
using System.Text.Json;
using FiveStar.Shaders.Disassembly;
using FiveStar.Shaders.Translation;

namespace FiveStar.Shaders.Compilation;

/// <summary>
/// Translations kept in a <see cref="CacheFolder"/>. The key of a program is the hash of the translator identity (this
/// assembly, the LLVM tools and the translation options) and the FSSM and container bytes. The entry holds the stage,
/// status, reason and instruction count; its HLSL is stored once per distinct text.
/// </summary>
public sealed class TranslationCache
{
    private const string EntryKind = "programs";
    private const string TextKind = "hlsl";
    private const string EntryExtension = ".json";
    private const string TextExtension = ".hlsl";

    private readonly CacheFolder _folder;
    private readonly byte[] _identity;
    private int _hits;
    private int _misses;

    public TranslationCache(CacheFolder folder, string identity)
    {
        _folder = folder;
        _identity = Encoding.UTF8.GetBytes(identity);
    }

    public int Hits => Volatile.Read(ref _hits);

    public int Misses => Volatile.Read(ref _misses);

    /// <summary>The identity of this translator build with its disassembler and options.</summary>
    public static string Identity(LlvmToolPaths tools, TranslationOptions options)
    {
        var files = ContentHash.OfFiles([typeof(HlslTranslator).Assembly.Location, tools.Assembler, tools.Objdump]);

        return ContentHash.OfText(files, tools.Cpu, options.ToString());
    }

    public string Key(string programPath)
    {
        var container = ProgramLoader.ContainerPath(programPath);
        byte[] containerBytes = [];

        if (File.Exists(container))
        {
            containerBytes = File.ReadAllBytes(container);
        }

        return ContentHash.Of(_identity, File.ReadAllBytes(programPath), containerBytes);
    }

    /// <summary>The cached translation of a program under its name, or null.</summary>
    public TranslatedProgram? Find(string key, string name)
    {
        var entry = _folder.Read(EntryKind, key, EntryExtension);
        var program = entry is null ? null : Parse(entry, name);

        if (program is null)
        {
            Interlocked.Increment(ref _misses);

            return null;
        }

        Interlocked.Increment(ref _hits);

        return program;
    }

    public void Store(string key, TranslatedProgram program)
    {
        string? textKey = null;

        if (program.Hlsl is not null)
        {
            var text = Encoding.UTF8.GetBytes(program.Hlsl);
            textKey = ContentHash.Of(text);

            if (!_folder.Contains(TextKind, textKey, TextExtension))
            {
                _folder.Write(TextKind, textKey, TextExtension, text);
            }
        }

        _folder.Write(EntryKind, key, EntryExtension, Serialize(program, textKey));
    }

    private static byte[] Serialize(TranslatedProgram program, string? textKey)
    {
        using var memory = new MemoryStream();
        using var writer = new Utf8JsonWriter(memory);
        string? stage = null;

        if (program.Stage is not null)
        {
            stage = ShaderStages.ProfileName(program.Stage.Value);
        }

        writer.WriteStartObject();
        writer.WriteString("stage", stage);
        writer.WriteString("status", program.Status);
        writer.WriteString("reason", program.Reason);
        writer.WriteNumber("instructions", program.Instructions);
        writer.WriteString("hlsl", textKey);

        writer.WriteEndObject();
        writer.Flush();

        return memory.ToArray();
    }

    /// <summary>The entry as a program; null when its HLSL is missing from the cache.</summary>
    private TranslatedProgram? Parse(byte[] entry, string name)
    {
        using var document = JsonDocument.Parse(entry);
        var root = document.RootElement;
        var stageName = root.GetProperty("stage").GetString();
        var textKey = root.GetProperty("hlsl").GetString();
        string? hlsl = null;

        if (textKey is not null)
        {
            var text = _folder.Read(TextKind, textKey, TextExtension);

            if (text is null)
            {
                return null;
            }

            hlsl = Encoding.UTF8.GetString(text);
        }

        ShaderStage? stage = stageName is null ? null : ShaderStages.FromProfileName(stageName);
        var status = root.GetProperty("status").GetString();
        var reason = root.GetProperty("reason").GetString();

        return new TranslatedProgram(name, stage, hlsl, status, reason, root.GetProperty("instructions").GetInt32());
    }
}
