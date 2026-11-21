// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using FiveStar.Shaders.Translation;

namespace FiveStar.Shaders.Compilation;

/// <summary>Writes and compiles the compute + vertex-pulling replacement of a mesh shader dxc rejected.</summary>
public sealed class MeshFallbackCompiler
{
    private readonly DxcCompiler _compiler;

    public MeshFallbackCompiler(DxcCompiler compiler)
    {
        _compiler = compiler;
    }

    /// <summary>Null when both shaders compile; otherwise why the fallback failed.</summary>
    public string? Compile(string meshPath, string meshHlsl, bool large)
    {
        string compute;
        string vertex;

        try
        {
            (compute, vertex) = MeshComputeFallback.Rewrite(meshHlsl);
        }
        catch (UnsupportedProgramException exception)
        {
            return exception.Message;
        }

        var stem = Path.ChangeExtension(meshPath, null);
        (string Source, string Profile, string Suffix)[] parts = [(compute, "cs", ".msfb_cs.hlsl"), (vertex, "vs", ".msfb_vs.hlsl")];

        foreach (var (source, profile, suffix) in parts)
        {
            var path = stem + suffix;
            File.WriteAllText(path, source);
            var result = _compiler.Compile(path, profile, large, _compiler.Options.FallbackTimeout);

            if (result.TimedOut)
            {
                return "dxc timeout";
            }

            if (result.ExitCode != 0)
            {
                return DxcCompiler.FirstError(result);
            }
        }

        return null;
    }
}
