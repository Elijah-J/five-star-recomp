// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using FiveStar.Shaders.Tests.Cases;
using FiveStar.Shaders.Tests.Toolchain;
using FiveStar.Shaders.Tests.Warp;

namespace FiveStar.Shaders.Tests;

/// <summary>
/// One WARP device, work folder and tool set per test class (classes run one at a time), created on first use so a
/// skipped class creates nothing. The probes record the PC wave layout that lane-dependent expectations need.
/// </summary>
public sealed class WarpFixture : IDisposable
{
    private const int ProbeGroups = 2;
    private const int ProbeGroupSize = 64;
    private const int ProbeHeapSlot = 1;

    private readonly Lazy<string> _folder;
    private readonly Lazy<ShaderWorkbench> _workbench;
    private readonly Lazy<WarpDevice> _device;
    private readonly Lazy<PixelContext[]> _pixelLanes;
    private readonly Lazy<int> _computeWaveSize;
    private readonly Dictionary<string, byte[]> _helpers = [];

    public WarpFixture()
    {
        _folder = new Lazy<string>(() => Directory.CreateTempSubdirectory("fivestar-warp-").FullName);
        _workbench = new Lazy<ShaderWorkbench>(() => new ShaderWorkbench(ShaderToolchain.Find(), _folder.Value));
        _device = new Lazy<WarpDevice>(() => new WarpDevice());
        _pixelLanes = new Lazy<PixelContext[]>(ProbePixels);
        _computeWaveSize = new Lazy<int>(ProbeComputeWave);
    }

    public ShaderWorkbench Workbench => _workbench.Value;

    public WarpRunner Runner => new(_device.Value);

    /// <summary>Per pixel of an 8x8 fullscreen draw (row-major): its PC lane, quad slot and wave size.</summary>
    public IReadOnlyList<PixelContext> PixelLanes => _pixelLanes.Value;

    /// <summary>The PC compute wave size (WARP: 4 lanes).</summary>
    public int ComputeWaveSize => _computeWaveSize.Value;

    /// <summary>A helper shader of the Shaders folder, compiled once.</summary>
    public byte[] Helper(string fileName, string profile)
    {
        if (!_helpers.TryGetValue(fileName, out var shader))
        {
            shader = Workbench.CompileHelper(fileName, profile);
            _helpers[fileName] = shader;
        }

        return shader;
    }

    public void Dispose()
    {
        if (_device.IsValueCreated)
        {
            _device.Value.Dispose();
        }

        if (_folder.IsValueCreated)
        {
            Directory.Delete(_folder.Value, recursive: true);
        }
    }

    private PixelContext[] ProbePixels()
    {
        var vertex = Helper("fullscreen_vs.hlsl", "vs");
        var pixel = Helper("probe_ps.hlsl", "ps");
        var size = PixelCase.Size;
        var result = Runner.Graphics(new GraphicsJob(vertex, pixel, Framebuffer.Fullscreen(1, size, size, isFloat: false), new Bindings()));
        var lanes = new PixelContext[size * size];

        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var words = result.Pixel(0, x, y);
                lanes[(y * size) + x] = new PixelContext(x, y, (int)words[0], (int)words[2], (int)words[1]);
            }
        }

        return lanes;
    }

    private int ProbeComputeWave()
    {
        var shader = Helper("probe_cs.hlsl", "cs");
        var output = new BufferResource(ProbeHeapSlot, new uint[ProbeGroups * ProbeGroupSize * 4], Writable: true);
        var result = Runner.Compute(new ComputeJob(shader, ProbeGroups, 1, 1, new Bindings { Buffers = [output] }));
        var words = result.Buffers[ProbeHeapSlot];
        var wave = (int)words[1];

        for (var thread = 0; thread < ProbeGroups * ProbeGroupSize; thread++)
        {
            var index = thread % ProbeGroupSize;
            var (lane, count, first) = (words[4 * thread], words[(4 * thread) + 1], words[(4 * thread) + 2]);

            if (count != wave || lane != index % wave || first != index - lane)
            {
                throw new InvalidOperationException($"unexpected compute lane layout at thread {thread}: lane {lane}, count {count}, first {first}");
            }
        }

        return wave;
    }
}
