// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using FiveStar.Loader.Shims.D3D12X.Resources;

namespace FiveStar.Loader.Shims.Bvh;

/// <summary>
/// D3D12_RAYTRACING_GEOMETRY_DESC with triangles (d3d12.h): Type, Flags, then Transform3x4, IndexFormat, VertexFormat,
/// IndexCount, VertexCount, IndexBuffer, VertexBuffer {StartAddress, StrideInBytes}. Translation swaps its three GPU
/// addresses for the CPU addresses of the same memory, after checking every byte they cover is readable.
/// </summary>
internal static unsafe class TriangleGeometry
{
    public const int Size = 0x38;

    private const int TypeOffset = 0x00;
    private const int TransformOffset = 0x08;
    private const int IndexFormatOffset = 0x10;
    private const int IndexCountOffset = 0x18;
    private const int VertexCountOffset = 0x1C;
    private const int IndexBufferOffset = 0x20;
    private const int VertexStartOffset = 0x28;
    private const int VertexStrideOffset = 0x30;

    /// <summary>D3D12_RAYTRACING_GEOMETRY_TYPE_TRIANGLES.</summary>
    private const uint Triangles = 0;

    /// <summary>A row-major 3x4 float matrix.</summary>
    private const ulong TransformSize = 48;

    /// <summary>DXGI_FORMAT_R32_UINT and DXGI_FORMAT_R16_UINT (dxgiformat.h).</summary>
    private const uint IndexR32 = 42;
    private const uint IndexR16 = 57;

    private const ulong WideIndex = 4;
    private const ulong NarrowIndex = 2;

    /// <summary>Copies <paramref name="geometry"/> to <paramref name="copy"/> with CPU addresses; false when a range is unreadable.</summary>
    public static bool TryTranslate(byte* geometry, byte* copy, GameMemorySource memory)
    {
        new ReadOnlySpan<byte>(geometry, Size).CopyTo(new Span<byte>(copy, Size));

        if (*(uint*)(geometry + TypeOffset) != Triangles)
        {
            return false;
        }

        ulong stride = *(ulong*)(geometry + VertexStrideOffset);
        ulong vertices = stride * *(uint*)(geometry + VertexCountOffset);
        ulong indices = IndexSize(*(uint*)(geometry + IndexFormatOffset)) * *(uint*)(geometry + IndexCountOffset);

        return Translate(copy + VertexStartOffset, vertices, memory)
            && Translate(copy + IndexBufferOffset, indices, memory)
            && Translate(copy + TransformOffset, TransformSize, memory);
    }

    /// <summary>Replaces the address at <paramref name="field"/>; an absent address (0) or an empty range stays as it is.</summary>
    private static bool Translate(byte* field, ulong size, GameMemorySource memory)
    {
        ulong address = *(ulong*)field;

        if (address == 0 || size == 0)
        {
            return true;
        }

        byte* cpu = memory.Find(address, size);

        if (cpu == null)
        {
            return false;
        }

        *(ulong*)field = (ulong)cpu;

        return true;
    }

    private static ulong IndexSize(uint format)
    {
        if (format == IndexR32)
        {
            return WideIndex;
        }

        if (format == IndexR16)
        {
            return NarrowIndex;
        }

        return 0;
    }
}
