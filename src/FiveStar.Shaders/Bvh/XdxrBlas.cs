// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Buffers.Binary;

namespace FiveStar.Shaders.Bvh;

/// <summary>
/// A BLAS built on the CPU in the console's structure layout (docs/raytracing.md): the 64-byte header a rehydrated BLAS
/// has, then the <see cref="Bvh4Builder"/> nodes. The shaders and a TLAS take the root node pointer from dword 12;
/// node pointers count from the start of the structure.
/// </summary>
public static class XdxrBlas
{
    /// <summary>Header bytes; the nodes follow.</summary>
    public const int HeaderSize = 0x40;

    /// <summary>The structure is a whole number of 64-byte blocks; dword 1 holds the count minus one.</summary>
    public const int BlockSize = 64;

    /// <summary>The header dword holding the root node pointer.</summary>
    public const int RootOffset = 0x30;

    // Header dwords (docs/raytracing.md, serialized BLAS): size, triangle count, bounds, node offsets, copy size,
    // vertex transform.
    private const int BlocksOffset = 0x04;
    private const int TrianglesOffset = 0x0C;
    private const int BoundsOffset = 0x10;
    private const int BoxesOffset = 0x28;
    private const int CopySizeFlagOffset = 0x2C;
    private const int TriangleNodesOffset = 0x34;
    private const int CopySizeOffset = 0x38;
    private const int VertexTransformOffset = 0x3C;
    private const int FloatSize = 4;
    private const int Axes = 3;

    /// <summary>Dword 15 without a vertex transform: three scale exponents of 127 (2^0).</summary>
    private const uint NoVertexTransform = 0x7F7F7F;

    /// <summary>Builds the structure over <paramref name="triangles"/> (transformed vertices).</summary>
    public static byte[] Build(IReadOnlyList<BvhTriangle> triangles)
    {
        var image = Bvh4Builder.Build(triangles, HeaderSize);
        int size = HeaderSize + image.Nodes.Length;
        var bytes = new byte[size];
        image.Nodes.CopyTo(bytes.AsSpan(HeaderSize));
        WriteHeader(bytes, image, Bounds(triangles));

        return bytes;
    }

    /// <summary>The structure size from a header (dword 1).</summary>
    public static ulong Size(ReadOnlySpan<byte> header)
    {
        return ((ulong)BinaryPrimitives.ReadUInt32LittleEndian(header[BlocksOffset..]) + 1) * BlockSize;
    }

    private static void WriteHeader(Span<byte> bytes, Bvh4Image image, BvhBounds bounds)
    {
        int triangleNodes = HeaderSize + (image.BoxNodes * AmdBvhLayout.BoxNodeSize);
        Write(bytes, BlocksOffset, (uint)((bytes.Length / BlockSize) - 1));
        Write(bytes, TrianglesOffset, (uint)image.TriangleNodes);

        for (int axis = 0; axis < Axes; axis++)
        {
            BinaryPrimitives.WriteSingleLittleEndian(bytes[(BoundsOffset + (FloatSize * axis))..], bounds.Min[axis]);
            BinaryPrimitives.WriteSingleLittleEndian(bytes[(BoundsOffset + (FloatSize * (Axes + axis)))..], bounds.Max[axis]);
        }

        Write(bytes, BoxesOffset, HeaderSize);
        Write(bytes, CopySizeFlagOffset, 1);
        Write(bytes, RootOffset, image.Root);
        Write(bytes, TriangleNodesOffset, (uint)triangleNodes);
        Write(bytes, CopySizeOffset, (uint)bytes.Length);
        Write(bytes, VertexTransformOffset, NoVertexTransform);
    }

    private static BvhBounds Bounds(IReadOnlyList<BvhTriangle> triangles)
    {
        var bounds = BvhBounds.Empty;

        foreach (var triangle in triangles)
        {
            bounds = bounds.Union(BvhBounds.Of(triangle));
        }

        return bounds;
    }

    private static void Write(Span<byte> bytes, int offset, uint value)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(bytes[offset..], value);
    }
}
