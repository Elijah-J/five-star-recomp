// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Runtime.InteropServices;
using Vortice.Direct3D12;

namespace FiveStar.Loader.Shims.D3D12X.Pipelines;

/// <summary>
/// A desktop pipeline-state stream in native memory: each subobject is a u32 D3D12_PIPELINE_STATE_SUBOBJECT_TYPE and
/// its payload at the payload's alignment, every subobject pointer-aligned. Shader bytecode, input elements and their
/// semantic names live in native allocations freed on disposal.
/// </summary>
internal sealed unsafe class DesktopStream : IDisposable
{
    private const int WordSize = sizeof(int);
    private const int StreamDescWords = 2;

    private readonly List<byte> _bytes = [];
    private readonly List<nint> _allocations = [];

    public void AddPointer(PipelineStateSubObjectType type, nint address)
    {
        Begin(type, PipelineLayout.PointerSize);
        _bytes.AddRange(BitConverter.GetBytes((long)address));
    }

    /// <summary>A D3D12_SHADER_BYTECODE subobject over a native copy of <paramref name="bytecode"/>.</summary>
    public void AddShader(PipelineStateSubObjectType type, byte[] bytecode)
    {
        Begin(type, PipelineLayout.PointerSize);
        _bytes.AddRange(BitConverter.GetBytes((long)Native(bytecode)));
        _bytes.AddRange(BitConverter.GetBytes((long)bytecode.Length));
    }

    /// <summary>A subobject whose payload is a desc with the d3d12.h layout.</summary>
    public void AddBytes(PipelineStateSubObjectType type, ReadOnlySpan<byte> payload)
    {
        Begin(type, WordSize);
        _bytes.AddRange(payload);
    }

    public void AddWords(PipelineStateSubObjectType type, IReadOnlyList<int> words)
    {
        Begin(type, WordSize);

        foreach (var word in words)
        {
            _bytes.AddRange(BitConverter.GetBytes(word));
        }
    }

    /// <summary>A D3D12_INPUT_LAYOUT_DESC over native D3D12_INPUT_ELEMENT_DESCs.</summary>
    public void AddInputLayout(IReadOnlyList<VertexElement> elements)
    {
        var table = new byte[elements.Count * PipelineLayout.InputElementSize];

        for (var index = 0; index < elements.Count; index++)
        {
            var element = elements[index];
            int[] fields = [element.SemanticIndex, element.Format, element.InputSlot, element.Offset, element.Classification, element.StepRate];
            var row = table.AsSpan(index * PipelineLayout.InputElementSize);
            BitConverter.TryWriteBytes(row, (long)NativeString(element.Semantic));

            for (var field = 0; field < fields.Length; field++)
            {
                BitConverter.TryWriteBytes(row[(PipelineLayout.PointerSize + (field * WordSize))..], fields[field]);
            }
        }

        Begin(PipelineStateSubObjectType.InputLayout, PipelineLayout.PointerSize);
        _bytes.AddRange(BitConverter.GetBytes((long)Native(table)));
        _bytes.AddRange(BitConverter.GetBytes((long)elements.Count));
    }

    /// <summary>ID3D12Device2::CreatePipelineState over the stream.</summary>
    public int Create(nint device2, Guid* iid, nint* result)
    {
        Align(PipelineLayout.PointerSize);
        var stream = Native(_bytes.ToArray());
        // D3D12_PIPELINE_STATE_STREAM_DESC {SizeInBytes, pPipelineStateSubobjectStream}.
        var desc = stackalloc nint[StreamDescWords];
        desc[0] = _bytes.Count;
        desc[1] = stream;

        return DesktopDevice.CreatePipelineState(device2, desc, iid, result);
    }

    public void Dispose()
    {
        foreach (var allocation in _allocations)
        {
            NativeMemory.Free((void*)allocation);
        }

        _allocations.Clear();
    }

    private nint Native(byte[] bytes)
    {
        var copy = (byte*)NativeMemory.Alloc((nuint)Math.Max(bytes.Length, 1));
        bytes.CopyTo(new Span<byte>(copy, bytes.Length));
        _allocations.Add((nint)copy);

        return (nint)copy;
    }

    private nint NativeString(string text)
    {
        var bytes = new byte[text.Length + 1];
        System.Text.Encoding.ASCII.GetBytes(text, bytes);

        return Native(bytes);
    }

    private void Begin(PipelineStateSubObjectType type, int payloadAlignment)
    {
        Align(PipelineLayout.PointerSize);
        _bytes.AddRange(BitConverter.GetBytes((int)type));
        Align(payloadAlignment);
    }

    private void Align(int alignment)
    {
        while (_bytes.Count % alignment != 0)
        {
            _bytes.Add(0);
        }
    }
}
