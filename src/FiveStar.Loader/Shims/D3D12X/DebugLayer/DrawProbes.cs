// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Runtime.InteropServices;
using FiveStar.Loader.Diagnostics;
using FiveStar.Loader.Shims.D3D12X.Resources;
using Vortice.Direct3D12;

namespace FiveStar.Loader.Shims.D3D12X.DebugLayer;

/// <summary>
/// Debug readback of a render target right after a draw, inside the game's own command list: the target goes from the
/// state it is in on that list to COPY_SOURCE, is copied into a readback buffer and goes back, so the copy sees exactly
/// what the draw left. <see cref="Report"/> maps the buffers once the GPU finished (the caller waited) and logs what
/// each holds. Not for per-frame use: each probe makes a buffer of the target's size.
/// </summary>
internal sealed unsafe class DrawProbes
{
    private readonly object _gate = new();
    private readonly List<(string Label, ID3D12Resource Buffer, PlacedSubresourceFootPrint Footprint, uint Rows, ulong RowBytes, int Width, int Format)> _pending = [];
    private readonly LoaderLog _log;

    public DrawProbes(LoaderLog log)
    {
        _log = log;
    }

    /// <summary>Records the copy of <paramref name="target"/> (in <paramref name="state"/> on the list) into the desktop list <paramref name="list"/>.</summary>
    public void Record(nint device, nint list, nint target, int state, string label)
    {
        Marshal.AddRef(device);
        using var desktop = new ID3D12Device(device);
        Marshal.AddRef(target);
        var texture = new ID3D12Resource(target);
        var description = texture.Description;

        var footprints = new PlacedSubresourceFootPrint[1];
        var rows = new uint[1];
        var rowBytes = new ulong[1];
        desktop.GetCopyableFootprints(description, 0, 1, 0, footprints, rows, rowBytes, out ulong total);
        var buffer = desktop.CreateCommittedResource(new HeapProperties(HeapType.Readback), HeapFlags.None, Vortice.Direct3D12.ResourceDescription.Buffer(total), ResourceStates.CopyDest);

        Marshal.AddRef(list);
        using var commands = new ID3D12GraphicsCommandList(list);
        var resting = (ResourceStates)state;

        commands.ResourceBarrierTransition(texture, resting, ResourceStates.CopySource);
        commands.CopyTextureRegion(new TextureCopyLocation(buffer, footprints[0]), 0, 0, 0, new TextureCopyLocation(texture, 0), null);
        commands.ResourceBarrierTransition(texture, ResourceStates.CopySource, resting);
        texture.Dispose();

        lock (_gate)
        {
            _pending.Add((label, buffer, footprints[0], rows[0], rowBytes[0], (int)description.Width, (int)description.Format));
        }
    }

    /// <summary>Logs what every recorded probe holds; the GPU has finished the lists that recorded them.</summary>
    public void Report()
    {
        lock (_gate)
        {
            foreach (var probe in _pending)
            {
                _log.Info($"  [draws] after {probe.Label}: {Summary(probe.Buffer, probe.Footprint, probe.Rows, probe.RowBytes, probe.Width, probe.Format)}");
                probe.Buffer.Dispose();
            }

            _pending.Clear();
        }
    }

    private static string Summary(ID3D12Resource buffer, PlacedSubresourceFootPrint footprint, uint rows, ulong rowBytes, int width, int format)
    {
        var pixels = new byte[(int)rowBytes * (int)rows];
        void* data;
        buffer.Map(0, null, &data).CheckError();

        for (int row = 0; row < rows; row++)
        {
            new ReadOnlySpan<byte>((byte*)data + (row * footprint.Footprint.RowPitch), (int)rowBytes).CopyTo(pixels.AsSpan(row * (int)rowBytes));
        }

        buffer.Unmap(0);
        var rgba = ReadbackPixels.ToRgba(new ReadbackImage(width, (int)rows, format, (int)rowBytes, pixels));

        if (rgba is null)
        {
            return $"format {format} not converted";
        }

        return ReadbackPixels.Summarize(rgba, width, (int)rows);
    }
}
