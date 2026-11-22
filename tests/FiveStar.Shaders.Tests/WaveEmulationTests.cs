// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using FiveStar.Shaders.Containers;
using FiveStar.Shaders.Hardware;
using FiveStar.Shaders.Tests.Reference;
using FiveStar.Shaders.Tests.Toolchain;
using FiveStar.Shaders.Tests.Warp;
using FiveStar.Shaders.Translation;
using Xunit;

namespace FiveStar.Shaders.Tests;

/// <summary>
/// The prelude's emulated wave operations (vw_*) on WARP's 4-lane waves for program waves wider, as wide and narrower
/// (with pixel-style and mesh-style lane numbers): each thread must see its console wave (<see cref="ProgramWave"/>).
/// A translated program names the narrowest hardware wave with console results from what its reads reach.
/// </summary>
public sealed class WaveEmulationTests : IClassFixture<WarpFixture>
{
    private const int Threads = 32;
    private const int Results = 8;
    private const int HeapSlot = 1;

    private readonly WarpFixture _fixture;

    public WaveEmulationTests(WarpFixture fixture)
    {
        _fixture = fixture;
    }

    [WarpTheory]
    [InlineData(2, false)]
    [InlineData(4, false)]
    [InlineData(16, false)]
    [InlineData(16, true)]
    [InlineData(64, true)]
    public void Wave_operations_see_the_console_wave(int wave, bool groupLanes)
    {
        var shader = _fixture.Workbench.Compile($"vw_{wave}_{groupLanes}", Program(wave, groupLanes), "cs");
        var output = new BufferResource(HeapSlot, new uint[Threads * Results], Writable: true);
        var words = _fixture.Runner.Compute(new ComputeJob(shader, 1, 1, 1, new Bindings { Buffers = [output] })).Buffers[HeapSlot];
        var model = new ProgramWave(wave, _fixture.ComputeWaveSize, groupLanes);
        var mismatches = new List<string>();

        for (var thread = 0; thread < Threads; thread++)
        {
            var expected = Expected(model, thread, wave);
            var actual = words.AsSpan(thread * Results, Results).ToArray();

            if (!expected.SequenceEqual(actual))
            {
                mismatches.Add($"thread {thread}: expected {string.Join(" ", expected)} actual {string.Join(" ", actual)}");
            }
        }

        Assert.True(mismatches.Count == 0, string.Join("\n", mismatches));
    }

    [WarpTheory]
    [InlineData("v_mov_b32_dpp v1, v0 quad_perm:[1,0,3,2] row_mask:0xf bank_mask:0xf", 4)]
    [InlineData("v_mov_b32_dpp v1, v0 row_shr:1 row_mask:0xf bank_mask:0xf", 16)]
    [InlineData("v_permlanex16_b32 v1, v0, s2, s3", 32)]
    [InlineData("v_readlane_b32 s4, v0, 5", 8)]
    [InlineData("v_readlane_b32 s4, v0, s2", 64)]
    [InlineData("s_ff1_i32_b64 s2, exec\nv_readlane_b32 s4, v0, s2", 4)]
    public void A_pixel_program_states_the_narrowest_wave_with_console_results(string code, int floor)
    {
        var assembled = _fixture.Workbench.Assemble($"floor_{floor}_{code.Length}", code + "\ns_endpgm", 64);
        var config = new ShaderConfig { Stage = HardwareStage.Pixel, WaveSize = 64 };
        var program = new ShaderProgram(ShaderStage.Pixel, assembled.Instructions, assembled.Code, ShaderSignatures.None, config, null);

        Assert.StartsWith($"// program wave 64; console results on hardware waves >= {floor} lanes\n", ShaderWorkbench.Translate(program), StringComparison.Ordinal);
    }

    private static string Program(int wave, bool groupLanes)
    {
        var group = groupLanes ? 1 : 0;

        return $$"""
            #define VW_WAVE {{wave}}
            #define VW_EMULATE 1
            #define VW_GROUP_LANES {{group}}
            #include "rdna_prelude.hlsli"
            [numthreads({{Threads}}, 1, 1)]
            void main(uint gi : SV_GroupIndex) {
            #if VW_GROUP_LANES
                g_vw_lane = gi & (VW_WAVE - 1u);
            #endif
                RWByteAddressBuffer o = ResourceDescriptorHeap[{{HeapSlot}}];
                uint x = gi * 7u + 3u;
                bool c = gi % 3u == 0u;
                uint2 m = vw_ballot(c);
                uint r[{{Results}}] = { vw_lane(), vw_read(x, vw_lane() ^ 1u), vw_read(x, VW_WAVE - 1u), m.x, m.y,
                    vw_any(gi == 5u) ? 1u : 0u, vw_count(c), readfirstlane(x, c) };
                for (uint i = 0; i < {{Results}}; i++) o.Store((gi * {{Results}}u + i) * 4u, r[i]);
            }
            """;
    }

    private static uint Value(int? holder)
    {
        if (holder is null)
        {
            return 0;
        }

        return (uint)((holder.Value * 7) + 3);
    }

    private static bool Condition(int thread)
    {
        return thread % 3 == 0;
    }

    private static uint[] Expected(ProgramWave model, int thread, int wave)
    {
        var lane = model.Lane(thread);
        var mask = model.Ballot(thread, Condition);
        var first = mask == 0 ? 0 : System.Numerics.BitOperations.TrailingZeroCount(mask);
        var any = model.Members(thread).Contains(5) ? 1u : 0u;

        return
        [
            (uint)lane, Value(model.Holder(thread, lane ^ 1)), Value(model.Holder(thread, wave - 1)), (uint)mask, (uint)(mask >> 32),
            any, (uint)System.Numerics.BitOperations.PopCount(mask), Value(model.Holder(thread, first)),
        ];
    }
}
