// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using FiveStar.Shaders.Cache;

namespace FiveStar.Loader.Shims.D3D12X.Pipelines;

/// <summary>
/// The output of <c>fivestar shader translate</c>: <c>&lt;folder&gt;/&lt;name&gt;.dxil</c> per program, found through a
/// <see cref="ProgramIndex"/> of the extracted FSSM programs. Each DXIL file is read once.
/// </summary>
internal sealed class TranslatedProgramLibrary : IProgramLibrary
{
    private const string DxilExtension = ".dxil";

    private readonly object _gate = new();
    private readonly ProgramIndex _index;
    private readonly string _folder;
    private readonly Dictionary<string, TranslatedProgram?> _loaded = new(StringComparer.Ordinal);

    public TranslatedProgramLibrary(ProgramIndex index, string folder)
    {
        _index = index;
        _folder = folder;
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

    private TranslatedProgram? Load(string name)
    {
        lock (_gate)
        {
            if (_loaded.TryGetValue(name, out var known))
            {
                return known;
            }

            var path = Path.Combine(_folder, name + DxilExtension);
            TranslatedProgram? program = null;

            if (File.Exists(path))
            {
                program = new TranslatedProgram(name, _index.Stage(name), File.ReadAllBytes(path));
            }

            _loaded[name] = program;

            return program;
        }
    }
}
