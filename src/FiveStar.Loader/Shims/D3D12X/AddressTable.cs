// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

namespace FiveStar.Loader.Shims.D3D12X;

/// <summary>
/// Translates game addresses to desktop ones by range: console GPU addresses (on the console, GPU address == CPU
/// address) to desktop GPU addresses, and descriptor handles in game memory to desktop descriptor handles. A new range
/// replaces the ones it overlaps. Lookups read a sorted array that is replaced on each addition, so they take no lock.
/// </summary>
internal sealed class AddressTable
{
    private readonly object _gate = new();
    private AddressRange[] _ranges = [];

    public void Add(AddressRange range)
    {
        lock (_gate)
        {
            var ranges = _ranges.Where(r => r.GameAddress + r.Size <= range.GameAddress || range.GameAddress + range.Size <= r.GameAddress)
                .Append(range)
                .OrderBy(r => r.GameAddress)
                .ToArray();
            Volatile.Write(ref _ranges, ranges);
        }
    }

    /// <summary>The desktop address of <paramref name="gameAddress"/>; false outside every known range.</summary>
    [HotPath]
    public bool TryTranslate(ulong gameAddress, out ulong desktopAddress)
    {
        desktopAddress = 0;

        if (!TryFind(gameAddress, out var range))
        {
            return false;
        }

        desktopAddress = range.DesktopAddress + (gameAddress - range.GameAddress);

        return true;
    }

    /// <summary>The range holding <paramref name="gameAddress"/>; false outside every known range.</summary>
    [HotPath]
    public bool TryFind(ulong gameAddress, out AddressRange found)
    {
        var ranges = Volatile.Read(ref _ranges);
        int low = 0;
        int high = ranges.Length - 1;

        while (low <= high)
        {
            int middle = low + ((high - low) >> 1);
            var range = ranges[middle];

            if (gameAddress < range.GameAddress)
            {
                high = middle - 1;
            }
            else if (gameAddress >= range.GameAddress + range.Size)
            {
                low = middle + 1;
            }
            else
            {
                found = range;

                return true;
            }
        }

        found = default;

        return false;
    }
}
