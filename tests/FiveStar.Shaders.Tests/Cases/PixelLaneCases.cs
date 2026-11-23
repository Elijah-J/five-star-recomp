// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Numerics;
using FiveStar.Shaders.Tests.Reference;
using FiveStar.Shaders.Tests.Warp;

namespace FiveStar.Shaders.Tests.Cases;

/// <summary>
/// Pixel-shader cases for cross-lane operations, EXEC and helper lanes. Expected values use the PC lane layout from the
/// probe: on WARP a wave is one 2x2 quad, so wave-wide behaviour beyond a quad cannot be exercised here.
/// </summary>
public static class PixelLaneCases
{
    // value = x + 16 * y, the per-pixel input of the cross-lane cases.
    private const string PixelValue = """
        v_cvt_u32_f32_e32 v10, v2
        v_cvt_u32_f32_e32 v11, v3
        v_lshl_add_u32 v12, v11, 4, v10
        """;

    public static IEnumerable<PixelCase> All()
    {
        return
        [
            QuadPermutes(),
            LaneCount(),
            ExecFromScalar(),
            WholeQuadMode(),
            ReadFirstLane(),
            MaskAsInteger(),
            RowShift(),
            HelperExecInitial(),
            HelperExecWholeQuad(),
            WholeWaveReductions(),
        ];
    }

    // Whole-wave-mode minimum and sum of x + 16 * y, as compiled wave reductions do: lanes without a pixel hold the
    // identity (-1, 0), read across the wave by permlanex16 and readlane 20. On a 4-lane wave those lanes are missing,
    // so the result is the quad's, as on a console wave holding only this quad.
    private static PixelCase WholeWaveReductions()
    {
        const string Program = PixelValue + """

            s_mov_b32 s2, 0
            s_mov_b32 s3, 0
            s_or_saveexec_b32 s10, -1
            v_cndmask_b32_e64 v13, -1, v12, s10
            v_cndmask_b32_e64 v15, 0, v12, s10
            v_mov_b32_dpp v14, v13 quad_perm:[1,0,3,2] row_mask:0xf bank_mask:0xf
            v_mov_b32_dpp v16, v15 quad_perm:[1,0,3,2] row_mask:0xf bank_mask:0xf
            v_min_u32_e32 v13, v13, v14
            v_add_nc_u32_e32 v15, v15, v16
            v_mov_b32_dpp v14, v13 quad_perm:[2,3,0,1] row_mask:0xf bank_mask:0xf
            v_mov_b32_dpp v16, v15 quad_perm:[2,3,0,1] row_mask:0xf bank_mask:0xf
            v_min_u32_e32 v13, v13, v14
            v_add_nc_u32_e32 v15, v15, v16
            v_readlane_b32 s5, v13, 20
            v_readlane_b32 s7, v15, 20
            v_permlanex16_b32 v14, v13, s2, s3
            v_permlanex16_b32 v16, v15, s2, s3
            v_min_u32_e32 v13, v13, v14
            v_add_nc_u32_e32 v15, v15, v16
            v_readlane_b32 s4, v13, 0
            v_readlane_b32 s6, v15, 0
            s_min_u32 s4, s4, s5
            s_add_u32 s6, s6, s7
            s_mov_b32 exec_lo, s10
            v_mov_b32 v20, s4
            v_mov_b32 v21, s6
            """;

        return new PixelCase("whole_wave_reductions", Program, 2, pixel =>
        {
            var values = Enumerable.Range(0, 4).Select(slot => Value(pixel, slot)).ToList();

            return [values.Min(), values.Aggregate(0u, (sum, value) => sum + value)];
        });
    }

    private static uint Value(PixelContext pixel, int slot)
    {
        var (x, y) = pixel.QuadPixel(slot);

        return (uint)(x + (16 * y));
    }

