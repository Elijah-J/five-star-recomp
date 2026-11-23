// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using FiveStar.Loader.Native;

namespace FiveStar.Loader.Shims.D3D12X.Resources;

/// <summary>
/// The desktop textures standing for console GPU address ranges, sorted by address. Lookups read the current array
/// without a lock; registering replaces it. A new texture retires every texture whose range it overlaps (the memory
/// was given to something else). Retired textures keep their reference until disposal: descriptors the game wrote
/// earlier may still name them, and nothing tells when the last one is overwritten.
/// </summary>
internal sealed class GpuTextureRegistry : IDisposable
{
    private readonly object _gate = new();
    private readonly List<GpuTexture> _retired = [];
    private GpuTexture[] _sorted = [];

    public int Count => Volatile.Read(ref _sorted).Length;

    /// <summary>Textures replaced by an overlapping one and kept alive.</summary>
    public int RetiredCount
    {
        get
        {
            lock (_gate)
            {
                return _retired.Count;
            }
        }
    }

    public void Register(GpuTexture texture)
    {
        lock (_gate)
        {
            var kept = new List<GpuTexture>(_sorted.Length + 1);

            foreach (var entry in _sorted)
            {
                if (entry.Address < texture.Address + texture.Size && texture.Address < entry.Address + entry.Size)
                {
                    _retired.Add(entry);
                }
                else
                {
                    kept.Add(entry);
                }
            }

            kept.Add(texture);
            kept.Sort((left, right) => left.Address.CompareTo(right.Address));
            Volatile.Write(ref _sorted, kept.ToArray());
        }
    }

    /// <summary>The texture whose range holds <paramref name="address"/>, or null.</summary>
    [HotPath]
    public GpuTexture? Find(ulong address)
    {
        var sorted = Volatile.Read(ref _sorted);
        int low = 0;
        int high = sorted.Length - 1;

        while (low <= high)
        {
            int middle = low + ((high - low) >> 1);
            var entry = sorted[middle];

            if (entry.Contains(address))
            {
                return entry;
            }

            if (address < entry.Address)
            {
                high = middle - 1;
            }
            else
            {
                low = middle + 1;
            }
        }

        return null;
    }

    public void Dispose()
    {
        lock (_gate)
        {
            foreach (var entry in _sorted.Concat(_retired))
            {
                ComObject.Release(entry.Desktop);
            }

            _retired.Clear();
            _sorted = [];
        }
    }
}
