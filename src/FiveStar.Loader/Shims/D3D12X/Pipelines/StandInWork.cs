// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

namespace FiveStar.Loader.Shims.D3D12X.Pipelines;

/// <summary>
/// The work still outstanding for a stand-in (<see cref="StandInPipelines"/>): the request for its programs made on
/// demand, then its queued creation. When a command list first sets the stand-in both are made urgent.
/// </summary>
internal sealed class StandInWork
{
    /// <summary>Requests the programs, urgently when told so; null when the stand-in has none to wait for.</summary>
    public Func<bool, Task>? Programs { get; init; }

    /// <summary>The queued creation and its queue, once there is one.</summary>
    public (PipelineBuild Build, PipelineBuildQueue Queue)? Creation { get; set; }
}
