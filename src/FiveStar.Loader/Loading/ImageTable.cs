// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using FiveStar.Loader.Native;

namespace FiveStar.Loader.Loading;

/// <summary>Every image this loader has mapped, in mapping order. Read lock-free by shims; replaced on change.</summary>
internal sealed class ImageTable
{
    private readonly object _gate = new();
    private LoadedImage[] _images = [];

    public IReadOnlyList<LoadedImage> All => Volatile.Read(ref _images);

    public void Add(LoadedImage image)
    {
        lock (_gate)
        {
            _images = [.. _images, image];
        }
    }

    public void Remove(LoadedImage image)
    {
        lock (_gate)
        {
            _images = _images.Where(i => i != image).ToArray();
        }
    }

    public LoadedImage? FindByBase(nint imageBase)
    {
        foreach (var image in All)
        {
            if (image.Base == imageBase)
            {
                return image;
            }
        }

        return null;
    }

    public LoadedImage? FindByName(string name)
    {
        foreach (var image in All)
        {
            if (string.Equals(image.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return image;
            }
        }

        return null;
    }

    public LoadedImage? FindByAddress(nint address)
    {
        foreach (var image in All)
        {
            if (image.Contains(address))
            {
                return image;
            }
        }

        return null;
    }

    /// <summary>"image.dll+0xoffset" for a mapped image, else the OS module the address belongs to.</summary>
    public string Describe(nint address)
    {
        var image = FindByAddress(address);

        if (image is null)
        {
            return SystemModules.Describe(address);
        }

        return $"{image.Name}+0x{(long)address - (long)image.Base:x}";
    }
}
