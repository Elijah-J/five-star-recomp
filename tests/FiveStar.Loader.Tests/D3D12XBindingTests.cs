// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Diagnostics;
using FiveStar.Loader.Shims.D3D12X.Binding;
using FiveStar.Loader.Tests.Support;
using Vortice.Direct3D12;
using Xunit;
using HeapProperties = FiveStar.Loader.Shims.D3D12X.Binding.HeapProperties;

namespace FiveStar.Loader.Tests;

/// <summary>
/// Command-list binding through the D3D12.X slot numbers: root signature layouts, and a dispatch whose descriptor table
/// a program reads the way translated programs do (user SGPR, window record, heap index).
/// </summary>
public sealed unsafe class D3D12XBindingTests
{
    private const uint Marker = 0x1234;
    private const int CreateDescriptorHeapSlot = 14;
    private const int CreateRootSignatureSlot = 16;
    private const int CreateShaderResourceViewSlot = 18;
    private const int CreateUnorderedAccessViewSlot = 19;
    private const int DescriptorIncrementSlot = 15;
    private const int HeapCpuStartSlot = 9;
    private const int HeapGpuStartSlot = 10;
    private const int ListCloseSlot = 9;
    private const int ListDispatchSlot = 14;
    private const int ListSetPipelineSlot = 25;
    private const int ListSetHeapsSlot = 32;
    private const int ListSetComputeRootSlot = 33;
    private const int ListSetComputeTableSlot = 35;
    private const int QueueExecuteSlot = 35;
    private const int QueueSignalSlot = 16;
    private const int FenceWaitSlot = 9;
    private const int GenericRead = 0xAC3;
    private const int WriteBack = 3;

    /// <summary>Reads table entry 0 (an SRV) and 1 (a UAV) through the window and stores the SRV's first dword + 1.</summary>
    private const string Program = """
        cbuffer UserData : register(b0) { uint4 g_user4[32]; };
        [numthreads(1, 1, 1)]
        void main()
        {
            uint table = g_user4[1].x;
            ByteAddressBuffer window = ResourceDescriptorHeap[0];
            ByteAddressBuffer source = ResourceDescriptorHeap[window.Load(table)];
            RWByteAddressBuffer destination = ResourceDescriptorHeap[window.Load(table + 32)];
            destination.Store(0, source.Load(0) + 1);
        }
        """;

    [Fact]
    public void A_root_signature_packs_its_parameters_into_user_sgprs()
    {
        var blob = Serialize(
            new RootParameter1(new RootDescriptorTable1(new DescriptorRange1(DescriptorRangeType.ShaderResourceView, 1, 0)), ShaderVisibility.All),
            new RootParameter1(new RootConstants(0, 0, 3), ShaderVisibility.All),
            new RootParameter1(RootParameterType.ShaderResourceView, new RootDescriptor1(1, 0), ShaderVisibility.All),
            new RootParameter1(new RootDescriptorTable1(new DescriptorRange1(DescriptorRangeType.Sampler, 1, 0)), ShaderVisibility.All));

        Assert.True(RootSignatureParser.TryParse(blob, out var layout));
        Assert.Equal(4, layout.Count);
        Assert.Equal(new byte[] { 0, 1, 4, 6 }, new[] { layout.Offsets[0], layout.Offsets[1], layout.Offsets[2], layout.Offsets[3] });
        Assert.Equal(new byte[] { 1, 3, 2, 1 }, new[] { layout.Sizes[0], layout.Sizes[1], layout.Sizes[2], layout.Sizes[3] });
    }

    [DxcFact]
    public void A_descriptor_table_set_through_the_game_slots_reaches_the_program()
    {
        using var test = TestRuntime.Create();
        var device = new XboxDevice(test);
        nint desktop = test.Runtime.D3D12X.Objects.DesktopDevice;
        var (heap, cpu, gpu, increment) = ViewHeap(device);
        nint rootSignature = GameRootSignature(device);

        nint source = Buffer(desktop, new HeapProperties { Type = HeapProperties.Upload }, 0, GenericRead);
        nint destination = Buffer(desktop, new HeapProperties { Type = HeapProperties.Custom, CpuPageProperty = WriteBack, MemoryPool = HeapProperties.SystemPool }, DesktopCalls.UnorderedAccessFlag, 0);
        *(uint*)DesktopCalls.Map(source) = Marker;

        var read = BufferViewDescription.RawShaderResource(1);
        var write = BufferViewDescription.RawUnorderedAccess(1);
        ((delegate* unmanaged<nint, nint, BufferViewDescription*, nint, void>)XboxDevice.Vtable(device.Device)[CreateShaderResourceViewSlot])(device.Device, source, &read, cpu);
        ((delegate* unmanaged<nint, nint, nint, BufferViewDescription*, nint, void>)XboxDevice.Vtable(device.Device)[CreateUnorderedAccessViewSlot])(device.Device, destination, 0, &write, cpu + (nint)increment);

        nint pipeline = Pipeline(desktop, test.Runtime.D3D12X.Bindings.Root);
        nint list = device.DirectList();
        nint* heaps = stackalloc nint[] { heap };
        ((delegate* unmanaged<nint, uint, nint*, void>)XboxDevice.Vtable(list)[ListSetHeapsSlot])(list, 1, heaps);

        ((delegate* unmanaged<nint, nint, void>)XboxDevice.Vtable(list)[ListSetPipelineSlot])(list, pipeline);
        ((delegate* unmanaged<nint, nint, void>)XboxDevice.Vtable(list)[ListSetComputeRootSlot])(list, rootSignature);
        ((delegate* unmanaged<nint, uint, ulong, void>)XboxDevice.Vtable(list)[ListSetComputeTableSlot])(list, 0, gpu);
        ((delegate* unmanaged<nint, uint, uint, uint, void>)XboxDevice.Vtable(list)[ListDispatchSlot])(list, 1, 1, 1);

        Run(device, list);

        Assert.Equal(Marker + 1, *(uint*)DesktopCalls.Map(destination));
    }

