// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using FiveStar.Shaders.Compilation;
using FiveStar.Shaders.Disassembly;

namespace FiveStar.Cli.Shader;

/// <summary>
/// Finds the external tools: the LLVM bin folder (llvm-mc, llvm-objdump) from <c>--llvm</c>, then FIVESTAR_LLVM, then the
/// default install folder when it exists, then PATH; dxc from <c>--dxc</c>, then FIVESTAR_DXC, then the Windows SDK
/// default when it exists, then PATH.
/// </summary>
internal static class ShaderTools
{
    private const string LlvmVariable = "FIVESTAR_LLVM";
    private const string DxcVariable = "FIVESTAR_DXC";
    private const string PathVariable = "PATH";
    private const string DefaultLlvmFolder = @"D:\Tools\llvm\bin";
    private const string DefaultDxc = @"C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64\dxc.exe";

    public static LlvmToolPaths Llvm(CommandArguments arguments)
    {
        var folder = arguments.Option("llvm") ?? Environment.GetEnvironmentVariable(LlvmVariable);

        if (folder is null && Directory.Exists(DefaultLlvmFolder))
        {
            folder = DefaultLlvmFolder;
        }

        if (folder is null)
        {
            var suffix = OperatingSystem.IsWindows() ? ".exe" : string.Empty;

            return new LlvmToolPaths("llvm-mc" + suffix, "llvm-objdump" + suffix, LlvmToolPaths.DefaultCpu);
        }

        return LlvmToolPaths.InFolder(folder);
    }

    public static string Dxc(CommandArguments arguments)
    {
        var path = arguments.Option("dxc") ?? Environment.GetEnvironmentVariable(DxcVariable);

        if (path is null && File.Exists(DefaultDxc))
        {
            path = DefaultDxc;
        }

        return OnPath(path ?? "dxc");
    }

    public static DxcCompiler Compiler(CommandArguments arguments, bool retry)
    {
        return new DxcCompiler(DxcOptions.Defaults(Dxc(arguments), retry));
    }

    /// <summary>The full path of a bare tool name found in a PATH folder; other names unchanged.</summary>
    private static string OnPath(string tool)
    {
        if (Path.IsPathRooted(tool) || tool.Contains(Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            return tool;
        }

        var suffix = OperatingSystem.IsWindows() && !Path.HasExtension(tool) ? ".exe" : string.Empty;
        var folders = (Environment.GetEnvironmentVariable(PathVariable) ?? string.Empty).Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
        var found = folders.Select(folder => Path.Combine(folder, tool + suffix)).FirstOrDefault(File.Exists);

        return found ?? tool;
    }
}
