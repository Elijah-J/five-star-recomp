// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

namespace FiveStar.Shaders.Translation;

/// <summary>
/// Translator choices. <paramref name="StructuredControlFlow"/> emits branches as if/else and loops instead of the
/// block dispatch loop, where the program's control-flow graph allows it.
/// </summary>
public sealed record TranslationOptions(bool StructuredControlFlow);
