// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

namespace FiveStar.Cli.Shader;

/// <summary>One pipeline state to create: the program, its kind (cs, vs, ps, ms, gs, msfb_cs, msfb_vs) and its DXIL file.</summary>
internal sealed record PsoTarget(string Name, string Kind, string DxilPath)
{
    public string Key => $"{Name}|{Kind}";
}
