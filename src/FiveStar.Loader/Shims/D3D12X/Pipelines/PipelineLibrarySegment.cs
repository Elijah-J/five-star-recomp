// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using FiveStar.Loader.Native;

namespace FiveStar.Loader.Shims.D3D12X.Pipelines;

/// <summary>
/// A finished segment of the pipeline cache: its file mapped for reading and the ID3D12PipelineLibrary1 the driver made
/// from it. Loads of one segment are serialized, as d3d12 asks of loads that may hit the same pipeline.
/// </summary>
internal sealed unsafe class PipelineLibrarySegment : IDisposable
{
    private readonly object _gate = new();
    private readonly PipelineLibraryView _view;
    private nint _library;

    private PipelineLibrarySegment(string path, PipelineLibraryView view, nint library)
    {
        Path = path;
        _view = view;
        _library = library;
    }

    public string Path { get; }

    public IReadOnlyList<string> Keys => _view.Keys;

    /// <summary>The bytes of the library blob.</summary>
    public long Bytes => (long)_view.Size;

    /// <summary>
    /// The segment at <paramref name="path"/>, or null with the reason: a foreign or damaged file (S_OK), or the
    /// driver's refusal of the blob (another adapter or driver version, or a damaged blob).
    /// </summary>
    public static PipelineLibrarySegment? Open(string path, nint device1, ReadOnlySpan<byte> salt, out int refusal)
    {
        refusal = HResult.Ok;
        var view = PipelineLibraryFile.Open(path, salt);

        if (view is null)
        {
            return null;
        }

        nint library = 0;
        refusal = DesktopDevice.CreatePipelineLibrary(device1, (void*)view.Blob, view.Size, &library);

        if (HResult.Failed(refusal))
        {
            view.Dispose();

            return null;
        }

        return new PipelineLibrarySegment(path, view, library);
    }

    /// <summary>The pipeline stored as <paramref name="key"/> for the stream <paramref name="desc"/>.</summary>
    public int Load(string key, void* desc, Guid* iid, nint* result)
    {
        lock (_gate)
        {
            if (_library == 0)
            {
                return HResult.IllegalMethodCall;
            }

            return DesktopPipelineLibrary.Load(_library, key, desc, iid, result);
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            ComObject.Release(_library);
            _library = 0;
        }

        _view.Dispose();
    }
}