    // x + 16 * y read across the 2x2 pixel quad with DPP quad_perm and ds_swizzle QUAD_PERM.
    private static PixelCase QuadPermutes()
    {
        const string Program = PixelValue + """

            v_mov_b32_dpp v20, v12 quad_perm:[1,0,3,2] row_mask:0xf bank_mask:0xf
            v_mov_b32_dpp v21, v12 quad_perm:[2,3,0,1] row_mask:0xf bank_mask:0xf
            v_mov_b32_dpp v22, v12 quad_perm:[0,0,0,0] row_mask:0xf bank_mask:0xf
            v_add_nc_u32_dpp v23, v12, v12 quad_perm:[3,2,1,0] row_mask:0xf bank_mask:0xf
            ds_swizzle_b32 v24, v12 offset:swizzle(QUAD_PERM,1,0,3,2)
            ds_swizzle_b32 v25, v12 offset:swizzle(QUAD_PERM,3,3,1,0)
            s_waitcnt lgkmcnt(0)
            v_mov_b32 v26, v12
            """;

        return new PixelCase("quad_dpp_swizzle", Program, 7, pixel =>
        {
            var slot = (pixel.X & 1) | ((pixel.Y & 1) << 1);
            var own = Value(pixel, slot);

            uint Read(int[] pattern) => Value(pixel, CrossLane.QuadPermute(slot, pattern));

            return [Read([1, 0, 3, 2]), Read([2, 3, 0, 1]), Read([0, 0, 0, 0]), Read([3, 2, 1, 0]) + own, Read([1, 0, 3, 2]), Read([3, 3, 1, 0]), own];
        });
    }

    // Lane index = popcount(mask & lanes below me).
    private static PixelCase LaneCount()
    {
        const string Program = """
            v_mbcnt_lo_u32_b32 v20, -1, 0
            v_mbcnt_hi_u32_b32 v21, -1, v20
            v_mbcnt_lo_u32_b32 v22, s0, 0
            v_mbcnt_lo_u32_b32 v23, exec_lo, 0
            """;

        return new PixelCase("mbcnt", Program, 4, pixel =>
        {
            var below = BitOperations.PopCount(0x5u & ((1u << pixel.Lane) - 1));

            return [pixel.Lane, pixel.Lane, below, pixel.Lane];
        })
        { Bindings = new Bindings { UserSgprs = [0x5] } };
    }

    // EXEC written from an integer SGPR: only lanes whose bit is set execute.
    private static PixelCase ExecFromScalar()
    {
        const string Program = """
            v_mov_b32 v20, 0
            s_mov_b32 exec_lo, s0
            v_mov_b32 v20, 1
            s_mov_b32 exec_lo, -1
            v_mov_b32 v21, 7
            """;

        return new PixelCase("exec_from_sgpr", Program, 2, pixel => [(0x5 >> pixel.Lane) & 1, 7]) { Bindings = new Bindings { UserSgprs = [0x5] } };
    }

    // s_wqm: a lane is enabled when any lane of its quad is (the x == 0 lanes enable the x in {0, 1} quads).
    private static PixelCase WholeQuadMode()
    {
        const string Program = """
            v_cvt_u32_f32_e32 v10, v2
            v_mov_b32 v20, 0
            s_mov_b32 s12, exec_lo
            v_cmp_eq_u32_e32 vcc_lo, 0, v10
            s_and_b32 exec_lo, exec_lo, vcc_lo
            s_wqm_b32 exec_lo, exec_lo
            v_mov_b32 v20, 1
            s_mov_b32 exec_lo, s12
            """;

        return new PixelCase("wqm_exec", Program, 1, pixel => [Convert.ToUInt32(pixel.X < 2)]);
    }

