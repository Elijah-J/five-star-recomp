// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using FiveStar.Loader.Diagnostics;
using FiveStar.Loader.Native;

namespace FiveStar.Loader.Shims.D3D12X.Pipelines;

/// <summary>
/// The disk cache of built desktop pipelines (docs/d3d12x-pipelines.md, Disk cache): ID3D12PipelineLibrary1
/// segments in one folder, each pipeline named by <see cref="DesktopStream.Key"/>. Finished segments are mapped and
/// only read; new pipelines go to this process's active library, which is written to its own segment file every
/// <see cref="SaveInterval"/>, after <see cref="SaveBatch"/> new pipelines and at disposal, and becomes a finished
/// segment past <see cref="SegmentBytes"/>.
/// Segments the driver refuses (another adapter or driver version) are deleted; past <see cref="MaximumBytes"/> the
/// oldest segments are deleted when the cache opens.
/// </summary>
internal sealed unsafe class PipelineLibraryCache : IDisposable
{
    /// <summary>The cache's folder under the loader's work folder when no other is given.</summary>
    public const string FolderName = "pipeline-cache";

    private const long SegmentBytes = 256L << 20;

    /// <summary>Pipelines stored since the last write that make the saver write at once, keeping the active library small.</summary>
    private const int SaveBatch = 2048;
    private const long MaximumBytes = 32L << 30;
    private const double BytesPerMegabyte = 1 << 20;
    private const string Format = "fivestar pipeline cache 1";
    private const string IdFormat = "yyyyMMdd-HHmmss-fff";

    private static readonly TimeSpan SaveInterval = TimeSpan.FromMinutes(1);

    private readonly object _active = new();
    private readonly ConcurrentDictionary<string, PipelineLibrarySegment> _index = new(StringComparer.Ordinal);
    private readonly List<PipelineLibrarySegment> _segments = [];
    private readonly List<string> _activeKeys = [];
    private readonly HashSet<string> _activeSet = new(StringComparer.Ordinal);
    private readonly ManualResetEventSlim _stop = new();
    private readonly AutoResetEvent _wake = new(false);
    private readonly string _folder;
    private readonly nint _device1;
    private readonly byte[] _salt;
    private readonly LoaderLog _log;
    private Thread? _saver;
    private nint _library;
    private string _activePath = string.Empty;
    private int _sequence;
    private bool _dirty;
    private int _unsaved;
    private int _hits;
    private int _stored;
    private int _refused;

    private PipelineLibraryCache(string folder, nint device1, byte[] salt, LoaderLog log)
    {
        _folder = folder;
        _device1 = device1;
        _salt = salt;
        _log = log;
    }

    /// <summary>Pipelines loaded from the cache, stored in it, and loads the driver refused.</summary>
    public string Summary => $"pipeline cache: {Volatile.Read(ref _hits)} loaded, {Volatile.Read(ref _stored)} stored, {Volatile.Read(ref _refused)} refused, {_index.Count + _activeSet.Count} pipelines in {_folder}";

    /// <summary>
    /// The cache in <paramref name="folder"/> for the device (an ID3D12Device1 or later), or null when the device has
    /// no pipeline libraries or the root signature cannot be serialized.
    /// </summary>
    public static PipelineLibraryCache? Open(string folder, nint device1, LoaderLog log)
    {
        var root = BindlessRoot.Serialized();

        if (root is null)
        {
            return null;
        }

        Directory.CreateDirectory(folder);
        var salt = SHA256.HashData([.. Encoding.ASCII.GetBytes(Format), .. root]);
        var cache = new PipelineLibraryCache(folder, device1, salt, log);

        if (!cache.NewActive())
        {
            log.Warn("[d3d12_xs] pipeline cache off: the device creates no pipeline library");

            return null;
        }

        cache.Load();

        return cache;
    }

    /// <summary>True when the pipeline named <paramref name="key"/> is in the cache.</summary>
    public bool Contains(string key)
    {
        if (_index.ContainsKey(key))
        {
            return true;
        }

        lock (_active)
        {
            return _activeSet.Contains(key);
        }
    }

    /// <summary>The cached pipeline of <paramref name="stream"/> (S_OK), or a failure when it is not cached.</summary>
    public int Load(DesktopStream stream, Guid* iid, nint* result)
    {
        var key = stream.Key;
        int hr = HResult.NotFound;
        bool known = false;

        lock (_active)
        {
            if (_activeSet.Contains(key))
            {
                known = true;
                hr = DesktopPipelineLibrary.Load(_library, key, stream.Describe(), iid, result);
            }
        }

        if (!known && _index.TryGetValue(key, out var segment))
        {
            known = true;
            hr = segment.Load(key, stream.Describe(), iid, result);
        }

        if (!HResult.Failed(hr))
        {
            Interlocked.Increment(ref _hits);
        }
        else if (known)
        {
            Interlocked.Increment(ref _refused);
        }

        return hr;
    }

    /// <summary>Adds a pipeline built for the stream named <paramref name="key"/> to the active library.</summary>
    public void Store(string key, nint pipeline)
    {
        lock (_active)
        {
            if (_library == 0 || _activeSet.Contains(key) || HResult.Failed(DesktopPipelineLibrary.Store(_library, key, pipeline)))
            {
                return;
            }

            _activeKeys.Add(key);
            _activeSet.Add(key);
            _dirty = true;

            if (++_unsaved >= SaveBatch)
            {
                _wake.Set();
            }
        }

        Interlocked.Increment(ref _stored);
    }

