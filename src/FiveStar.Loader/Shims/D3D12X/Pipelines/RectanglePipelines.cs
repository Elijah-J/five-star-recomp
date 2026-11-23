// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Collections.Concurrent;
using FiveStar.Loader.Native;
using FiveStar.Shaders.Compilation;
using FiveStar.Shaders.Containers;
using FiveStar.Shaders.Hardware;

namespace FiveStar.Loader.Shims.D3D12X.Pipelines;

/// <summary>
/// The rectangle-list variant of each graphics pipeline (docs/d3d12x-shim.md, Rectangle lists): the same pipeline with
/// the geometry shader of its vertex program's output signature added, read from <c>rect-gs/</c> of the translate
/// output (<c>fivestar shader rect-gs</c>; never compiled here). A variant is built on the first rectangle-list draw with
/// its pipeline; 0 stands for a pipeline that has none (mesh or geometry stage, no shader file, creation failed).
/// </summary>
internal sealed unsafe class RectanglePipelines
{
    private readonly nint _device2;
    private readonly nint _rootSignature;
    private readonly string _folder;
    private readonly OnceLog _warnings;
    private readonly ConcurrentDictionary<nint, RectangleRecipe> _recipes = new();
    private readonly ConcurrentDictionary<nint, nint> _variants = new();
    private readonly ConcurrentDictionary<string, byte[]?> _shaders = new(StringComparer.Ordinal);

    /// <param name="device2">A desktop ID3D12Device2 (the caller keeps it alive).</param>
    /// <param name="rootSignature">The root signature of translated programs.</param>
    /// <param name="folder">The compiled geometry shaders (<c>&lt;translated&gt;/rect-gs</c>).</param>
    /// <param name="warnings">Where missing shaders and failures are reported, once each.</param>
    public RectanglePipelines(nint device2, nint rootSignature, string folder, OnceLog warnings)
    {
        _device2 = device2;
        _rootSignature = rootSignature;
        _folder = folder;
        _warnings = warnings;
    }

    /// <summary>Keeps how <paramref name="pipeline"/> was built; a variant of an earlier pipeline at that address is dropped.</summary>
    public void Remember(nint pipeline, GraphicsPipelineDescription description, IReadOnlyList<TranslatedProgram> programs)
    {
        if (_variants.TryRemove(pipeline, out var stale) && stale != 0)
        {
            DesktopDevice.Release(stale);
        }

        var vertex = programs.FirstOrDefault(program => program.Stage == HardwareStage.Vertex);

        if (vertex is null || programs.Any(program => program.Stage is HardwareStage.Ngg or HardwareStage.Geometry))
        {
            _recipes.TryRemove(pipeline, out _);

            return;
        }

        _recipes[pipeline] = new RectangleRecipe(description, programs, vertex);
    }

    /// <summary>The variant of a desktop pipeline, built on the first call; 0 when it has none.</summary>
    [HotPath]
    public nint Variant(nint pipeline)
    {
        if (_variants.TryGetValue(pipeline, out var variant))
        {
            return variant;
        }

        return Create(pipeline);
    }

    private nint Create(nint pipeline)
    {
        nint variant = 0;

        if (_recipes.TryGetValue(pipeline, out var recipe))
        {
            variant = Build(recipe);
        }

        if (_variants.TryAdd(pipeline, variant))
        {
            return variant;
        }

        if (variant != 0)
        {
            DesktopDevice.Release(variant);
        }

        return _variants[pipeline];
    }

    private nint Build(RectangleRecipe recipe)
    {
        var shader = Shader(recipe.Vertex);

        if (shader is null)
        {
            return 0;
        }

        var programs = recipe.Programs.Append(new TranslatedProgram(recipe.Vertex.Name, HardwareStage.Geometry, shader)).ToList();
        var iid = DesktopDevice.PipelineStateIid;
        nint variant = 0;
        var hr = DesktopPipelineBuilder.Graphics(_device2, _rootSignature, recipe.Description, programs, &iid, &variant);

        if (HResult.Failed(hr))
        {
            _warnings.Warn($"rect-create-{recipe.Vertex.Name}", $"[d3d12_xs] rectangle-list pipeline for {recipe.Vertex.Name}: {HResult.Format(hr)}; drawn as triangles");

            return 0;
        }

        return variant;
    }

    /// <summary>The compiled geometry shader for the vertex program's outputs, or null with a warning.</summary>
    private byte[]? Shader(TranslatedProgram vertex)
    {
        string source;

        try
        {
            var chunk = DxbcContainer.Parse(vertex.Dxil).Find(PsvSignatureParser.ChunkCode) ?? throw new InvalidDataException("no PSV0 chunk");
            source = RectangleGeometryShader.Source(PsvSignatureParser.Parse(chunk.Data.Span).Outputs);
        }
        catch (Exception exception) when (exception is InvalidDataException or NotSupportedException)
        {
            _warnings.Warn($"rect-source-{vertex.Name}", $"[d3d12_xs] no rectangle-list geometry shader for {vertex.Name}: {exception.Message}");

            return null;
        }

        return _shaders.GetOrAdd(RectangleGeometryShader.FileName(source), Load);
    }

    private byte[]? Load(string file)
    {
        var path = Path.Combine(_folder, file);

        if (File.Exists(path))
        {
            return File.ReadAllBytes(path);
        }

        _warnings.Warn($"rect-file-{file}", $"[d3d12_xs] {path} missing (fivestar shader rect-gs <translated>): its rectangle lists are drawn as triangles");

        return null;
    }
}
