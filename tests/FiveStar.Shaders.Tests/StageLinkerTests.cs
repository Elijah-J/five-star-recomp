// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using FiveStar.Shaders.Containers;
using FiveStar.Shaders.Tests.Toolchain;
using FiveStar.Shaders.Tests.Warp;
using FiveStar.Shaders.Translation;
using SharpGen.Runtime;
using Xunit;

namespace FiveStar.Shaders.Tests;

/// <summary>A producer linked to its pixel shader's interpolation modes creates a pipeline and shades as the pair should.</summary>
public sealed class StageLinkerTests
{
    // A vertex shader as the translator writes it: one parameter register holding two float2 elements, the second
    // different per vertex. Without modes dxc packs both into one register.
    private const string Producer = """
        struct Out {
          float2 p0_xy : TEXCOORD0;
          float2 p0_zw : TEXCOORD1;
          float4 pos0 : SV_Position;
        };
        Out main(uint vid : SV_VertexID) {
          Out o;
          float2 uv = float2((vid << 1) & 2, vid & 2);
          o.p0_xy = float2(1.5, 2.5);
          o.p0_zw = float2(vid * 10 + 1, vid * 10 + 2);
          o.pos0 = float4(uv * float2(1, -1) + float2(-1, 1), 0, 1);
          return o;
        }
        """;

    // The pixel shader reads the second element flat, so its own packing gives it a register of its own.
    private const string Consumer = """
        struct PSIn {
          float2 a0_xy : TEXCOORD0;
          nointerpolation float2 a0_zw : TEXCOORD1;
        };
        uint4 main(PSIn pin) : SV_Target0 {
          return uint4(asuint(pin.a0_xy), asuint(pin.a0_zw));
        }
        """;

    private const int Size = 8;

    // Linear (1.5, 2.5) and the flat value of vertex 0.
    private static readonly float[] Expected = [1.5f, 2.5f, 1f, 2f];

    [WarpFact]
    public void Linked_producer_creates_the_pipeline_and_passes_the_flat_value_of_the_provoking_vertex()
    {
        var folder = Directory.CreateTempSubdirectory("fivestar-link").FullName;
        var workbench = new ShaderWorkbench(ShaderToolchain.Find(), folder);
        var pixel = workbench.Compile("ps", Consumer, "ps");
        var inputs = PsvSignatureParser.Parse(DxbcContainer.Parse(pixel).Find(PsvSignatureParser.ChunkCode)!.Data.Span).Inputs;
        using var device = new WarpDevice();
        var runner = new WarpRunner(device);

        var unlinked = workbench.Compile("vs", Producer, "vs");
        Assert.ThrowsAny<SharpGenException>(() => runner.Graphics(Job(unlinked, pixel)));

        var linked = workbench.Compile("vs_linked", StageLinker.Link(Producer, inputs), "vs");
        var result = runner.Graphics(Job(linked, pixel));

        Assert.Equal(Expected, result.Pixel(0, Size / 2, Size / 2).Select(BitConverter.UInt32BitsToSingle));
    }

    private static GraphicsJob Job(byte[] vertex, byte[] pixel)
    {
        return new GraphicsJob(vertex, pixel, Framebuffer.Fullscreen(1, Size, Size, isFloat: false), new Bindings());
    }
}
