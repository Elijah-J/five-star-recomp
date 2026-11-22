// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Runtime.InteropServices;
using FiveStar.Loader.Diagnostics;
using FiveStar.Loader.Shims.Gdk;

namespace FiveStar.Loader.Mods;

/// <summary>
/// Mod folders laid over the package root. A path the game opens inside the package root is redirected to the same
/// relative path in the first mod folder that has that file; every other path is left alone.
/// </summary>
internal sealed unsafe class FileOverlay
{
    /// <summary>The Win32 prefix that turns off path normalization (<c>\\?\C:\...</c>).</summary>
    private const string ExtendedPathPrefix = @"\\?\";

    private readonly GdkConfig _config;
    private readonly LoaderLog _log;
    private readonly List<string> _folders = [];

    public FileOverlay(GdkConfig config, LoaderLog log)
    {
        _config = config;
        _log = log;
    }

    /// <summary>The mod folders, highest priority first.</summary>
    public IReadOnlyList<string> Folders => _folders;

    /// <summary>Adds a mod folder below the ones already added; set up before mapped code runs.</summary>
    public void AddFolder(string folder)
    {
        _folders.Add(Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder)));
    }

    /// <summary>The mod file for a UTF-16 path, or null to open the path as given.</summary>
    public string? Redirect(char* path, string function)
    {
        if (_folders.Count == 0 || path == null)
        {
            return null;
        }

        return Redirect(new string(path), function);
    }

    /// <summary>The mod file for an ANSI path, or null to open the path as given.</summary>
    public string? Redirect(byte* path, string function)
    {
        if (_folders.Count == 0 || path == null)
        {
            return null;
        }

        return Redirect(Marshal.PtrToStringAnsi((nint)path), function);
    }

    /// <summary>The mod file for <paramref name="path"/>, or null to open the path as given.</summary>
    public string? Redirect(string? path, string function)
    {
        if (_folders.Count == 0 || string.IsNullOrEmpty(path))
        {
            return null;
        }

        string? relative = RelativeToPackage(path);

        if (relative is null)
        {
            return null;
        }

        foreach (string folder in _folders)
        {
            string candidate = Path.Combine(folder, relative);

            if (File.Exists(candidate))
            {
                _log.Detail($"  [overlay] {function}(\"{path}\") -> \"{candidate}\"");

                return candidate;
            }
        }

        return null;
    }

    private string? RelativeToPackage(string path)
    {
        string plain = path;

        if (plain.StartsWith(ExtendedPathPrefix, StringComparison.Ordinal))
        {
            plain = plain[ExtendedPathPrefix.Length..];
        }

        string full;

        try
        {
            full = Path.GetFullPath(plain);
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }

        string root = Path.TrimEndingDirectorySeparator(_config.PackageRoot);

        if (full.Length <= root.Length + 1 || !full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (full[root.Length] != Path.DirectorySeparatorChar && full[root.Length] != Path.AltDirectorySeparatorChar)
        {
            return null;
        }

        return full[(root.Length + 1)..];
    }
}
