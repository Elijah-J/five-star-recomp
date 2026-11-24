// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

namespace FiveStar.Shaders.Translation.Core;

/// <summary>
/// The bound on every translated loop. A console program's loops end on the data the console gives them; on the PC a
/// wrong input (a NaN or huge float step count, a garbage loop bound) can keep one going until the driver removes the
/// device (docs/shader-translation.md#loop-guard). A structured loop leaves through its first exit after
/// <see cref="Iterations"/> iterations, the block dispatch after <see cref="DispatchSteps"/> blocks.
/// </summary>
public static class LoopGuard
{
    public const long Iterations = 1 << 16;

    public const long DispatchSteps = 1 << 24;

    /// <summary><see cref="Iterations"/> as an HLSL literal.</summary>
    public static string Hex => Hlsl.HexUint(Iterations);

    /// <summary><see cref="DispatchSteps"/> as an HLSL literal.</summary>
    public static string DispatchHex => Hlsl.HexUint(DispatchSteps);
}
