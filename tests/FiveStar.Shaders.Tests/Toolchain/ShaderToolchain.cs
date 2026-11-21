// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using FiveStar.Shaders.Disassembly;

namespace FiveStar.Shaders.Tests.Toolchain;

/// <summary>
/// The external tools of the translator tests: llvm-mc / llvm-objdump from FIVESTAR_LLVM (default D:\Tools\llvm\bin),
/// dxc from FIVESTAR_DXC (default: the Windows SDK), and D3D12 WARP. <see cref="MissingReason"/> says why the tests
/// cannot run, or is null.
/// </summary>
public sealed record ShaderToolchain(LlvmToolPaths Llvm, string Dxc)
{
    public const string LlvmVariable = "FIVESTAR_LLVM";
    public const string DxcVariable = "FIVESTAR_DXC";

    private const string DefaultLlvmFolder = @"D:\Tools\llvm\bin";
    private const string DefaultDxc = @"C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64\dxc.exe";
    private const string WarpLibrary = "d3d10warp.dll";

    public static ShaderToolchain Find()
    {
        var folder = Environment.GetEnvironmentVariable(LlvmVariable);
        var dxc = Environment.GetEnvironmentVariable(DxcVariable);

        if (string.IsNullOrWhiteSpace(folder))
        {
            folder = DefaultLlvmFolder;
        }

        if (string.IsNullOrWhiteSpace(dxc))
        {
            dxc = DefaultDxc;
        }

        return new ShaderToolchain(LlvmToolPaths.InFolder(folder), dxc);
    }

    /// <summary>Why the WARP translator tests cannot run here, or null when they can.</summary>
    public static string? MissingReason()
    {
        if (!OperatingSystem.IsWindows())
        {
            return "D3D12 WARP needs Windows";
        }

        var tools = Find();

        if (!File.Exists(tools.Llvm.Assembler) || !File.Exists(tools.Llvm.Objdump))
        {
            return $"llvm-mc / llvm-objdump not found (set {LlvmVariable} to the LLVM bin folder)";
        }

        if (!File.Exists(tools.Dxc))
        {
            return $"dxc not found (set {DxcVariable} to dxc.exe)";
        }

        if (!File.Exists(Path.Combine(Environment.SystemDirectory, WarpLibrary)))
        {
            return $"D3D12 WARP ({WarpLibrary}) not installed";
        }

        return null;
    }
}
