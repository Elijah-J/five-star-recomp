// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Collections.Concurrent;

namespace FiveStar.Loader.Shims.D3D12X.Pipelines;

/// <summary>Pipeline creations that returned no pipeline, counted by slot and cause, for the sample summary.</summary>
internal sealed class PipelineFailureTally
{
    /// <summary>Causes the summary names; the rest are counted only.</summary>
    private const int ShownCauses = 8;

    private readonly ConcurrentDictionary<string, int> _causes = new(StringComparer.Ordinal);

    /// <summary>Counts one failed creation through <paramref name="slot"/>.</summary>
    public void Record(int slot, string cause)
    {
        _causes.AddOrUpdate($"slot {slot}: {cause}", 1, (_, count) => count + 1);
    }

    /// <summary>The failures, most frequent cause first; empty when none.</summary>
    public string Summary()
    {
        var causes = _causes.ToArray();

        if (causes.Length == 0)
        {
            return string.Empty;
        }

        int total = causes.Sum(cause => cause.Value);
        var shown = causes.OrderByDescending(cause => cause.Value).ThenBy(cause => cause.Key, StringComparer.Ordinal).Take(ShownCauses);

        return $"{total} failed in {causes.Length} causes ({string.Join("; ", shown.Select(cause => $"{cause.Value} x {cause.Key}"))})";
    }
}
