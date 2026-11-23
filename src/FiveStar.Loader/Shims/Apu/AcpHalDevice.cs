// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Runtime.InteropServices;
using FiveStar.Loader.Diagnostics;
using FiveStar.Loader.Native;

namespace FiveStar.Loader.Shims.Apu;

/// <summary>
/// The SHAPE side of acphal.dll: the XMA decode contexts and the IAcpHal object the game sends context messages
/// through. The game uses SHAPE only to decode XMA (no SRC, EQ, filter or DMA contexts); this skeleton hands out the
/// contexts and accepts every message, but nothing decodes XMA yet, so an XMA voice's output buffer stays empty. The
/// decoder exists (FiveStar.Formats.Audio.Xma.XmaStreamDecoder, one per context); what is missing is the context
/// protocol behind the per-context messages <see cref="SendMessage"/> receives (docs/audio-runtime.md).
/// </summary>
internal sealed unsafe class AcpHalDevice : IDisposable
{
    private readonly object _gate = new();
    private readonly HashSet<uint> _loggedMessages = [];
    private readonly ApuHeap _heap;
    private readonly LoaderLog _log;
    private nint _object;
    private nint _vtable;
    private nint _contexts;

    public AcpHalDevice(ApuHeap heap, LoaderLog log)
    {
        _heap = heap;
        _log = log;
    }

    /// <summary>HRESULT AcpHalAllocateShapeContexts(SHAPE_CONTEXTS*): fills the XMA array pointer from the APU heap.</summary>
    public int AllocateContexts(byte* block)
    {
        if (block == null)
        {
            return HResult.Pointer;
        }

        var counts = new ReadOnlySpan<uint>(block, AcpHalLayout.ContextKinds);

        for (int kind = 0; kind < AcpHalLayout.ContextKinds; kind++)
        {
            if (kind != AcpHalLayout.XmaKind && counts[kind] != 0)
            {
                _log.Warn($"[acphal] AcpHalAllocateShapeContexts: {counts[kind]} contexts of kind {kind}; only XMA is known");

                return HResult.NotImplemented;
            }
        }

        uint xma = counts[AcpHalLayout.XmaKind];
        nint array = 0;

        if (xma != 0)
        {
            int hr = _heap.Allocate(&array, null, xma * AcpHalLayout.XmaContextBytes, AcpHalLayout.ContextAlignment);

            if (HResult.Failed(hr))
            {
                return hr;
            }
        }

        ((nint*)(block + AcpHalLayout.PointersOffset))[AcpHalLayout.XmaKind] = array;
        _contexts = array;
        _log.Info($"  [acphal] AcpHalAllocateShapeContexts: {xma} XMA contexts at 0x{array:x} (no XMA decoder yet)");

        return HResult.Ok;
    }

    /// <summary>AcpHalReleaseShapeContexts(): the game passes nothing, so the last array is freed.</summary>
    public void ReleaseContexts()
    {
        if (_contexts != 0)
        {
            _ = _heap.Free(_contexts);
            _contexts = 0;
        }
    }

    /// <summary>HRESULT AcpHalCreate(IAcpHal**): one object for the process.</summary>
    public int Create(nint* result)
    {
        if (result == null)
        {
            return HResult.Pointer;
        }

        lock (_gate)
        {
            if (_object == 0)
            {
                _vtable = (nint)NativeMemory.AllocZeroed(AcpHalLayout.Slots, (nuint)sizeof(nint));
                AcpHalMethods.Fill((nint*)_vtable);
                _object = (nint)NativeMemory.AllocZeroed((nuint)sizeof(nint));
                *(nint*)_object = _vtable;
            }
        }

        *result = _object;

        return HResult.Ok;
    }

    /// <summary>IAcpHal::SendMessage: accepted; the first message of each type is logged with its first payload word.</summary>
    public int SendMessage(uint type, uint cookie, uint* payload)
    {
        lock (_gate)
        {
            if (!_loggedMessages.Add(type))
            {
                return HResult.Ok;
            }
        }

        string word = payload == null ? "none" : $"0x{*payload:x}";
        _log.Info($"  [acphal] IAcpHal message {type} (cookie 0x{cookie:x}, payload {word}) accepted, not acted on (logged once per type)");

        return HResult.Ok;
    }

    public void Dispose()
    {
        NativeMemory.Free((void*)_object);
        NativeMemory.Free((void*)_vtable);
        _object = 0;
        _vtable = 0;
    }
}
