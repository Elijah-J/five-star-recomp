// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using FiveStar.Formats.Common;
using FiveStar.Formats.Compression;
using FiveStar.Formats.Ttpf;
using FiveStar.Formats.Ttpf.BcPack;

namespace FiveStar.Cli.Texture;

/// <summary>The BCPack BC1 pages of a set of page files, and the decoder engine the command asked for.</summary>
internal static class Bc1PageSet
{
    private const string PagePattern = "*.ttpf";

    /// <summary>Every page file given or below a folder given, sorted.</summary>
    public static List<string> Files(IReadOnlyList<string> paths)
    {
        var files = new List<string>();

        foreach (var path in paths)
        {
            if (Directory.Exists(path))
            {
                files.AddRange(Directory.EnumerateFiles(path, PagePattern, SearchOption.AllDirectories));
            }
            else if (File.Exists(path))
            {
                files.Add(path);
            }
            else
            {
                throw new FileNotFoundException("no such page file or folder: " + path);
            }
        }

        files.Sort(StringComparer.OrdinalIgnoreCase);

        return files;
    }

    /// <summary>The BC1 pages of each file, every <paramref name="every"/>-th one per file.</summary>
    public static List<(int File, TexturePage Page)> Pages(IReadOnlyList<string> files, long every)
    {
        var pages = new List<(int File, TexturePage Page)>();

        for (var index = 0; index < files.Count; index++)
        {
            using var source = new FileByteSource(files[index]);
            var file = new TexturePageFile(source, new ZlibInflater());
            long seen = 0;

            foreach (var page in file.Pages)
            {
                if (page.IsBcPack && page.Codec == TexturePage.Bc1Codec && seen++ % every == 0)
                {
                    pages.Add((index, page));
                }
            }
        }

        return pages;
    }

    /// <summary>The engine named by <c>--engine avx512|avx2|scalar</c>, or the fastest one this processor runs.</summary>
    public static Bc1Engine Engine(string? name)
    {
        if (name is null)
        {
            return Bc1Decoder.DefaultEngine;
        }

        if (!Enum.TryParse<Bc1Engine>(name, ignoreCase: true, out var engine) || !Enum.IsDefined(engine))
        {
            throw new ArgumentException($"unknown engine {name} (avx512, avx2, scalar)");
        }

        if (!Bc1Decoder.IsSupported(engine))
        {
            throw new ArgumentException($"this processor cannot run the {name} engine");
        }

        return engine;
    }

    public static string EngineName(Bc1Engine engine)
    {
        switch (engine)
        {
            case Bc1Engine.Avx512:
                return "AVX-512";
            case Bc1Engine.Avx2:
                return "AVX2";
            default:
                return "scalar";
        }
    }
}