    /// <summary>
    /// Writes the active library to its segment file every <see cref="SaveInterval"/>, and after every
    /// <see cref="SaveBatch"/> stored pipelines, until disposal.
    /// </summary>
    public void StartSaving()
    {
        _saver = new Thread(SaveLoop) { IsBackground = true, Name = "fivestar pipeline cache" };
        _saver.Start();
    }

    /// <summary>Writes the active library when pipelines were stored since the last write.</summary>
    public void Save()
    {
        lock (_active)
        {
            if (!_dirty || _library == 0)
            {
                return;
            }

            long size;

            try
            {
                size = PipelineLibraryFile.Write(_activePath, _salt, _activeKeys, _library);
            }
            catch (IOException exception)
            {
                _log.Warn($"[d3d12_xs] pipeline cache not written to {_activePath}: {exception.Message}");

                return;
            }

            if (size < 0)
            {
                _log.Warn($"[d3d12_xs] pipeline cache not written: Serialize -> {HResult.Format((int)size)}");

                return;
            }

            _dirty = false;
            _unsaved = 0;

            if (size > SegmentBytes)
            {
                Rotate();
            }
        }
    }

    public void Dispose()
    {
        _stop.Set();
        _saver?.Join();
        Save();
        _log.Info($"  [d3d12_xs] {Summary}");

        lock (_active)
        {
            ComObject.Release(_library);
            _library = 0;
        }

        foreach (var segment in _segments)
        {
            segment.Dispose();
        }

        _stop.Dispose();
        _wake.Dispose();
    }

    private void SaveLoop()
    {
        WaitHandle[] wakers = [_stop.WaitHandle, _wake];

        while (WaitHandle.WaitAny(wakers, SaveInterval) != 0)
        {
            Save();
        }
    }

    /// <summary>Opens every segment, oldest first so newer ones name a pipeline last; deletes the unusable ones.</summary>
    private void Load()
    {
        var files = Directory.EnumerateFiles(_folder, "*" + PipelineLibraryFile.Extension).Order(StringComparer.Ordinal).ToList();
        files = Evict(files);
        var discarded = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var file in files)
        {
            var segment = PipelineLibrarySegment.Open(file, _device1, _salt, out var refusal);

            if (segment is null)
            {
                var reason = Refusal(refusal);
                discarded[reason] = discarded.GetValueOrDefault(reason) + 1;
                TryDelete(file);

                continue;
            }

            Add(segment);
        }

        foreach (var (reason, count) in discarded)
        {
            _log.Warn($"[d3d12_xs] pipeline cache: {count} segment(s) discarded ({reason})");
        }

        _log.Detail($"  [d3d12_xs] pipeline cache: {_index.Count} pipelines in {_segments.Count} segment(s) of {_folder}");
    }

    private void Add(PipelineLibrarySegment segment)
    {
        _segments.Add(segment);

        foreach (var key in segment.Keys)
        {
            _index[key] = segment;
        }
    }

    /// <summary>The active library's file becomes a finished segment, and a new empty library takes its place.</summary>
    private void Rotate()
    {
        var segment = PipelineLibrarySegment.Open(_activePath, _device1, _salt, out var refusal);

        if (segment is null)
        {
            _log.Warn($"[d3d12_xs] pipeline cache segment {_activePath} not reopened: {Refusal(refusal)}");

            return;
        }

        Add(segment);
        ComObject.Release(_library);
        _library = 0;
        _activeKeys.Clear();
        _activeSet.Clear();
        NewActive();
    }

    private bool NewActive()
    {
        nint library = 0;

        if (HResult.Failed(DesktopDevice.CreatePipelineLibrary(_device1, null, 0, &library)))
        {
            return false;
        }

        _library = library;
        var id = DateTime.UtcNow.ToString(IdFormat, CultureInfo.InvariantCulture);
        _activePath = Path.Combine(_folder, $"{id}-{Environment.ProcessId}-{Interlocked.Increment(ref _sequence)}{PipelineLibraryFile.Extension}");

        return true;
    }

    /// <summary>Deletes the oldest segments while the folder holds more than <see cref="MaximumBytes"/>; returns the rest.</summary>
    private List<string> Evict(List<string> files)
    {
        var total = files.Sum(file => new FileInfo(file).Length);
        var kept = new List<string>(files);

        while (total > MaximumBytes && kept.Count > 0)
        {
            total -= new FileInfo(kept[0]).Length;
            TryDelete(kept[0]);
            kept.RemoveAt(0);
        }

        if (kept.Count < files.Count)
        {
            _log.Warn($"[d3d12_xs] pipeline cache over {MaximumBytes / BytesPerMegabyte:F0} MB: {files.Count - kept.Count} oldest segment(s) deleted");
        }

        return kept;
    }

    private static string Refusal(int hr)
    {
        switch (hr)
        {
            case HResult.Ok:
                return "not a segment of this build";
            case DesktopPipelineLibrary.AdapterNotFound:
                return "made on another adapter";
            case DesktopPipelineLibrary.DriverVersionMismatch:
                return "made by another driver version";
            default:
                return $"refused by the driver: {HResult.Format(hr)}";
        }
    }

    private static void TryDelete(string file)
    {
        try
        {
            File.Delete(file);
        }
        catch (IOException)
        {
            // Another process has it open; a later run deletes it.
        }
        catch (UnauthorizedAccessException)
        {
            // As above.
        }
    }
}
