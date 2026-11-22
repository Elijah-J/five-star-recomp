// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using FiveStar.Cli.Shader;
using FiveStar.Cli.Tests.Support;
using FiveStar.Shaders.Cache;
using Xunit;

namespace FiveStar.Cli.Tests;

/// <summary>The driver pipeline cache of the user's shaders.rpf reads completely and links to the extracted programs.</summary>
public sealed class PipelineCacheTests
{
    [DumpFact]
    public void The_cache_in_shaders_rpf_reads_completely_and_its_pipelines_resolve_to_programs()
    {
        var cache = PipelineCacheSource.Load(Path.Combine(DumpFactAttribute.Folder!, "shaders.rpf"));

        Assert.Equal("930.048.020", cache.Header.DriverVersion);
        Assert.Equal((25471, 2693, 54576, 21522), (cache.Header.GraphicsCount, cache.Header.ComputeCount, cache.Header.MetaCount, cache.Header.ShaderCount));
        Assert.Equal(275540780, cache.Length);
        Assert.Equal(13385, cache.Graphics.Count(pipeline => pipeline.Stage(PipelineSlot.Vertex).HasValue && pipeline.Stage(PipelineSlot.Pixel).HasValue));
        Assert.Equal(12062, cache.Graphics.Count(pipeline => pipeline.Stage(PipelineSlot.Mesh).HasValue));
        Assert.All(cache.Graphics, pipeline => InputLayoutParser.Parse(cache.Meta(pipeline.InputLayoutMeta).Span));

        if (DumpFactAttribute.ShaderFolder is null)
        {
            return;
        }

        var programs = CacheProgramMap.Build(cache, ProgramIndex.Load(DumpFactAttribute.ShaderFolder));
        var resolved = cache.Graphics.Where(programs.Resolves).ToList();

        Assert.Equal(21502, programs.Matched);
        Assert.Equal(13362, resolved.Count(pipeline => pipeline.Stage(PipelineSlot.Vertex).HasValue && pipeline.Stage(PipelineSlot.Pixel).HasValue));
        Assert.Equal(10726, resolved.Count(pipeline => pipeline.Stage(PipelineSlot.Mesh).HasValue && pipeline.Stage(PipelineSlot.Pixel).HasValue));
    }
}
