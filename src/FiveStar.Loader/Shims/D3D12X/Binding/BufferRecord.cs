// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

namespace FiveStar.Loader.Shims.D3D12X.Binding;

/// <summary>
/// Dwords 1 to 3 of a buffer view's descriptor record, the V# fields translated programs read besides the heap index
/// in dword 0 (<see cref="BufferRecords"/>). All 0 for a texture view.
/// </summary>
/// <param name="Stride">Dword 1: bytes an indexed access (idxen) multiplies its index by; 0 for a raw view.</param>
/// <param name="Records">Dword 2: num_records, elements of the stride (bytes when the stride is 0).</param>
/// <param name="Format">Dword 3: the format code buffer_load/store_format decode with (<see cref="BufferFormatCodes"/>).</param>
internal readonly record struct BufferRecord(uint Stride, uint Records, uint Format)
{
    /// <summary>The fields of a descriptor that is no buffer view.</summary>
    public static BufferRecord None => default;
}
