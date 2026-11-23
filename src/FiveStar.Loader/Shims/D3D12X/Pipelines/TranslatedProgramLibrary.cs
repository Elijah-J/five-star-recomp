// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using FiveStar.Graphics.Programs;
using FiveStar.Loader.Diagnostics;
using FiveStar.Shaders.Cache;

namespace FiveStar.Loader.Shims.D3D12X.Pipelines;

/// <summary>
/// The output of <c>fivestar shader translate</c>: <c>&lt;folder&gt;/&lt;name&gt;.dxil</c> per program, found through a
/// <see cref="ProgramIndex"/> of the extracted FSSM programs, or the form a <see cref="ProgramVariantPicker"/> picks for
/// the adapter. Each DXIL file is read once.
/// </summary>
internal sealed class TranslatedProgramLibrary : IProgramLibrary
{
    private const string DxilExtension = ".dxil";

    private readonly object _gate = new();
    private readonly ProgramIndex _index;
    private readonly string _folder;
    private readonly ProgramVariantPicker? _picker;
    private readonly Dictionary<string, TranslatedProgram?> _loaded = new(StringComparer.Ordinal);

    /// <param name="index">The extracted programs.</param>
    /// <param name="folder">The translation's output folder.</param>
    /// <param name="picker">Picks each program's file for the adapter; null loads the native DXIL.</param>
    public TranslatedProgramLibrary(ProgramIndex index, string folder, ProgramVariantPicker? picker)
    {
        _index = index;
        _folder = folder;
        _picker = picker;
    }

    /// <summary>
    /// The library of <paramref name="folder"/> for the adapter of <paramref name="device"/>, picking variants when the
    /// folder holds <see cref="ShaderManifest.FileName"/>, else always the native DXIL.
    /// </summary>
    public static TranslatedProgramLibrary Open(ProgramIndex index, string folder, nint device, OnceLog warnings, LoaderLog log)
    {
        var manifestPath = Path.Combine(folder, ShaderManifest.FileName);

        if (!File.Exists(manifestPath))
        {
            log.Warn($"[d3d12_xs] no {ShaderManifest.FileName} in {folder}: every program loads its native DXIL, whatever the adapter's wave size");

            return new TranslatedProgramLibrary(index, folder, null);
        }

        var adapter = DesktopAdapter.Describe(device);
        log.Detail($"  [d3d12_xs] program variants for SM {adapter.ShaderModel}, wave lanes {adapter.WaveLaneCountMin}-{adapter.WaveLaneCountMax}, mesh {adapter.MeshShaderTier}");

        return new TranslatedProgramLibrary(index, folder, new ProgramVariantPicker(ShaderManifest.Read(manifestPath), adapter, warnings));
    }

    public TranslatedProgram? Find(ReadOnlySpan<byte> prefix)
    {
        foreach (var name in _index.Find(prefix))
        {
            var program = Load(name);

            if (program is not null)
            {
                return program;
            }
        }

        return null;
    }

    public IReadOnlyList<string> Names(ReadOnlySpan<byte> prefix)
    {
        return [.. _index.Find(prefix)];
    }

    private TranslatedProgram? Load(string name)
    {
        lock (_gate)
        {
            if (_loaded.TryGetValue(name, out var known))
            {
                return known;
            }

            var file = name + DxilExtension;

            if (_picker is not null)
            {
                file = _picker.File(name, file);
            }

            TranslatedProgram? program = null;

            if (file is not null && File.Exists(Path.Combine(_folder, file)))
            {
                program = new TranslatedProgram(name, _index.Stage(name), File.ReadAllBytes(Path.Combine(_folder, file)));
            }

            _loaded[name] = program;

            return program;
        }
    }
}
