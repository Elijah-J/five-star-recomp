// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using FiveStar.Loader.Diagnostics;
using FiveStar.Loader.Native;
using Vortice.Direct3D12;

namespace FiveStar.Loader.Shims.D3D12X.DebugLayer;

/// <summary>
/// <c>--d3d12-breadcrumbs</c>: before every draw, dispatch and ExecuteIndirect, and at Close, a command list writes
/// its recording number and the operation's index to its own dword of a readback buffer, with
/// WriteBufferImmediate(MARKER_OUT), which waits for the work before it. A list keeps the pipeline of each operation,
/// so when <see cref="QueueProgress"/> reports an execution the GPU has not finished, each of its lists names the
/// operation it stopped at (docs/d3d12x-shim.md#queue-progress). Unlike DRED it needs no debug layer, and it covers
/// the game's own command lists only.
/// </summary>
internal static unsafe class GpuBreadcrumbs
{
    /// <summary>Command lists followed; each takes one dword.</summary>
    private const int Slots = 1 << 16;

    /// <summary>Operations per recording whose pipeline is kept.</summary>
    private const int KeptOperations = 4096;

    /// <summary>A marker is the recording number (low 16 bits) over the operation index.</summary>
    private const int SerialShift = 16;
    private const uint IndexMask = 0xFFFF;

    /// <summary>The index written at Close: the list finished.</summary>
    private const uint Finished = 0xFFFF;
    private const uint LastIndex = Finished - 1;

    /// <summary>ID3D12GraphicsCommandList2::WriteBufferImmediate and D3D12_WRITEBUFFERIMMEDIATE_MODE_MARKER_OUT (d3d12.h).</summary>
    private const int WriteBufferImmediateSlot = 66;
    private const int MarkerOut = 2;

    private static readonly nint[] Lists = new nint[Slots];
    private static bool _enabled;
    private static ID3D12Resource? _buffer;
    private static uint* _markers;
    private static ulong _address;
    private static int _slots;
    private static int _serial;

    [StructLayout(LayoutKind.Sequential)]
    private struct WriteParameter
    {
        public ulong Destination;
        public uint Value;
    }

    /// <summary>Turns the markers on (or leaves them off) for the device created next.</summary>
    public static void Enable(bool enabled)
    {
        _enabled = enabled;
    }

    /// <summary>After device creation: the marker buffer, when enabled.</summary>
    public static void Attach(nint device, LoaderLog log)
    {
        if (!_enabled)
        {
            return;
        }

        Marshal.AddRef(device);
        using var desktop = new ID3D12Device(device);
        var buffer = desktop.CreateCommittedResource(new HeapProperties(HeapType.Readback), HeapFlags.None, Vortice.Direct3D12.ResourceDescription.Buffer(Slots * sizeof(uint)), ResourceStates.CopyDest);
        void* data;
        buffer.Map(0, null, &data).CheckError();

        _buffer = buffer;
        _address = buffer.GPUVirtualAddress;
        _markers = (uint*)data;
        log.Info($"  [gpu] breadcrumbs on: a marker before every draw and dispatch of {Slots} command lists");
    }

    /// <summary>The number of a new recording (Reset).</summary>
    [HotPath]
    public static uint NextSerial()
    {
        return (uint)Interlocked.Increment(ref _serial) & IndexMask;
    }

    /// <summary>Before a draw, dispatch or ExecuteIndirect: notes its pipeline and marks that the work before it is done.</summary>
    [HotPath]
    public static void Before(XboxObject* list)
    {
        var activity = ListActivity.Of(list);

        if (_markers == null || !Ready(list, activity))
        {
            return;
        }

        int index = activity->Operations++;

        if (index < KeptOperations)
        {
            activity->Pipelines[index] = activity->Pipeline;
        }

        uint marker = LastIndex;

        if (index < LastIndex)
        {
            marker = (uint)index;
        }

        Write(list, activity, marker);
    }

    /// <summary>At Close: marks the list finished once its work is.</summary>
    [HotPath]
    public static void Close(XboxObject* list)
    {
        var activity = ListActivity.Of(list);

        if (_markers != null && Ready(list, activity))
        {
            Write(list, activity, Finished);
        }
    }

    /// <summary>Where each list of the execution that ends at <paramref name="value"/> on <paramref name="fence"/> stopped.</summary>
    public static void Describe(StringBuilder text, nint fence, ulong value, Func<nint, string> pipelineName)
    {
        if (_buffer is null || _markers == null)
        {
            return;
        }

        int count = Math.Min(Volatile.Read(ref _slots), Slots);

        for (int slot = 0; slot < count; slot++)
        {
            nint list = Lists[slot];
            var submission = ListSubmission.Of(list);

            if (list != 0 && submission->Fence == fence && submission->Value == value)
            {
                text.Append("\n        ").Append(Where((XboxObject*)list, _markers[slot], pipelineName));
            }
        }
    }

    private static string Where(XboxObject* list, uint marker, Func<nint, string> pipelineName)
    {
        var activity = ListActivity.Of(list);
        uint index = marker & IndexMask;
        string name = string.Create(CultureInfo.InvariantCulture, $"list 0x{(nint)list:x} ({activity->Operations} operations)");

        if (marker >> SerialShift != activity->Serial)
        {
            return $"{name}: not started";
        }

        if (index == Finished)
        {
            return $"{name}: finished";
        }

        string pipeline = "?";

        if (index < KeptOperations)
        {
            pipeline = pipelineName(activity->Pipelines[index]);
        }

        return string.Create(CultureInfo.InvariantCulture, $"{name}: STOPPED at operation {index} (the work before it is done), pipeline {pipeline}");
    }

    /// <summary>Gives the list its slot and pipeline table on first use; false when the slots ran out.</summary>
    [HotPath]
    private static bool Ready(XboxObject* list, ListActivity* activity)
    {
        if (activity->Slot == 0)
        {
            int slot = Interlocked.Increment(ref _slots);

            if (slot > Slots)
            {
                activity->Slot = -1;

                return false;
            }

            activity->Pipelines = (nint*)NativeMemory.AllocZeroed((nuint)(KeptOperations * sizeof(nint)));
            activity->Slot = slot;
            Lists[slot - 1] = (nint)list;
        }

        return activity->Slot > 0;
    }

    [HotPath]
    private static void Write(XboxObject* list, ListActivity* activity, uint index)
    {
        var parameter = new WriteParameter
        {
            Destination = _address + ((ulong)(activity->Slot - 1) * sizeof(uint)),
            Value = (activity->Serial << SerialShift) | index,
        };
        int mode = MarkerOut;
        nint inner = list->Inner;
        ((delegate* unmanaged<nint, uint, WriteParameter*, int*, void>)ComObject.Slot(inner, WriteBufferImmediateSlot))(inner, 1, &parameter, &mode);
    }
}
