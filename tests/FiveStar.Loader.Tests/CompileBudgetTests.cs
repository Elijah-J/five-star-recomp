// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using FiveStar.Loader.Shims.D3D12X.Pipelines;
using Xunit;

namespace FiveStar.Loader.Tests;

/// <summary>The driver compile budget: estimates by DXIL size, and room for what running compiles still need.</summary>
public sealed class CompileBudgetTests
{
    private const long Gigabyte = 1L << 30;

    [Fact]
    public void A_compile_starts_only_while_memory_covers_it_and_what_the_running_ones_still_need()
    {
        long mesh = CompileBudget.Estimate(3L << 20);

        Assert.Equal(12 * Gigabyte, mesh);
        Assert.Equal(2 * Gigabyte, CompileBudget.Estimate(4096));
        Assert.Equal(32 * Gigabyte, CompileBudget.Estimate(64L << 20));

        // 30 GB free: one running compile that still needs 12 GB leaves room for another (4 + 12 + 12 = 28).
        Assert.True(CompileBudget.Room(mesh, 30UL * Gigabyte, 100UL * Gigabyte, 12 * Gigabyte));

        // Two that still need 24 GB do not; nor does a commit charge near its limit.
        Assert.False(CompileBudget.Room(mesh, 30UL * Gigabyte, 100UL * Gigabyte, 24 * Gigabyte));
        Assert.False(CompileBudget.Room(mesh, 30UL * Gigabyte, 16UL * Gigabyte, 0));
    }
}
