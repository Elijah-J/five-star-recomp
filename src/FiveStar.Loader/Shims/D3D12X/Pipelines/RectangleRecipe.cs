// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

namespace FiveStar.Loader.Shims.D3D12X.Pipelines;

/// <summary>What a graphics pipeline was built from, kept to build its rectangle-list variant on first use.</summary>
/// <param name="Description">The desktop description the pipeline was created with.</param>
/// <param name="Programs">Its translated programs.</param>
/// <param name="Vertex">The vertex program among them, whose outputs the geometry shader takes.</param>
internal sealed record RectangleRecipe(GraphicsPipelineDescription Description, IReadOnlyList<TranslatedProgram> Programs, TranslatedProgram Vertex);
