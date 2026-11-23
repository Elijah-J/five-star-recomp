// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

namespace FiveStar.Loader.Shims.D3D12X.Pipelines;

/// <summary>One queued desktop pipeline creation (<see cref="PipelineBuildQueue"/>) and where its result goes.</summary>
internal sealed record PipelineBuild(Func<PipelineResult> Create, TaskCompletionSource<PipelineResult> Done);
