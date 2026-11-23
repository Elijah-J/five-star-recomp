// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using FiveStar.Formats.Compression;
using FiveStar.Loader.Diagnostics;
using FiveStar.Loader.Native;

namespace FiveStar.Loader.Shims.Storage;

/// <summary>
/// Runs the requests of one queue on its worker thread: two overlapped read slots, so the next request reads while the
/// current one inflates, a reusable zlib inflater, and delivery to system memory or the destination sink. A request
/// whose sector-aligned source does not fit the staging size is read in pieces when it is uncompressed, and fails
/// otherwise.
/// </summary>
internal sealed unsafe class RequestExecutor : IDisposable
{
    /// <summary>Compression format 0: the bytes are copied as stored.</summary>
    public const byte CompressionNone = 0;

    /// <summary>Compression format 1: a zlib stream (RFC 1950); the archives' chunks are zlib streams.</summary>
    public const byte CompressionZlib = 1;

    private const int SlotCount = 2;
    private const int MinimumStagingBytes = 0x100000;
    private const int MaxLoggedFailures = 16;

    private readonly ReadSlot[] _slots = new ReadSlot[SlotCount];
    private readonly ZlibInflater _inflater = new();
    private readonly IStorageDestinationSink _sink;
    private readonly LoaderLog _log;
    private readonly int _stagingBytes;
    private int _loggedFailures;
    private bool _loggedCompression;

    public RequestExecutor(uint stagingBytes, IStorageDestinationSink sink, LoaderLog log)
    {
        _stagingBytes = (int)Math.Clamp(stagingBytes, MinimumStagingBytes, int.MaxValue);
        _sink = sink;
        _log = log;

        for (int i = 0; i < SlotCount; i++)
        {
            _slots[i] = new ReadSlot();
        }
    }

    /// <summary>Starts reading a file request when a slot is free and it was not started yet; memory sources need no read.</summary>
    [HotPath]
    public void Start(long sequence, in StorageRequest request)
    {
        if (request.SourceIsMemory || Find(sequence) is not null)
        {
            return;
        }

        var slot = FreeSlot();
        nint handle = FileHandle(request);

        if (slot is null || handle == 0)
        {
            return;
        }

        slot.TryBegin(sequence, handle, request.SourceOffset, request.SourceSize, _stagingBytes);
    }

    /// <summary>Finishes one request: waits for its read, inflates it and writes the destination. Returns its HRESULT.</summary>
    [HotPath]
    public int Complete(long sequence, in StorageRequest request)
    {
        int result = Run(sequence, request);

        if (HResult.Failed(result))
        {
            LogFailure(request, result);
        }

        return result;
    }

    public void Dispose()
    {
        foreach (var slot in _slots)
        {
            slot.Dispose();
        }
    }

    [HotPath]
    private static nint FileHandle(in StorageRequest request)
    {
        if (request.Source == 0)
        {
            return 0;
        }

        return StorageObjects.Owner<StorageFile>(request.Source).Handle;
    }

    [HotPath]
    private int Run(long sequence, in StorageRequest request)
    {
        if (request.SourceIsMemory)
        {
            return Deliver(request, new ReadOnlySpan<byte>((void*)request.Source, (int)request.SourceSize));
        }

        nint handle = FileHandle(request);

        if (handle == 0)
        {
            return HResult.InvalidArgument;
        }

        Start(sequence, request);
        var slot = Find(sequence);

        if (slot is null)
        {
            return ReadInPieces(request, handle);
        }

        int result = slot.Wait(out var data);

        if (!HResult.Failed(result))
        {
            result = Deliver(request, data);
        }

        slot.Release();

        return result;
    }

    /// <summary>An uncompressed request larger than the staging size, read through one slot a staging buffer at a time.</summary>
    private int ReadInPieces(in StorageRequest request, nint handle)
    {
        if (request.Compression != CompressionNone || !request.DestinationIsMemory || request.Destination == 0 || request.DestinationSize < request.SourceSize)
        {
            return HResult.InvalidArgument;
        }

        var slot = FreeSlot()!;
        uint piece = (uint)(_stagingBytes - (ReadSlot.Alignment * SlotCount));
        var destination = (byte*)request.Destination;

        for (uint done = 0; done < request.SourceSize; done += piece)
        {
            uint size = Math.Min(piece, request.SourceSize - done);
            slot.TryBegin(0, handle, request.SourceOffset + done, size, _stagingBytes);
            int result = slot.Wait(out var data);
            slot.Release();

            if (HResult.Failed(result))
            {
                return result;
            }

            data.CopyTo(new Span<byte>(destination + done, (int)size));
        }

        return HResult.Ok;
    }

    [HotPath]
    private int Deliver(in StorageRequest request, ReadOnlySpan<byte> source)
    {
        ReadOnlySpan<byte> data = source;

        if (request.Compression == CompressionZlib)
        {
            var inflated = _inflater.Inflate(source, DeflateWrapper.Zlib, (int)request.DestinationSize);

            if (inflated.Status != InflateStatus.Complete || _inflater.Output.Length != request.DestinationSize)
            {
                return HResult.InvalidData;
            }

            data = _inflater.Output;
        }
        else if (request.Compression != CompressionNone)
        {
            return UnknownCompression(request);
        }

        if (!request.DestinationIsMemory)
        {
            return _sink.Write(request, data);
        }

        if (request.Destination == 0 || data.Length > request.DestinationSize)
        {
            return HResult.InvalidArgument;
        }

        data.CopyTo(new Span<byte>((void*)request.Destination, (int)request.DestinationSize));

        return HResult.Ok;
    }

    [HotPath]
    private ReadSlot? Find(long sequence)
    {
        foreach (var slot in _slots)
        {
            if (slot.Busy && slot.Sequence == sequence)
            {
                return slot;
            }
        }

        return null;
    }

    [HotPath]
    private ReadSlot? FreeSlot()
    {
        foreach (var slot in _slots)
        {
            if (!slot.Busy)
            {
                return slot;
            }
        }

        return null;
    }

    /// <summary>A format the game was not seen to use (BCPack would be one): logged once, failed with E_NOTIMPL.</summary>
    private int UnknownCompression(in StorageRequest request)
    {
        if (!_loggedCompression)
        {
            _loggedCompression = true;
            _log.Warn($"  [xcrdapi] DirectStorage compression format {request.Compression} is not implemented (only none and zlib)");
        }

        return HResult.NotImplemented;
    }

    private void LogFailure(in StorageRequest request, int result)
    {
        if (_loggedFailures >= MaxLoggedFailures)
        {
            return;
        }

        _loggedFailures++;
        string source = "memory";

        if (!request.SourceIsMemory && request.Source != 0)
        {
            source = StorageObjects.Owner<StorageFile>(request.Source).Path;
        }

        _log.Warn($"  [xcrdapi] DirectStorage request failed {HResult.Format(result)}: {source} at 0x{request.SourceOffset:x}, 0x{request.SourceSize:x} bytes, compression {request.Compression}, 0x{request.DestinationSize:x} out");
    }
}