    private static (nint Heap, nint Cpu, ulong Gpu, uint Increment) ViewHeap(XboxDevice device)
    {
        int* description = stackalloc int[] { 0, 16, 1, 0 };
        Guid iid = new("8efb471d-616c-4f49-90f7-127bb763fa51");
        nint heap = 0;
        Assert.Equal(0, ((delegate* unmanaged<nint, int*, Guid*, nint*, int>)XboxDevice.Vtable(device.Device)[CreateDescriptorHeapSlot])(device.Device, description, &iid, &heap));

        uint increment = ((delegate* unmanaged<nint, int, uint>)XboxDevice.Vtable(device.Device)[DescriptorIncrementSlot])(device.Device, 0);
        nint cpu = ((delegate* unmanaged<nint, nint>)XboxDevice.Vtable(heap)[HeapCpuStartSlot])(heap);
        ulong gpu = (ulong)((delegate* unmanaged<nint, nint>)XboxDevice.Vtable(heap)[HeapGpuStartSlot])(heap);

        return (heap, cpu, gpu, increment);
    }

    /// <summary>The game's kind of root signature: one table of an SRV and a UAV, created through device slot 16.</summary>
    private static nint GameRootSignature(XboxDevice device)
    {
        var blob = Serialize(new RootParameter1(
            new RootDescriptorTable1(new DescriptorRange1(DescriptorRangeType.ShaderResourceView, 1, 0), new DescriptorRange1(DescriptorRangeType.UnorderedAccessView, 1, 0)),
            ShaderVisibility.All));
        Guid iid = new("c54a6b66-72df-4ee8-8be5-a946a1429214");
        nint root = 0;

        fixed (byte* data = blob)
        {
            var create = (delegate* unmanaged<nint, uint, byte*, nuint, Guid*, nint*, int>)XboxDevice.Vtable(device.Device)[CreateRootSignatureSlot];
            Assert.Equal(0, create(device.Device, 0, data, (nuint)blob.Length, &iid, &root));
        }

        return root;
    }

    private static byte[] Serialize(params RootParameter1[] parameters)
    {
        var description = new VersionedRootSignatureDescription(new RootSignatureDescription1(RootSignatureFlags.None, parameters));
        string? error = D3D12.D3D12SerializeVersionedRootSignature(description, out var blob);
        Assert.True(string.IsNullOrEmpty(error), error);

        using (blob)
        {
            return blob!.AsBytes();
        }
    }

    private static nint Buffer(nint device, HeapProperties properties, int flags, int state)
    {
        nint buffer = DesktopCalls.CreateBuffer(device, properties, 256, flags, state);
        Assert.NotEqual(0, buffer);

        return buffer;
    }

    /// <summary>The program compiled with dxc, as a compute pipeline on the bindless root signature.</summary>
    private static nint Pipeline(nint device, nint root)
    {
        var folder = Directory.CreateTempSubdirectory("fivestar-binding-");

        try
        {
            string source = Path.Combine(folder.FullName, "program.hlsl");
            string output = Path.Combine(folder.FullName, "program.dxil");
            File.WriteAllText(source, Program);

            using var dxc = Process.Start(new ProcessStartInfo(PipelineFactAttribute.Dxc, ["-T", "cs_6_6", "-E", "main", "-Fo", output, source]) { RedirectStandardError = true })!;
            string errors = dxc.StandardError.ReadToEnd();
            dxc.WaitForExit();
            Assert.True(dxc.ExitCode == 0, errors);

            var desktop = new ID3D12Device(device);
            desktop.AddRef();

            using (desktop)
            {
                var signature = new ID3D12RootSignature(root);
                signature.AddRef();
                using var state = desktop.CreateComputePipelineState(new ComputePipelineStateDescription { RootSignature = signature, ComputeShader = File.ReadAllBytes(output) });
                state.AddRef();

                return state.NativePointer;
            }
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }

    /// <summary>Close (list 9), ExecuteCommandListsX (queue 35), Signal (queue 16) and a blocking SetEventOnCompletion (fence 9).</summary>
    private static void Run(XboxDevice device, nint list)
    {
        nint queue = device.Queue();
        nint fence = device.Fence();
        Assert.Equal(0, ((delegate* unmanaged<nint, int>)XboxDevice.Vtable(list)[ListCloseSlot])(list));

        nint* lists = stackalloc nint[] { list };
        Assert.Equal(0, ((delegate* unmanaged<nint, uint, nint*, nint, uint, int>)XboxDevice.Vtable(queue)[QueueExecuteSlot])(queue, 1, lists, 0, 0));
        Assert.Equal(0, ((delegate* unmanaged<nint, nint, ulong, int>)XboxDevice.Vtable(queue)[QueueSignalSlot])(queue, fence, 1));
        Assert.Equal(0, ((delegate* unmanaged<nint, ulong, nint, int>)XboxDevice.Vtable(fence)[FenceWaitSlot])(fence, 1, 0));
    }
}
