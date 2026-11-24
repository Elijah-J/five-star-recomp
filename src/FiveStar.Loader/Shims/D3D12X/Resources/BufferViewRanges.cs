// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using FiveStar.Loader.Shims.Xg;

namespace FiveStar.Loader.Shims.D3D12X.Resources;

/// <summary>
/// Buffer SRVs and UAVs (device slots 18 and 19) of a buffer at a console address. A slot-133 buffer whose pages lie in
/// two page sections is one desktop buffer per run (<see cref="XboxPlacedResources"/>), and the game's object is the
/// first; a view over the whole buffer does not fit it and removes the device. One in memory the game committed itself
/// shares that memory's buffer, so its first element moves by its address. The view goes to the run that holds its
/// first element, moved to that run's offset and cut at its end (elements past a cut read 0). A view that fits its
/// buffer passes unchanged.
/// </summary>
internal static unsafe class BufferViewRanges
{
    /// <summary>D3D12_SRV_DIMENSION_BUFFER / D3D12_UAV_DIMENSION_BUFFER and the RAW flag (d3d12.h).</summary>
    private const int BufferDimension = 1;
    private const int RawFlag = 1;
    private const uint RawElementBytes = sizeof(uint);

    /// <summary>D3D12_BUFFER_SRV fields: FirstElement, NumElements, StructureByteStride, Flags (byte offsets).</summary>
    private const int SrvFirst = 16;
    private const int SrvCount = 24;
    private const int SrvStride = 28;

    /// <summary>D3D12_BUFFER_UAV fields: FirstElement, NumElements, StructureByteStride.</summary>
    private const int UavFirst = 8;
    private const int UavCount = 16;
    private const int UavStride = 20;

    private const int Flags = 32;
    private const int ViewSize = 40;

    /// <summary>
    /// The desktop resource and desc (<paramref name="view"/>, possibly <paramref name="copy"/>) for a buffer view on
    /// <paramref name="wrapper"/>, whose desktop buffer is <paramref name="desktop"/>.
    /// </summary>
    [HotPath]
    public static nint Fit(nint wrapper, nint desktop, void* description, byte* copy, bool unordered, out void* view)
    {
        view = description;
        var resource = ShimContext.Runtime.D3D12X.Objects.AsResource(wrapper);

        if (description == null || desktop == 0 || resource == null || resource->GameAddress == 0 || ((int*)description)[1] != BufferDimension)
        {
            return desktop;
        }

        if (unordered)
        {
            return Fitted(resource->GameAddress, desktop, (byte*)description, copy, UavFirst, UavCount, UavStride, ref view);
        }

        return Fitted(resource->GameAddress, desktop, (byte*)description, copy, SrvFirst, SrvCount, SrvStride, ref view);
    }

    [HotPath]
    private static nint Fitted(ulong game, nint desktop, byte* description, byte* copy, int firstAt, int countAt, int strideAt, ref void* view)
    {
        uint element = ElementBytes(description, strideAt);
        ulong start = game + (*(ulong*)(description + firstAt) * element);
        uint count = *(uint*)(description + countAt);

        if (element == 0 || !ShimContext.Runtime.D3D12X.GameMemory.TryFind(start, out var range) || range.Resource == 0)
        {
            return desktop;
        }

        ulong end = range.GameAddress + range.Size;

        if ((range.Resource == desktop && range.GameAddress == game && start + ((ulong)count * element) <= end) || (start - range.GameAddress) % element != 0)
        {
            return desktop;
        }

        ulong fits = (end - start) / element;
        Buffer.MemoryCopy(description, copy, ViewSize, ViewSize);
        *(ulong*)(copy + firstAt) = (start - range.GameAddress) / element;

        if (fits < count)
        {
            count = (uint)fits;
        }

        *(uint*)(copy + countAt) = count;
        view = copy;

        return range.Resource;
    }

    /// <summary>Bytes per element: 4 for raw views, the stride of structured ones, the format's size for typed ones; 0 when unknown.</summary>
    [HotPath]
    private static uint ElementBytes(byte* description, int strideAt)
    {
        if ((*(int*)(description + Flags) & RawFlag) != 0)
        {
            return RawElementBytes;
        }

        uint stride = *(uint*)(description + strideAt);

        if (stride != 0)
        {
            return stride;
        }

        int format = *(int*)description;

        if (format == ViewFormats.Unknown)
        {
            return 0;
        }

        return (uint)XgFormats.Find((uint)format).BytesPerElement;
    }
}
