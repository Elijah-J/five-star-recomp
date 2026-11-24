// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using FiveStar.Shaders.Tests.Warp;
using Xunit;

namespace FiveStar.Shaders.Tests;

public sealed class ScratchExperiment : IClassFixture<WarpFixture>
{
    private readonly WarpFixture _fixture;

    public ScratchExperiment(WarpFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Try_containers()
    {
        var folder = Environment.GetEnvironmentVariable("SCRATCH_DXIL");

        if (folder is null)
        {
            return;
        }

        var lines = new List<string>();

        foreach (var file in Directory.GetFiles(folder, "*.dxil"))
        {
            try
            {
                var output = new BufferResource(1, new uint[64], Writable: true);
                var result = _fixture.Runner.Compute(new ComputeJob(File.ReadAllBytes(file), 1, 1, 1, new Bindings { Buffers = [output] }));
                lines.Add($"{Path.GetFileName(file)}: ok {string.Join(",", result.Buffers[1].Take(4))}");
            }
            catch (Exception exception)
            {
                lines.Add($"{Path.GetFileName(file)}: {exception.Message.Split('\n')[0]}");
            }
        }

        File.WriteAllLines(Path.Combine(folder, "results.txt"), lines);
    }
}
