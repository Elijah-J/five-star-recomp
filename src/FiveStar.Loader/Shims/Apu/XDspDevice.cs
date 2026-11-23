// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Buffers.Binary;
using System.Runtime.InteropServices;
using FiveStar.Loader.Diagnostics;
using FiveStar.Loader.Native;

namespace FiveStar.Loader.Shims.Apu;

/// <summary>
/// chad.dll (XDsp) on the CPU: the audio DSP the game runs its convolution effects on. A stream is activated with an
/// effect config and gets a status record; each submitted command processes one block from an input buffer into an
/// output buffer and completes by advancing the status record's completed count, which the game polls. Here a command
/// completes when it is submitted, and no effect runs: the output buffer is left as the game zeroed it, so the effect
/// returns silence, as the game's own fallback does while a result is late.
/// </summary>
internal sealed unsafe class XDspDevice : IDisposable
{
    /// <summary>The connection handle; any nonzero value, the game only passes it back.</summary>
    public const nint ConnectionHandle = 0x58445350;

    /// <summary>Stream handles are this plus the stream's index, so a handle the game passes back is checked cheaply.</summary>
    public const nint StreamHandleBase = 0x58440000;

    private readonly object _gate = new();
    private readonly bool[] _active = new bool[XDspLayout.MaxStreams];
    private readonly uint[] _completed = new uint[XDspLayout.MaxStreams];
    private readonly LoaderLog _log;
    private byte* _status;
    private uint _streamLimit;
    private long _submitted;
    private int _firstSubmitLogged;

    public XDspDevice(LoaderLog log)
    {
        _log = log;
    }

    /// <summary>Commands completed since start.</summary>
    public long Submitted => Interlocked.Read(ref _submitted);

    /// <summary>
    /// HRESULT XDspConnectWithMaximumStreamLimit(void* memory, UINT32 bytes, UINT32 maxStreams, UINT32 (0x80),
    /// UINT32 (1), XDSP_HANDLE* handle). The memory is the game's own buffer pool, which the DSP would read and write.
    /// </summary>
    public int Connect(nint memory, uint bytes, uint maxStreams, nint* handle)
    {
        if (handle == null)
        {
            return HResult.Pointer;
        }

        lock (_gate)
        {
            if (_status == null)
            {
                _status = (byte*)NativeMemory.AllocZeroed(XDspLayout.MaxStreams * XDspLayout.StatusSize);
            }

            _streamLimit = Math.Min(maxStreams, XDspLayout.MaxStreams);
        }

        *handle = ConnectionHandle;
        _log.Info($"  [xdsp] XDspConnect: memory 0x{memory:x} (0x{bytes:x} bytes), {maxStreams} streams; effects pass silence");

        return HResult.Ok;
    }

    /// <summary>HRESULT XDspActivate(XDSP_HANDLE, const config*, status** status, XDSP_STREAM* stream).</summary>
    public int Activate(nint handle, byte* config, nint* status, nint* stream)
    {
        if (handle != ConnectionHandle || config == null || status == null || stream == null)
        {
            return HResult.InvalidArgument;
        }

        int index = Claim();

        if (index < 0)
        {
            return HResult.OutOfMemory;
        }

        *status = (nint)Status(index);
        *stream = StreamHandleBase + index;
        LogActivation(index, new ReadOnlySpan<byte>(config, XDspLayout.ConfigResponseAddressOffset + sizeof(long)));

        return HResult.Ok;
    }

    /// <summary>
    /// HRESULT XDspSubmitCommand(XDSP_STREAM, const command*, UINT32* id): completes the command at once and returns
    /// its id, the value the stream's completed count now holds.
    /// </summary>
    [HotPath]
    public int Submit(nint stream, byte* command, uint* id)
    {
        int index = Index(stream);

        if (index < 0 || command == null || !Volatile.Read(ref _active[index]))
        {
            return HResult.InvalidArgument;
        }

        uint completed = Interlocked.Increment(ref _completed[index]);
        byte* status = Status(index);
        *(uint*)(status + XDspLayout.StatusErrorOffset) = 0;
        Volatile.Write(ref *(uint*)(status + XDspLayout.StatusCompletedOffset), completed);

        if (id != null)
        {
            *id = completed;
        }

        Interlocked.Increment(ref _submitted);

        if (_firstSubmitLogged == 0 && Interlocked.Exchange(ref _firstSubmitLogged, 1) == 0)
        {
            LogFirstSubmit(index, command);
        }

        return HResult.Ok;
    }

    /// <summary>XDspDeactivate(XDSP_STREAM): frees the stream; the result is not read.</summary>
    public void Deactivate(nint stream)
    {
        int index = Index(stream);

        if (index < 0)
        {
            return;
        }

        lock (_gate)
        {
            Volatile.Write(ref _active[index], false);
        }
    }

    /// <summary>XDspDisconnect(XDSP_HANDLE): drops every stream; the status records stay for late readers.</summary>
    public void Disconnect()
    {
        lock (_gate)
        {
            Array.Clear(_active);
        }

        _log.Info($"  [xdsp] XDspDisconnect after {Submitted} commands");
    }

    public void Dispose()
    {
        NativeMemory.Free(_status);
        _status = null;
    }

    private int Claim()
    {
        lock (_gate)
        {
            for (int index = 0; index < _streamLimit; index++)
            {
                if (!_active[index])
                {
                    _active[index] = true;
                    _completed[index] = 0;
                    new Span<byte>(Status(index), XDspLayout.StatusSize).Clear();

                    return index;
                }
            }
        }

        return -1;
    }

    private int Index(nint stream)
    {
        nint index = stream - StreamHandleBase;

        if (index < 0 || index >= XDspLayout.MaxStreams || _status == null)
        {
            return -1;
        }

        return (int)index;
    }

    private byte* Status(int index)
    {
        return _status + (index * XDspLayout.StatusSize);
    }

    private void LogActivation(int index, ReadOnlySpan<byte> config)
    {
        var kind = (XDspEffectKind)BinaryPrimitives.ReadUInt32LittleEndian(config[XDspLayout.ConfigKindOffset..]);
        uint frames = BinaryPrimitives.ReadUInt32LittleEndian(config[XDspLayout.ConfigFramesOffset..]);
        uint channels = BinaryPrimitives.ReadUInt32LittleEndian(config[XDspLayout.ConfigChannelsOffset..]);
        uint response = BinaryPrimitives.ReadUInt32LittleEndian(config[XDspLayout.ConfigResponseLengthOffset..]);
        _log.Info($"  [xdsp] XDspActivate stream {index}: {kind}, {frames} frames, value {channels}, response {response} floats");
    }

    private void LogFirstSubmit(int index, byte* command)
    {
        var record = new ReadOnlySpan<byte>(command, XDspLayout.CommandSize);
        long input = BinaryPrimitives.ReadInt64LittleEndian(record[XDspLayout.CommandInputOffset..]);
        long output = BinaryPrimitives.ReadInt64LittleEndian(record[XDspLayout.CommandOutputOffset..]);
        _log.Info($"  [xdsp] first command on stream {index}: input 0x{input:x}, output 0x{output:x}, completed at once");
    }
}
