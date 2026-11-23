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

    /// <summary>The game's main view heap (0x141cf3280), a heap that fits the desktop, and a page far from both ends.</summary>
    private const uint GameViews = 1245184;
    private const uint SmallHeap = 16;
    private const uint FarPage = 500000;
    private const uint TableSize = 2;

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

    [Fact]
    public void The_remap_gives_touched_pages_desktop_pages_in_order_and_keeps_each_contiguous()
    {
        var remap = new DescriptorRemap(DescriptorWindow.ReservedViews, DescriptorWindow.MaxViews, GameViews);
        uint high = GameViews - 1;

        Assert.Equal(DescriptorWindow.ReservedViews + (high % DescriptorRemap.PageSize), remap.Map(high));
        Assert.Equal(remap.Map(high) - 1, remap.Map(high - 1));
        Assert.Equal(DescriptorWindow.ReservedViews + DescriptorRemap.PageSize, remap.Map(0));

        for (uint page = 1; page < remap.Capacity - 1; page++)
        {
            Assert.NotEqual(DescriptorRemap.Unmapped, remap.Map(page * DescriptorRemap.PageSize));
        }

        Assert.Equal(remap.Capacity, remap.MappedPages);
        Assert.Equal(DescriptorRemap.Unmapped, remap.Map(high - DescriptorRemap.PageSize));
    }

    [DxcFact]
    public void A_descriptor_table_set_through_the_game_slots_reaches_the_program()
    {
        using var test = TestRuntime.Create();
        var device = new XboxDevice(test);
        var heap = ViewHeap(device, SmallHeap);

        Assert.Equal(Marker + 1, Dispatch(test, device, heap, 0, 1, heap.Gpu));
    }

    [DxcFact]
    public void A_table_past_the_desktop_limit_reaches_the_program_across_remap_pages()
    {
        using var test = TestRuntime.Create();
        var device = new XboxDevice(test);
        var heap = ViewHeap(device, GameViews);
        uint source = GameViews - DescriptorRemap.PageSize - 1;

        // The table's two descriptors lie on two pages that other writes keep apart on the desktop.
        Assert.Equal(Marker + 1, Dispatch(test, device, heap, source, source + 1, heap.Gpu + ((ulong)source * heap.Increment), FarPage));
    }

    [DxcFact]
    public void A_table_the_game_copies_into_title_memory_itself_reaches_the_program()
    {
        using var test = TestRuntime.Create();
        var device = new XboxDevice(test);
        var heap = ViewHeap(device, GameViews);
        uint copy = GameViews - 1 - FarPage;
        nint table = heap.Cpu + (nint)((ulong)copy * heap.Increment);

        // Views at 0 and 1 through the slots, then the game's own per-draw table: their bytes copied, no slot called.
        Assert.Equal(Marker + 1, Dispatch(test, device, heap, 0, 1, heap.Gpu, copy: table));
    }

    /// <summary>
    /// Creates an SRV of a marker buffer at game index <paramref name="read"/> and a UAV at <paramref name="write"/>
    /// through the slots, binds the table at <paramref name="table"/> and runs the program; returns what it stored.
    /// </summary>
    private static uint Dispatch(TestRuntime test, XboxDevice device, (nint Heap, nint Cpu, ulong Gpu, uint Increment) heap, uint read, uint write, ulong table, uint between = 0, nint copy = 0)
    {
        nint desktop = test.Runtime.D3D12X.Objects.DesktopDevice;
        nint rootSignature = GameRootSignature(device);
        nint source = Buffer(desktop, new HeapProperties { Type = HeapProperties.Upload }, 0, GenericRead);
        nint destination = Buffer(desktop, new HeapProperties { Type = HeapProperties.Custom, CpuPageProperty = WriteBack, MemoryPool = HeapProperties.SystemPool }, DesktopCalls.UnorderedAccessFlag, 0);
        *(uint*)DesktopCalls.Map(source) = Marker;

        var view = BufferViewDescription.RawShaderResource(1);
        var store = BufferViewDescription.RawUnorderedAccess(1);
        var createRead = (delegate* unmanaged<nint, nint, BufferViewDescription*, nint, void>)XboxDevice.Vtable(device.Device)[CreateShaderResourceViewSlot];
        createRead(device.Device, source, &view, heap.Cpu + (nint)((ulong)read * heap.Increment));

        if (between != 0)
        {
            createRead(device.Device, source, &view, heap.Cpu + (nint)((ulong)between * heap.Increment));
        }

        ((delegate* unmanaged<nint, nint, nint, BufferViewDescription*, nint, void>)XboxDevice.Vtable(device.Device)[CreateUnorderedAccessViewSlot])(device.Device, destination, 0, &store, heap.Cpu + (nint)((ulong)write * heap.Increment));

        if (copy != 0)
        {
            System.Buffer.MemoryCopy((void*)(heap.Cpu + (nint)((ulong)read * heap.Increment)), (void*)copy, heap.Increment * TableSize, heap.Increment * TableSize);
            table = (ulong)copy;
        }

        nint list = device.DirectList();
        nint* heaps = stackalloc nint[] { heap.Heap };
        ((delegate* unmanaged<nint, uint, nint*, void>)XboxDevice.Vtable(list)[ListSetHeapsSlot])(list, 1, heaps);

        ((delegate* unmanaged<nint, nint, void>)XboxDevice.Vtable(list)[ListSetPipelineSlot])(list, Pipeline(desktop, test.Runtime.D3D12X.Bindings.Root));
        ((delegate* unmanaged<nint, nint, void>)XboxDevice.Vtable(list)[ListSetComputeRootSlot])(list, rootSignature);
        ((delegate* unmanaged<nint, uint, ulong, void>)XboxDevice.Vtable(list)[ListSetComputeTableSlot])(list, 0, table);
        ((delegate* unmanaged<nint, uint, uint, uint, void>)XboxDevice.Vtable(list)[ListDispatchSlot])(list, 1, 1, 1);
        Run(device, list);

        return *(uint*)DesktopCalls.Map(destination);
    }

    private static (nint Heap, nint Cpu, ulong Gpu, uint Increment) ViewHeap(XboxDevice device, uint count)
    {
        int* description = stackalloc int[] { 0, (int)count, 1, 0 };
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
