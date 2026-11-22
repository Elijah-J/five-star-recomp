// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

namespace FiveStar.Shaders.Cache;

/// <summary>A tag 0x41 entry of a shader's metadata: one resource binding of the program.</summary>
/// <param name="Kind">The u32 at +4: 0x300 SRV, 0x100 SRV, 0x302 CBV, 0x002 CBV, 0x303 UAV, 0x401 sampler.</param>
/// <param name="Offset">The u32 at +8 (a byte offset; multiples of 8).</param>
/// <param name="Register">The u32 at +12: the shader register (bN, tN, uN or sN).</param>
/// <param name="Value">The u32 at +16.</param>
/// <param name="Layout">The u32 at +20.</param>
public readonly record struct UserDataEntry(uint Kind, uint Offset, uint Register, uint Value, uint Layout)
{
    // Kind codes, matched against the PSV0 resource bindings of the same program.
    private const uint ShaderResource = 0x300;
    private const uint BufferResource = 0x100;
    private const uint ConstantBuffer = 0x302;
    private const uint RootConstantBuffer = 0x002;
    private const uint UnorderedAccess = 0x303;
    private const uint Sampler = 0x401;

    /// <summary>Gets the register class of <see cref="Kind"/>: <c>srv</c>, <c>cbv</c>, <c>uav</c>, <c>sampler</c> or the code in hex.</summary>
    public string RegisterClass
    {
        get
        {
            switch (Kind)
            {
                case ShaderResource:
                case BufferResource:
                    return "srv";
                case ConstantBuffer:
                case RootConstantBuffer:
                    return "cbv";
                case UnorderedAccess:
                    return "uav";
                case Sampler:
                    return "sampler";
                default:
                    return $"0x{Kind:x}";
            }
        }
    }
}
