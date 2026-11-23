// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

namespace FiveStar.Loader.Shims.D3D12X.States;

/// <summary>
/// The textures one command list moved out of their home state since it was last closed, with the state each is in
/// now. A list is recorded by one thread at a time, so nothing here locks. Also holds the list's
/// ID3D12GraphicsCommandList7 (0 when enhanced barriers are not used), looked up once per desktop list.
/// </summary>
internal sealed class ListResourceStates
{
    /// <summary>Textures tracked per list; a transition past this many is written without tracking.</summary>
    public const int Capacity = 64;

    private const int NotFound = -1;

    private readonly nint[] _resources = new nint[Capacity];
    private readonly int[] _states = new int[Capacity];
    private readonly int[] _homes = new int[Capacity];

    /// <summary>The desktop list the enhanced interface below belongs to.</summary>
    public nint Desktop { get; set; }

    /// <summary>The desktop list's ID3D12GraphicsCommandList7, not referenced (the list holds it), or 0.</summary>
    public nint Enhanced { get; set; }

    public int Count { get; private set; }

    /// <summary>The state <paramref name="resource"/> is in on this list: the tracked one, else <paramref name="home"/>.</summary>
    [HotPath]
    public int Current(nint resource, int home)
    {
        int index = IndexOf(resource);

        if (index < 0)
        {
            return home;
        }

        return _states[index];
    }

    /// <summary>Records <paramref name="resource"/> in <paramref name="state"/>; false when the table is full.</summary>
    [HotPath]
    public bool Set(nint resource, int home, int state)
    {
        int index = IndexOf(resource);

        if (index < 0)
        {
            if (Count == Capacity)
            {
                return false;
            }

            index = Count;
            Count++;
            _resources[index] = resource;
            _homes[index] = home;
        }

        _states[index] = state;

        return true;
    }

    /// <summary>The tracked texture at <paramref name="index"/> with its home and current state.</summary>
    [HotPath]
    public (nint Resource, int Home, int State) At(int index)
    {
        return (_resources[index], _homes[index], _states[index]);
    }

    [HotPath]
    public void Clear()
    {
        Count = 0;
    }

    [HotPath]
    private int IndexOf(nint resource)
    {
        for (int i = 0; i < Count; i++)
        {
            if (_resources[i] == resource)
            {
                return i;
            }
        }

        return NotFound;
    }
}
