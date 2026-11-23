// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using FiveStar.Loader.Native;

namespace FiveStar.Loader.Shims.D3D12X.Pipelines;

/// <summary>
/// The five pipeline-creation slots of the D3D12.X device (docs/d3d12x-pipelines.md), with the game's arguments after
/// the device pointer. A description the readers cannot translate is reported once and answered with E_NOTIMPL.
/// </summary>
internal sealed unsafe class PipelineSlots
{
    // D3D12_PIPELINE_STATE_STREAM_DESC: SizeInBytes @0, pPipelineStateSubobjectStream @8.
    private const int StreamPointerField = sizeof(long);

    private readonly PipelineFactory _factory;
    private readonly OnceLog _warnings;
    private int _requests;
    private int _created;

    public PipelineSlots(PipelineFactory factory, OnceLog warnings)
    {
        _factory = factory;
        _warnings = warnings;
    }

    /// <summary>Slot 55: (ID3D12PipelineState* base, UINT count, const override* entries, REFIID, void**).</summary>
    public int Derive(nint basePipeline, uint count, byte* entries, Guid* iid, nint* result)
    {
        return Guarded(PipelineLayout.DeriveSlot, result, () => _factory.Derive(basePipeline, PipelineDescReader.Overrides(count, entries), iid, result));
    }

    /// <summary>Slot 57: (const pipeline-cache graphics description*, REFIID, void**).</summary>
    public int CachedGraphics(byte* desc, Guid* iid, nint* result)
    {
        return Guarded(PipelineLayout.PrecompiledGraphicsSlot, result, () => _factory.Graphics(PipelineDescReader.CachedGraphics(desc), iid, result));
    }

    /// <summary>Slot 62: (const D3D12_COMPUTE_PIPELINE_STATE_DESC*, two arguments the game passes as 0, REFIID, void**).</summary>
    public int Compute(byte* desc, nint second, nint third, Guid* iid, nint* result)
    {
        if (second != 0 || third != 0)
        {
            _warnings.Warn("compute-arguments", $"[d3d12_xs] compute pipeline with arguments 0x{second:x}, 0x{third:x} (always 0 in the game's calls)");
        }

        return Guarded(PipelineLayout.ComputeSlot, result, () => _factory.Compute(PipelineDescReader.Compute(desc), iid, result));
    }

    /// <summary>Slot 67: (const pipeline-cache compute description*, REFIID, void**).</summary>
    public int CachedCompute(byte* desc, Guid* iid, nint* result)
    {
        return Guarded(PipelineLayout.PrecompiledComputeSlot, result, () => _factory.Compute(PipelineDescReader.CachedCompute(desc), iid, result));
    }

    /// <summary>Slot 86: (const D3D12_PIPELINE_STATE_STREAM_DESC*, REFIID, void**).</summary>
    public int Stream(byte* desc, Guid* iid, nint* result)
    {
        return Guarded(PipelineLayout.StreamSlot, result, () =>
        {
            var stream = *(byte**)(desc + StreamPointerField);

            return _factory.Graphics(PipelineStreamParser.Parse(stream, *(long*)desc), iid, result);
        });
    }

    /// <summary>Pipeline creations the game asked for so far, and how many returned a pipeline.</summary>
    public string Summary => $"{Volatile.Read(ref _requests)} pipelines requested, {Volatile.Read(ref _created)} created";

    private int Guarded(int slot, nint* result, Func<int> create)
    {
        if (result == null)
        {
            return HResult.Pointer;
        }

        *result = 0;
        Interlocked.Increment(ref _requests);

        try
        {
            int hr = create();

            if (*result != 0)
            {
                Interlocked.Increment(ref _created);
            }

            return hr;
        }
        catch (Exception exception) when (exception is InvalidDataException or NotSupportedException)
        {
            _warnings.Warn($"slot{slot}-{exception.Message}", $"[d3d12_xs] ID3D12Device.X::Slot{slot}: {exception.Message} -> E_NOTIMPL");

            return HResult.NotImplemented;
        }
    }
}
