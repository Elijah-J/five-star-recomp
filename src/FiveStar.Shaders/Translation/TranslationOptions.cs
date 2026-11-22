// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

namespace FiveStar.Shaders.Translation;

/// <summary>
/// Translator choices. <paramref name="StructuredControlFlow"/> emits branches as if/else and loops instead of the
/// block dispatch loop, where the program's control-flow graph allows it. <paramref name="PinWaveSize"/> gives a compute
/// program that moves data between lanes by lane number <c>[WaveSize(n)]</c> with its native wave size, so it runs only
/// where the GPU offers that size instead of computing wrong results.
/// </summary>
public sealed record TranslationOptions(bool StructuredControlFlow, bool PinWaveSize);
