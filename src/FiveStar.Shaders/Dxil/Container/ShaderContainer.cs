// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Buffers.Binary;
using FiveStar.Shaders.Dxil.Ir;

namespace FiveStar.Shaders.Dxil.Container;

/// <summary>
/// Finishes a compute module: the DXIL metadata (versions, shader model 6.6, the user-data constant buffer, the entry
/// point with flags, thread-group size and wave size), the bitcode, and the container (SFI0, ISG1, OSG1, PSV0, DXIL)
/// with its digest.
/// </summary>
public static class ShaderContainer
{
    public const string UserDataName = "UserData";

    private const uint Major = 1;
    private const uint Minor = 6;
    private const uint ModelMajor = 6;
    private const int ComputeKind = 5;
    private const uint FlagsTag = 0;
    private const uint ThreadsTag = 4;
    private const uint WaveSizeTag = 11;
    private const uint CbufferKind = 13;
    private const uint PsvCbuffer = 2;
    private const int KindShift = 16;
    private const int MajorShift = 4;
    private const uint DxilVersion = 0x106;
    private const int ProgramHeaderBytes = 24;
    private const int BitcodeHeaderBytes = 16;
    private const int UserDataWords = 128;

    private static readonly (ShaderFlagBits Flag, ulong Feature)[] Features =
    [
        (ShaderFlagBits.EnableDoublePrecision, 0x1),
        (ShaderFlagBits.EnableDoubleExtensions, 0x20),
        (ShaderFlagBits.StencilExport, 0x200),
        (ShaderFlagBits.TypedUavLoadAdditionalFormats, 0x800),
        (ShaderFlagBits.WaveOps, 0x4000),
        (ShaderFlagBits.Int64Ops, 0x8000),
        (ShaderFlagBits.Barycentrics, 0x20000),
        (ShaderFlagBits.AtomicInt64OnGroupShared, 0x80_0000),
        (ShaderFlagBits.ResourceDescriptorHeapIndexing, 0x200_0000),
        (ShaderFlagBits.SamplerDescriptorHeapIndexing, 0x400_0000),
    ];

    public static byte[] Write(DxilModule module, IrFunction main, ShaderDescription description)
    {
        var bindings = new List<PsvBinding>();
        var resources = Resources(module, description, bindings);
        var entry = module.Node(module.ValueMetadata(main), module.Text("main"), null, resources, Properties(module, description));
        module.Named("llvm.ident", module.Node(module.Text("Five Star Recomp")));
        var version = module.Node(module.Int32Metadata(Major), module.Int32Metadata(Minor));
        module.Named("dx.version", version);
        module.Named("dx.valver", version);
        module.Named("dx.shaderModel", module.Node(module.Text("cs"), module.Int32Metadata(ModelMajor), module.Int32Metadata(Minor)));

        if (resources is not null)
        {
            module.Named("dx.resources", resources);
        }

        module.Named("dx.entryPoints", entry);
        var program = Program(ModuleWriter.Write(module));
        var psv = PsvWriter.Compute(ComputeKind, description.Threads, description.WaveSize, bindings);

        return ContainerWriter.Build(
        [
            ("SFI0", Sfi0(description.Flags)),
            ("ISG1", EmptySignature()),
            ("OSG1", EmptySignature()),
            ("PSV0", psv),
            ("DXIL", program),
        ]);
    }

    private static Metadata? Resources(DxilModule module, ShaderDescription description, List<PsvBinding> bindings)
    {
        if (description.UserDataBytes == 0)
        {
            return null;
        }

        var types = module.Types;
        var layout = types.Struct(UserDataName, [types.Array(UserDataWords, types.Int32)]);
        var pointer = module.Undefined(types.Pointer(layout, 0));
        var cbuffer = module.Node(
            module.Int32Metadata(0), module.ValueMetadata(pointer), module.Text(string.Empty), module.Int32Metadata(0),
            module.Int32Metadata(0), module.Int32Metadata(1), module.Int32Metadata((uint)description.UserDataBytes), null);
        bindings.Add(new PsvBinding(PsvCbuffer, 0, 0, CbufferKind));

        return module.Node(null, null, module.Node(cbuffer), null);
    }

    private static Metadata Properties(DxilModule module, ShaderDescription description)
    {
        var properties = new List<Metadata?>();

        if (description.Flags != ShaderFlagBits.None)
        {
            properties.Add(module.Int32Metadata(FlagsTag));
            properties.Add(module.ValueMetadata(module.Int(module.Types.Int64, (long)description.Flags)));
        }

        var (x, y, z) = description.Threads;
        properties.Add(module.Int32Metadata(ThreadsTag));
        properties.Add(module.Node(module.Int32Metadata((uint)x), module.Int32Metadata((uint)y), module.Int32Metadata((uint)z)));

        if (description.WaveSize is not null)
        {
            properties.Add(module.Int32Metadata(WaveSizeTag));
            properties.Add(module.Node(module.Int32Metadata((uint)description.WaveSize.Value)));
        }

        return module.Node([.. properties]);
    }

    /// <summary>The DXIL part: program header (kind, version, size in dwords), bitcode header, bitcode.</summary>
    private static byte[] Program(byte[] bitcode)
    {
        var bytes = new byte[ProgramHeaderBytes + bitcode.Length];
        var version = ((uint)ComputeKind << KindShift) | (ModelMajor << MajorShift) | Minor;
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, version);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(sizeof(uint)), (uint)(bytes.Length / sizeof(uint)));
        "DXIL"u8.CopyTo(bytes.AsSpan(sizeof(ulong)));
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(sizeof(ulong) + sizeof(uint)), DxilVersion);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(BitcodeHeaderBytes), BitcodeHeaderBytes);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(BitcodeHeaderBytes + sizeof(uint)), (uint)bitcode.Length);
        bitcode.CopyTo(bytes, ProgramHeaderBytes);

        return bytes;
    }

    private static byte[] Sfi0(ShaderFlagBits flags)
    {
        ulong features = 0;

        foreach (var (flag, feature) in Features)
        {
            if (flags.HasFlag(flag))
            {
                features |= feature;
            }
        }

        return BitConverter.GetBytes(features);
    }

    /// <summary>A signature part with no elements: count 0, elements at offset 8.</summary>
    private static byte[] EmptySignature()
    {
        var bytes = new byte[sizeof(ulong)];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(sizeof(uint)), sizeof(ulong));

        return bytes;
    }
}
