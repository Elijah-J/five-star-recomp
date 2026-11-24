// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Vortice.Direct3D12;

namespace FiveStar.Loader.Shims.D3D12X.Pipelines;

/// <summary>
/// A desktop pipeline-state stream in native memory: each subobject is a u32 D3D12_PIPELINE_STATE_SUBOBJECT_TYPE and
/// its payload at the payload's alignment, every subobject pointer-aligned. Shader bytecode, input elements and their
/// semantic names live in native allocations freed on disposal. Alongside, a SHA-256 runs over the stream's content
/// with every pointer replaced by what it points to (the root signature by nothing: the cache's salt covers it), so
/// <see cref="Key"/> names the pipeline the same way in every process.
/// </summary>
internal sealed unsafe class DesktopStream : IDisposable
{
    /// <summary>Bytes of the SHA-256 kept in <see cref="Key"/>.</summary>
    private const int KeyBytes = 16;

    private const int WordSize = sizeof(int);
    private const int StreamDescWords = 2;

    private readonly List<byte> _bytes = [];
    private readonly List<nint> _allocations = [];
    private readonly IncrementalHash _identity = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    private nint* _desc;
    private string? _key;

    /// <summary>The bytes of shader bytecode in the stream.</summary>
    public long ShaderBytes { get; private set; }

    /// <summary>The pipeline's name in the pipeline cache: 32 hex digits of the content hash.</summary>
    public string Key => _key ??= Convert.ToHexStringLower(_identity.GetCurrentHash().AsSpan(0, KeyBytes));

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
        ShaderBytes += bytecode.Length;
        Identify(BitConverter.GetBytes(bytecode.Length));
        Identify(bytecode);
    }

    /// <summary>A subobject whose payload is a desc with the d3d12.h layout.</summary>
    public void AddBytes(PipelineStateSubObjectType type, ReadOnlySpan<byte> payload)
    {
        Begin(type, WordSize);
        _bytes.AddRange(payload);
        Identify(payload);
    }

    public void AddWords(PipelineStateSubObjectType type, IReadOnlyList<int> words)
    {
        Begin(type, WordSize);

        foreach (var word in words)
        {
            var bytes = BitConverter.GetBytes(word);
            _bytes.AddRange(bytes);
            Identify(bytes);
        }
    }

    /// <summary>A D3D12_INPUT_LAYOUT_DESC over native D3D12_INPUT_ELEMENT_DESCs.</summary>
    public void AddInputLayout(IReadOnlyList<VertexElement> elements)
    {
        var table = new byte[elements.Count * PipelineLayout.InputElementSize];
        Identify(BitConverter.GetBytes(elements.Count));

        for (var index = 0; index < elements.Count; index++)
        {
            var element = elements[index];
            int[] fields = [element.SemanticIndex, element.Format, element.InputSlot, element.Offset, element.Classification, element.StepRate];
            var row = table.AsSpan(index * PipelineLayout.InputElementSize);
            BitConverter.TryWriteBytes(row, (long)NativeString(element.Semantic));
            Identify(System.Text.Encoding.ASCII.GetBytes(element.Semantic + "\0"));

            for (var field = 0; field < fields.Length; field++)
            {
                BitConverter.TryWriteBytes(row[(PipelineLayout.PointerSize + (field * WordSize))..], fields[field]);
                Identify(BitConverter.GetBytes(fields[field]));
            }
        }

        Begin(PipelineStateSubObjectType.InputLayout, PipelineLayout.PointerSize);
        _bytes.AddRange(BitConverter.GetBytes((long)Native(table)));
        _bytes.AddRange(BitConverter.GetBytes((long)elements.Count));
    }

    /// <summary>
    /// The D3D12_PIPELINE_STATE_STREAM_DESC {SizeInBytes, pPipelineStateSubobjectStream} over the stream; built once,
    /// so no subobject may be added after the first call.
    /// </summary>
    public void* Describe()
    {
        if (_desc != null)
        {
            return _desc;
        }

        Align(PipelineLayout.PointerSize);
        var stream = Native(_bytes.ToArray());
        var desc = (nint*)NativeMemory.Alloc((nuint)(StreamDescWords * sizeof(nint)));
        _allocations.Add((nint)desc);

        desc[0] = _bytes.Count;
        desc[1] = stream;
        _desc = desc;

        return desc;
    }

    /// <summary>ID3D12Device2::CreatePipelineState over the stream.</summary>
    public int Create(nint device2, Guid* iid, nint* result)
    {
        return DesktopDevice.CreatePipelineState(device2, Describe(), iid, result);
    }

    public void Dispose()
    {
        foreach (var allocation in _allocations)
        {
            NativeMemory.Free((void*)allocation);
        }

        _allocations.Clear();
        _desc = null;
        _identity.Dispose();
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
        var tag = BitConverter.GetBytes((int)type);
        _bytes.AddRange(tag);
        Identify(tag);
        Align(payloadAlignment);
    }

    private void Identify(ReadOnlySpan<byte> content)
    {
        _key = null;
        _identity.AppendData(content);
    }

    private void Align(int alignment)
    {
        while (_bytes.Count % alignment != 0)
        {
            _bytes.Add(0);
        }
    }
}
