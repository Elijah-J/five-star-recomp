// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Diagnostics;
using System.Text;
using FiveStar.Loader.Diagnostics;

namespace FiveStar.Loader.Shims.D3D12X.DebugLayer;

/// <summary>
/// <c>--watch-transient</c>: checks the game's transient-memory pool every few milliseconds and logs the first
/// corrupted node with the memory around it. The pool (set up at 0x14247ac20) cuts one region into equal blocks; a
/// table of 0x58-byte block records holds each block's address (+0x10), retire token (+0x18) and a list (+0x48) of
/// 0x18-byte nodes {next, pooled object, token} that live in the block itself (0x140188430 links one per pooled
/// allocator or list; 0x144e9b480 walks the list when it takes the block again and starts a new one in the block's last
/// 0x18 bytes). A next pointer outside the pool or an object pointer outside user space is a write the game did not
/// make. The game's code is read, never changed.
/// </summary>
internal sealed unsafe class TransientPoolWatch
{
    /// <summary>Game globals: pool base, block table, block count and block size (0x14247ac2a to 0x14247ac75).</summary>
    private const ulong PoolBaseAddress = 0x1475DF1B8;
    private const ulong TableAddress = 0x1475DF1C0;
    private const ulong CountAddress = 0x1475DF1C8;
    private const ulong SizeAddress = 0x1475DF1CC;

    private const int RecordSize = 0x58;
    private const int BlockOffset = 0x10;
    private const int TokenOffset = 0x18;
    private const int ListOffset = 0x48;
    private const int NodeSize = 0x18;
    private const int NodeObjectOffset = 8;
    private const int NodeTokenOffset = 0x10;

    /// <summary>A list longer than this is taken as a loop.</summary>
    private const int MaxNodes = 1 << 16;

    private const ulong LowestUserAddress = 0x10000;
    private const ulong HighestUserAddress = 0x7FFF_FFFF_FFFF;
    private const int PollMilliseconds = 5;
    private const int DumpBytes = 0x60;
    private const int DumpRow = 16;
    private const ulong PointerAlignmentMask = 7;

    /// <summary>A node must look corrupted on this many polls in a row (the game may be relinking it).</summary>
    private const int Confirmations = 3;

    private readonly LoaderLog _log;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private ulong _suspect;
    private int _seen;

    private TransientPoolWatch(LoaderLog log)
    {
        _log = log;
    }

    /// <summary>Starts the watch thread; it waits for the game to set the pool up.</summary>
    public static void Start(LoaderLog log)
    {
        var watch = new TransientPoolWatch(log);
        var thread = new Thread(watch.Run) { IsBackground = true, Name = "fivestar transient watch" };
        thread.Start();
    }

    private void Run()
    {
        bool announced = false;

        while (true)
        {
            Thread.Sleep(PollMilliseconds);
            ulong pool = Read(PoolBaseAddress);
            ulong table = Read(TableAddress);
            uint count = *(uint*)CountAddress;
            uint size = *(uint*)SizeAddress;

            if (pool == 0 || table == 0 || count == 0 || size == 0)
            {
                continue;
            }

            if (!announced)
            {
                _log.Info($"  [transient] watching the pool at 0x{pool:x}: {count} blocks of 0x{size:x} bytes, records at 0x{table:x}");
                announced = true;
            }

            if (Check(pool, table, count, size))
            {
                return;
            }
        }
    }

    /// <summary>True once a corruption was confirmed and logged.</summary>
    private bool Check(ulong pool, ulong table, uint count, uint size)
    {
        ulong end = pool + ((ulong)count * size);

        for (uint i = 0; i < count; i++)
        {
            byte* record = (byte*)(table + ((ulong)i * RecordSize));
            ulong node = *(ulong*)(record + ListOffset);
            ulong previous = (ulong)(record + ListOffset);

            for (int n = 0; node != 0 && n < MaxNodes; n++)
            {
                if (!Valid(node, pool, end))
                {
                    return Suspect(i, record, previous, node, pool, size);
                }

                previous = node;
                node = *(ulong*)node;
            }
        }

        _seen = 0;

        return false;
    }

    /// <summary>A node address inside the pool whose pooled-object pointer is a user-space pointer.</summary>
    private static bool Valid(ulong node, ulong pool, ulong end)
    {
        if (node < pool || node + NodeSize > end || (node & PointerAlignmentMask) != 0)
        {
            return false;
        }

        ulong item = *(ulong*)(node + NodeObjectOffset);

        return item == 0 || (item >= LowestUserAddress && item <= HighestUserAddress && (item & PointerAlignmentMask) == 0);
    }

    private bool Suspect(uint index, byte* record, ulong holder, ulong node, ulong pool, uint size)
    {
        if (node != _suspect)
        {
            _suspect = node;
            _seen = 0;
        }

        _seen++;

        if (_seen < Confirmations)
        {
            return false;
        }

        ulong block = *(ulong*)(record + BlockOffset);
        ulong token = *(ulong*)(record + TokenOffset);

        var text = new StringBuilder();
        text.Append($"[transient] corrupted node list at {_clock.Elapsed.TotalSeconds:F1} s: block {index} (0x{block:x}, record 0x{(ulong)record:x}, token 0x{token:x}) ");
        text.Append($"reaches 0x{node:x} through 0x{holder:x} (a block's last node is at offset 0x{size - NodeSize:x})");
        ulong end = pool + ((ulong)*(uint*)CountAddress * size);
        ulong shown = node >= pool && node + NodeSize <= end ? node : holder;

        if (shown != (ulong)(record + ListOffset))
        {
            text.Append($"; node at 0x{shown:x} (pool offset 0x{shown - pool:x}): next 0x{*(ulong*)shown:x}, object 0x{*(ulong*)(shown + NodeObjectOffset):x}, token 0x{*(uint*)(shown + NodeTokenOffset):x}");
            Dump(text, shown, pool, end);
        }

        _log.Error(text.ToString());
        QueueProgress.Report("transient pool corrupted");

        return true;
    }

    /// <summary>The pool bytes around <paramref name="at"/>, 8-byte words per row.</summary>
    private static void Dump(StringBuilder text, ulong at, ulong pool, ulong end)
    {
        ulong first = Math.Max(pool, (at - DumpBytes) & ~(ulong)(DumpRow - 1));
        ulong last = Math.Min(end, at + DumpBytes);

        for (ulong row = first; row < last; row += DumpRow)
        {
            text.Append($"\n    0x{row:x}: {*(ulong*)row:x16} {*(ulong*)(row + sizeof(ulong)):x16}");
        }
    }

    private static ulong Read(ulong address)
    {
        return *(ulong*)address;
    }
}
