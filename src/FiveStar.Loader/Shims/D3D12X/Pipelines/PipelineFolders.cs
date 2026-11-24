// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

namespace FiveStar.Loader.Shims.D3D12X.Pipelines;

/// <summary>
/// Where pipeline creation finds its shaders: the extracted FSSM programs and their translated DXIL. Without both,
/// the pipeline slots stay logged E_NOTIMPL stubs.
/// </summary>
/// <param name="Programs">Folder of <c>fivestar shader extract</c> output (--shader-dir).</param>
/// <param name="Translated">Folder of <c>fivestar shader translate</c> output (--translated-dir).</param>
/// <param name="Cache">Folder of the disk cache of built pipelines (--pipeline-cache); null for none.</param>
internal sealed record PipelineFolders(string? Programs, string? Translated, string? Cache)
{
    /// <summary>Gets the index of <see cref="Programs"/> started at loader start; null builds it at device creation.</summary>
    public ProgramIndexLoad? Index { get; init; }

    /// <summary>Every pipeline is built at creation, not only on first use (<c>--eager-pipelines</c>, <see cref="PipelineFactory.Lazy"/>).</summary>
    public bool EagerBuilds { get; init; }
}
