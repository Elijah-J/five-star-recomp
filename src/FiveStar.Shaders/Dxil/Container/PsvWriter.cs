// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Buffers.Binary;

namespace FiveStar.Shaders.Dxil.Container;

/// <summary>
/// PSV0 (pipeline state validation) with runtime info version 2, as validator 1.6 writes it, for a shader without
/// signature elements: runtime info, the bound resources, an empty string table and semantic index table.
/// </summary>
public static class PsvWriter
{
    private const int RuntimeInfoBytes = 48;
    private const int BindingBytes = 24;
    private const int MinimumWaveOffset = 16;
    private const int MaximumWaveOffset = 20;
    private const int StageOffset = 24;
    private const int ThreadsOffset = 36;
    private const int ThreadsZOffset = 44;
    private const int EmptyStringTable = 4;

    /// <summary>The part for a compute shader of <paramref name="kind"/> (PSV stage number).</summary>
    public static byte[] Compute(int kind, (int X, int Y, int Z) threads, int? waveSize, IReadOnlyList<PsvBinding> bindings)
    {
        var info = new byte[RuntimeInfoBytes];
        var minimum = (uint)(waveSize ?? 0);
        var maximum = waveSize is null ? uint.MaxValue : (uint)waveSize.Value;
        BinaryPrimitives.WriteUInt32LittleEndian(info.AsSpan(MinimumWaveOffset), minimum);
        BinaryPrimitives.WriteUInt32LittleEndian(info.AsSpan(MaximumWaveOffset), maximum);
        info[StageOffset] = (byte)kind;
        BinaryPrimitives.WriteUInt32LittleEndian(info.AsSpan(ThreadsOffset), (uint)threads.X);
        BinaryPrimitives.WriteUInt32LittleEndian(info.AsSpan(ThreadsOffset + sizeof(uint)), (uint)threads.Y);
        BinaryPrimitives.WriteUInt32LittleEndian(info.AsSpan(ThreadsZOffset), (uint)threads.Z);

        var output = new List<byte>();
        Add(output, RuntimeInfoBytes);
        output.AddRange(info);
        Add(output, (uint)bindings.Count);

        if (bindings.Count > 0)
        {
            Add(output, BindingBytes);

            foreach (var binding in bindings)
            {
                Add(output, binding.Type, binding.Space, binding.Register, binding.Register, binding.Kind, 0);
            }
        }

        Add(output, EmptyStringTable, 0);
        Add(output, 0);

        return [.. output];
    }

    private static void Add(List<byte> output, params uint[] values)
    {
        Span<byte> word = stackalloc byte[sizeof(uint)];

        foreach (var value in values)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(word, value);
            output.AddRange(word.ToArray());
        }
    }
}
