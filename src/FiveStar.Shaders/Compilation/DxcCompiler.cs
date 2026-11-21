// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using FiveStar.Shaders.Tooling;

namespace FiveStar.Shaders.Compilation;

/// <summary>
/// Compiles translated HLSL with dxc: shader model 6.7 for every stage (programmable texel offsets, helper-lane wave
/// operations), HLSL 2021, entry point main, the output folder (with the runtime headers) as include path.
/// </summary>
public sealed class DxcCompiler
{
    private const string ShaderModel = "_6_7";
    private const int ErrorTextLength = 200;

    private readonly DxcOptions _options;

    public DxcCompiler(DxcOptions options)
    {
        _options = options;
    }

    public DxcOptions Options => _options;

    /// <summary>Compiles one file to <c>&lt;stem&gt;.dxil</c> next to it.</summary>
    public ToolResult Compile(string hlslPath, string profile, bool disableOptimizations, TimeSpan timeout)
    {
        var folder = Path.GetDirectoryName(Path.GetFullPath(hlslPath)) ?? ".";
        var output = Path.ChangeExtension(hlslPath, ".dxil");
        var arguments = new List<string>
        {
            "-nologo", "-T", profile + ShaderModel, "-E", "main", "-HV", "2021", "-I", folder, "-Fo", output, hlslPath,
        };

        if (disableOptimizations)
        {
            arguments.Add("-Od");
        }

        return ExternalTool.Run(_options.Path, arguments, timeout);
    }

    /// <summary>The first error line of a failed compile (or the start of its error output).</summary>
    public static string FirstError(ToolResult result)
    {
        var lines = result.Error.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var error = lines.FirstOrDefault(line => line.Contains("error", StringComparison.Ordinal));

        if (error is not null)
        {
            return error;
        }

        var text = result.Error.Replace("\r\n", "\n", StringComparison.Ordinal);

        return text[..Math.Min(ErrorTextLength, text.Length)];
    }
}
