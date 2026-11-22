// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.IO.Compression;

namespace FiveStar.Shaders.Compilation;

/// <summary>
/// The shader cache on disk: one Brotli-compressed file per entry at <c>&lt;kind&gt;/&lt;key[0..2]&gt;/&lt;key&gt;&lt;extension&gt;</c>.
/// An entry is written to a temporary file and moved into place, so a reader never sees a partial entry and an
/// interrupted run leaves only complete ones.
/// </summary>
public sealed class CacheFolder
{
    private const int ShardLength = 2;
    private const string TemporaryExtension = ".tmp";

    private readonly string _root;

    public CacheFolder(string root)
    {
        _root = root;
    }

    public string Root => _root;

    public bool Contains(string kind, string key, string extension)
    {
        return File.Exists(EntryPath(kind, key, extension));
    }

    /// <summary>The entry's bytes, or null when there is none.</summary>
    public byte[]? Read(string kind, string key, string extension)
    {
        var path = EntryPath(kind, key, extension);

        if (!File.Exists(path))
        {
            return null;
        }

        using var file = File.OpenRead(path);
        using var brotli = new BrotliStream(file, CompressionMode.Decompress);
        using var memory = new MemoryStream();
        brotli.CopyTo(memory);

        return memory.ToArray();
    }

    public void Write(string kind, string key, string extension, ReadOnlySpan<byte> data)
    {
        var path = EntryPath(kind, key, extension);
        var temporary = $"{path}.{Guid.NewGuid():N}{TemporaryExtension}";
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        Compress(temporary, data);

        try
        {
            File.Move(temporary, path, overwrite: true);
        }
        catch (IOException)
        {
            // Another writer stored the same entry and a reader holds it open; its content is the same.
            File.Delete(temporary);
        }
    }

    /// <summary>The total size of the entries, in bytes.</summary>
    public long Size()
    {
        if (!Directory.Exists(_root))
        {
            return 0;
        }

        return new DirectoryInfo(_root).EnumerateFiles("*", SearchOption.AllDirectories).Sum(file => file.Length);
    }

    private static void Compress(string path, ReadOnlySpan<byte> data)
    {
        using var file = File.Create(path);
        using var brotli = new BrotliStream(file, CompressionLevel.Fastest);
        brotli.Write(data);
    }

    private string EntryPath(string kind, string key, string extension)
    {
        return Path.Combine(_root, kind, key[..ShardLength], key + extension);
    }
}
