// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using FiveStar.Cli.Shader;

namespace FiveStar.Cli.Setup;

/// <summary>
/// What every step works from: the dump, the work folder, the sample size (<c>--limit</c> programs and textures per
/// file, every <c>--step</c>-th program), the parallel jobs, and the command's arguments for the tool options.
/// </summary>
internal sealed record SetupContext(string Dump, string Work, int? Limit, int Every, int Jobs, CommandArguments Arguments)
{
    public string ShaderFolder => Path.Combine(Work, "shaders");

    public string ProgramFolder => Path.Combine(ShaderFolder, "fssm");

    public string ManifestPath => Path.Combine(ShaderFolder, "dxbc", "_manifest.tsv");

    public string TranslationFolder => Path.Combine(ShaderFolder, "hlsl");

    public string CacheFolder => Path.Combine(Work, "shader-cache");

    public string PipelineFolder => Path.Combine(Work, "pipelines");

    public string TextureFolder => Path.Combine(Work, "textures");

    public string LoaderFolder => Path.Combine(Work, "loader");

    public string StateFolder => Path.Combine(Work, "state");

    public string ShaderArchive => Path.Combine(Dump, "shaders.rpf");

    /// <summary>The sample options as text, for fingerprints: a different sample redoes the work.</summary>
    public string Sample => $"limit={Limit} step={Every}";
}