    // v_readfirstlane returns the first lane with EXEC set (here the odd-x lane 1 of the wave).
    private static PixelCase ReadFirstLane()
    {
        const string Program = PixelValue + """

            v_and_b32_e32 v13, 1, v10
            s_mov_b32 s12, exec_lo
            v_cmp_eq_u32_e32 vcc_lo, 1, v13
            s_and_b32 exec_lo, exec_lo, vcc_lo
            v_readfirstlane_b32 s20, v12
            s_mov_b32 exec_lo, s12
            v_mov_b32 v20, s20
            """;

        return new PixelCase("readfirstlane_exec", Program, 1, pixel =>
        {
            var (x, y) = pixel.WavePixel(1);

            return [x + (16 * y)];
        });
    }

    // A v_cmp lane mask read as an integer while EXEC is partial still holds the bits of the inactive lanes.
    private static PixelCase MaskAsInteger()
    {
        const string Program = """
            v_cvt_u32_f32_e32 v10, v2
            v_and_b32_e32 v13, 1, v10
            v_mov_b32 v20, 0
            v_cmp_eq_u32_e64 s10, 0, v13
            v_cmp_eq_u32_e64 s11, 1, v13
            s_mov_b32 s12, exec_lo
            s_and_b32 exec_lo, exec_lo, s11
            v_mov_b32 v20, s10
            s_mov_b32 exec_lo, s12
            """;

        return new PixelCase("mask_as_int_partial_exec", Program, 1, pixel =>
        {
            var even = Enumerable.Range(0, pixel.WaveSize).Select(lane => pixel.WavePixel(lane).X % 2 == 0).ToArray();
            var mask = (pixel.X & 1) != 0 ? CrossLane.Ballot(even).Low : 0;

            return [mask];
        });
    }

    // row_shr:1 with bound_ctrl: lane l reads lane l - 1 of its 16-lane row, the row start reads 0 (on WARP only lanes
    // 0..3 of a row exist).
    private static PixelCase RowShift()
    {
        const string Program = PixelValue + """

            v_add_nc_u32_e32 v12, 1, v12
            v_mov_b32_dpp v20, v12 row_shr:1 row_mask:0xf bank_mask:0xf bound_ctrl:0
            """;

        return new PixelCase("dpp_row_shr", Program, 1, pixel =>
        {
            var values = Enumerable.Range(0, pixel.WaveSize).Select(lane => Value(pixel, lane) + 1).ToArray();
            var moved = new Dpp { RowControl = "row_shr", Shift = 1, BoundControl = true }.Move(values, old: 0);

            return [Expected.Maybe(moved[pixel.Lane])];
        });
    }

    /// <summary>Quad slots of a pixel's quad covered by the half triangle (x + y &lt;= 6).</summary>
    private static List<int> LiveSlots(PixelContext pixel)
    {
        return [.. Enumerable.Range(0, 4).Where(slot => pixel.QuadPixel(slot).X + pixel.QuadPixel(slot).Y <= 6)];
    }

    // A pixel shader starts with EXEC = live pixels only (helper lanes off), here in quads half covered by the triangle.
    private static PixelCase HelperExecInitial()
    {
        const string Program = """
            v_mbcnt_lo_u32_b32 v20, exec_lo, 0
            s_bcnt1_i32_b32 s10, exec_lo
            v_mov_b32 v21, s10
            """;

        return new PixelCase("helper_exec_initial", Program, 2, pixel =>
        {
            var live = LiveSlots(pixel);

            return [live.Count(slot => slot < pixel.Quad), live.Count];
        })
        { HalfCoverage = true };
    }

    // After s_wqm EXEC holds all 4 lanes of every quad with a live pixel (helpers included).
    private static PixelCase HelperExecWholeQuad()
    {
        const string Program = """
            s_wqm_b32 exec_lo, exec_lo
            v_mbcnt_lo_u32_b32 v20, exec_lo, 0
            s_bcnt1_i32_b32 s10, exec_lo
            v_mov_b32 v21, s10
            """;

        return new PixelCase("helper_exec_wqm", Program, 2, pixel => [pixel.Quad, 4]) { HalfCoverage = true };
    }
}
