// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using FiveStar.Loader.Shims.D3D12X.Pipelines;

namespace FiveStar.Loader.Tests.Support;

/// <summary>A program library that answers every key with one program.</summary>
internal sealed class SingleProgramLibrary(TranslatedProgram program) : IProgramLibrary
{
    public TranslatedProgram? Find(ReadOnlySpan<byte> prefix) => program;

    public IReadOnlyList<string> Names(ReadOnlySpan<byte> prefix) => [program.Name];
}
