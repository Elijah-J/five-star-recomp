// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using FiveStar.Loader.Diagnostics;

namespace FiveStar.Loader.Shims.D3D12X.Binding;

/// <summary>
/// The GPU-memory window of the translated programs (heap slots 0 and 1, docs/shader-translation.md, Resources) as far
/// as descriptor tables need it. A program reads a table through a 32-bit pointer in a user SGPR: descriptor k of the
/// table at pointer + k * 32 (T# size; buffers and samplers use the first 16 bytes), and dword 0 of that descriptor is
/// the desktop heap index. The window holds one such 32-byte record per desktop view descriptor, then one 16-byte
/// record per sampler, each with dword 0 set to its own index; a table's pointer is the window offset of its first
/// record. Views keep their desktop index, so copies and view creation leave the window as it is.
/// </summary>
internal sealed unsafe class DescriptorWindow
{
    /// <summary>D3D12_MAX_SHADER_VISIBLE_DESCRIPTOR_HEAP_SIZE_TIER_2 and D3D12_MAX_SHADER_VISIBLE_SAMPLER_HEAP_SIZE.</summary>
    public const uint MaxViews = 1000000;
    public const uint MaxSamplers = 2048;

    /// <summary>Bytes of a view record (a T#) and a sampler record (an S#) in the window.</summary>
    public const uint ViewRecordBytes = 32;
    public const uint SamplerRecordBytes = 16;

    /// <summary>Heap slots the programs reach by constant: the window (rdna_prelude GPU_MEM_HEAP), its writable view, the mesh fallback table.</summary>
    public const uint WindowSlot = 0;
    public const uint WritableWindowSlot = 1;
    public const uint FallbackTableSlot = 2;

    /// <summary>Descriptors written for game handles past the desktop heap (each lands at the sink start).</summary>
    public const uint SinkSlot = FallbackTableSlot + FallbackTableSize;
    public const uint SinkSize = 256;

    /// <summary>The first desktop index of the game's descriptors.</summary>
    public const uint ReservedViews = SinkSlot + SinkSize;

    /// <summary>rdna_prelude STATIC_SAMPLER_BASE: samplers for the literal S#s of a program.</summary>
    public const uint StaticSamplerBase = 2032;

    public const uint SamplerBase = MaxViews * ViewRecordBytes;
    public const uint WindowBytes = SamplerBase + (MaxSamplers * SamplerRecordBytes);

    /// <summary>The mesh fallback table: u0..u2 then t0 (BindlessRootSignature).</summary>
    private const uint FallbackTableSize = 4;
    private const uint FallbackUavs = 3;
    private const int CommonState = 0;

    private readonly LoaderLog _log;
    private ulong _viewGpuStart;
    private uint _viewIncrement;
    private uint _viewCount;
    private ulong _samplerGpuStart;
    private uint _samplerIncrement;
    private uint _samplerCount;

    public DescriptorWindow(LoaderLog log)
    {
        _log = log;
    }

    /// <summary>The desktop shader-visible view heap, once the game created its heap.</summary>
    public nint ViewHeap { get; private set; }

    /// <summary>The desktop shader-visible sampler heap, once the game created its heap.</summary>
    public nint SamplerHeap { get; private set; }

    /// <summary>The GPU handle of the mesh fallback table (root parameter 1 of every translated program).</summary>
    public ulong FallbackTable => _viewGpuStart + ((ulong)FallbackTableSlot * _viewIncrement);

    /// <summary>The window buffer; 0 until the view heap exists.</summary>
    public nint Buffer { get; private set; }

