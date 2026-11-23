// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Buffers.Binary;

namespace FiveStar.Formats.Models;

/// <summary>Decoders of the 4- and 8-byte attribute streams (normals, texture coordinates).</summary>
public static class VertexAttributes
{
    /// <summary>One unit of a <see cref="VertexFormat.Fixed16x2"/> value (checked on posters: 0..1 across the texture).</summary>
    public const float FixedUnit = 1f / 8192;

    private const int NormalBits = 10;
    private const uint NormalMask = (1u << NormalBits) - 1;
    private const float NormalHalfRange = NormalMask / 2f;
    private const int Components = 3;
    private const int Pair = 2;

    /// <summary>
    /// Octahedral normals: x = bits 0-9, y = bits 10-19, each unorm mapped to -1..1; z = 1 - |x| - |y|, folded when
    /// negative. Returns x, y, z triples of unit length.
    /// </summary>
    public static float[] OctahedralNormals(ReadOnlySpan<byte> stream, int count)
    {
        var normals = new float[count * Components];

        for (var vertex = 0; vertex < count; vertex++)
        {
            var word = BinaryPrimitives.ReadUInt32LittleEndian(stream[(vertex * sizeof(uint))..]);
            var x = ((word & NormalMask) / NormalHalfRange) - 1;
            var y = (((word >> NormalBits) & NormalMask) / NormalHalfRange) - 1;
            var z = 1 - MathF.Abs(x) - MathF.Abs(y);

            if (z < 0)
            {
                var foldedX = (1 - MathF.Abs(y)) * MathF.CopySign(1, x);
                y = (1 - MathF.Abs(x)) * MathF.CopySign(1, y);
                x = foldedX;
            }

            var length = MathF.Sqrt((x * x) + (y * y) + (z * z));
            normals[vertex * Components] = x / length;
            normals[(vertex * Components) + 1] = y / length;
            normals[(vertex * Components) + Pair] = z / length;
        }

        return normals;
    }

    /// <summary>A <see cref="VertexFormat.Float3"/> stream as x, y, z triples.</summary>
    public static float[] Floats(ReadOnlySpan<byte> stream, int count)
    {
        var values = new float[count * Components];

        for (var index = 0; index < values.Length; index++)
        {
            values[index] = BinaryPrimitives.ReadSingleLittleEndian(stream[(index * sizeof(float))..]);
        }

        return values;
    }

    /// <summary>Texture coordinates as u, v pairs (v = 0 at the top of the texture) from a 4- or 8-byte stream.</summary>
    public static float[] TexCoords(ReadOnlySpan<byte> stream, byte format, int count)
    {
        var coordinates = new float[count * Pair];

        for (var index = 0; index < coordinates.Length; index++)
        {
            if (format == VertexFormat.Eight)
            {
                coordinates[index] = BinaryPrimitives.ReadSingleLittleEndian(stream[(index * sizeof(float))..]);
            }
            else
            {
                coordinates[index] = BinaryPrimitives.ReadInt16LittleEndian(stream[(index * sizeof(short))..]) * FixedUnit;
            }
        }

        return coordinates;
    }
}