    /// <summary>
    /// Sets up the window for the game's shader-visible view heap: the window buffer (CPU-writable, GPU-readable and
    /// writable), its two views, null fallback descriptors and one record per descriptor.
    /// </summary>
    public bool AttachViews(nint device, nint heap, uint count, uint increment)
    {
        var properties = new HeapProperties { Type = HeapProperties.Custom, CpuPageProperty = HeapProperties.WriteCombine, MemoryPool = HeapProperties.SystemPool };
        nint buffer = DesktopCalls.CreateBuffer(device, properties, WindowBytes, DesktopCalls.UnorderedAccessFlag, CommonState);
        byte* data = buffer == 0 ? null : DesktopCalls.Map(buffer);

        if (data == null)
        {
            _log.Warn("[d3d12_xs] descriptor window: no CPU-writable buffer, descriptor tables read nothing");

            return false;
        }

        FillRecords(data);
        nint cpu = DesktopCalls.HeapCpuStart(heap);
        WriteReserved(device, buffer, cpu, increment);

        Buffer = buffer;
        ViewHeap = heap;
        _viewGpuStart = DesktopCalls.HeapGpuStart(heap);
        _viewIncrement = increment;
        _viewCount = Math.Min(count, MaxViews);
        _log.Detail($"  [d3d12_xs] descriptor window: 0x{WindowBytes:x} bytes, game views from desktop index {ReservedViews}");

        return true;
    }

    /// <summary>Takes the game's shader-visible sampler heap and fills the static sampler slots with a trilinear sampler.</summary>
    public void AttachSamplers(nint device, nint heap, uint count, uint increment)
    {
        uint* sampler = stackalloc uint[TrilinearSampler.Dwords];
        TrilinearSampler.Write(sampler);
        nint cpu = DesktopCalls.HeapCpuStart(heap);

        for (uint slot = StaticSamplerBase; slot < count; slot++)
        {
            DesktopCalls.Sampler(device, sampler, cpu + (nint)(slot * increment));
        }

        SamplerHeap = heap;
        _samplerGpuStart = DesktopCalls.HeapGpuStart(heap);
        _samplerIncrement = increment;
        _samplerCount = Math.Min(count, MaxSamplers);
    }

    /// <summary>
    /// The 32-bit window pointer of the table that starts at a desktop GPU descriptor handle, for the user SGPR of a
    /// table parameter; null for a handle in neither heap.
    /// </summary>
    [HotPath]
    public uint? TableAddress(ulong handle)
    {
        ulong viewOffset = handle - _viewGpuStart;

        if (_viewIncrement != 0 && viewOffset < (ulong)_viewCount * _viewIncrement)
        {
            return (uint)(viewOffset / _viewIncrement) * ViewRecordBytes;
        }

        ulong samplerOffset = handle - _samplerGpuStart;

        if (_samplerIncrement != 0 && samplerOffset < (ulong)_samplerCount * _samplerIncrement)
        {
            return SamplerBase + ((uint)(samplerOffset / _samplerIncrement) * SamplerRecordBytes);
        }

        return null;
    }

    private static void FillRecords(byte* data)
    {
        new Span<byte>(data, (int)WindowBytes).Clear();

        for (uint view = 0; view < MaxViews; view++)
        {
            *(uint*)(data + (view * ViewRecordBytes)) = view;
        }

        for (uint sampler = 0; sampler < MaxSamplers; sampler++)
        {
            *(uint*)(data + SamplerBase + (sampler * SamplerRecordBytes)) = sampler;
        }
    }

    private static void WriteReserved(nint device, nint buffer, nint cpu, uint increment)
    {
        var read = BufferViewDescription.RawShaderResource(WindowBytes / sizeof(uint));
        var write = BufferViewDescription.RawUnorderedAccess(WindowBytes / sizeof(uint));
        DesktopCalls.ShaderResourceView(device, buffer, &read, cpu + (nint)(WindowSlot * increment));
        DesktopCalls.UnorderedAccessView(device, buffer, &write, cpu + (nint)(WritableWindowSlot * increment));

        // Null descriptors: the fallback table the root signature asks for, and the sink for descriptors past the heap.
        var nullWrite = BufferViewDescription.RawUnorderedAccess(0);
        var nullRead = BufferViewDescription.RawShaderResource(0);

        for (uint slot = FallbackTableSlot; slot < FallbackTableSlot + FallbackUavs; slot++)
        {
            DesktopCalls.UnorderedAccessView(device, 0, &nullWrite, cpu + (nint)(slot * increment));
        }

        for (uint slot = FallbackTableSlot + FallbackUavs; slot < ReservedViews; slot++)
        {
            DesktopCalls.ShaderResourceView(device, 0, &nullRead, cpu + (nint)(slot * increment));
        }
    }
}
