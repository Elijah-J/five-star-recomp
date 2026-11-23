// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text;
using FiveStar.Loader.Codegen;
using FiveStar.Loader.Diagnostics;
using FiveStar.Loader.Loading;
using FiveStar.Loader.Native;
using FiveStar.Loader.Shims.D3D12X.DebugLayer;
using FiveStar.Loader.Shims.D3D12X.Pipelines;
using FiveStar.Shaders.Cache;

namespace FiveStar.Loader.Shims.D3D12X;

/// <summary>
/// D3D12XboxCreateDevice on a desktop D3D12 device. The game gets wrappers (<see cref="XboxObjects"/>) for the device
/// and every object it creates; this class binds the slots that need managed code: creation (results wrapped), the
/// X-only device and queue slots with a desktop translation, and the getters whose D3D12.X form differs.
/// </summary>
internal sealed unsafe class XboxDeviceFactory : IDisposable
{
    /// <summary>Device slot 80, read as WaitFrameEventX (*inferred*: type, timeout, null, flags, UINT64* token).</summary>
    private const int WaitFrameEventSlot = 80;

    /// <summary>Device slot 133, read as CreatePlacedResourceX (*inferred*).</summary>
    private const int CreatePlacedResourceSlot = 133;

    /// <summary>Resource slot 11 (GetGPUVirtualAddress) and descriptor-heap slots 9 and 10 (heap start handles).</summary>
    private const int ResourceGpuAddressSlot = 11;
    private const int HeapCpuStartSlot = 9;
    private const int HeapGpuStartSlot = 10;

    private readonly D3D12Forwarder _d3d12;
    private readonly UnresolvedStubs _stubs;
    private readonly PipelineFolders _pipelineFolders;
    private readonly LoaderLog _log;

    public XboxDeviceFactory(D3D12Forwarder d3d12, UnresolvedStubs stubs, CodeHeap heap, XMemPhysicalMemory memory, ImageTable images, PipelineFolders pipelines, LoaderLog log)
    {
        _d3d12 = d3d12;
        _stubs = stubs;
        _pipelineFolders = pipelines;
        _log = log;

        Debugging = new D3D12Debugging(d3d12, images, log);
        Objects = new XboxObjects(stubs, heap, Debugging, log);
        CommandLists = new XboxCommandLists(Objects);
        PlacedResources = new XboxPlacedResources(memory, Objects, log);
        DescriptorHeaps = new XboxDescriptorHeaps(Objects, new TitleMemoryHooks(images), log);

        BindDeviceSlots();
        BindObjectSlots();
    }

    /// <summary>The wrappers and their vtables; <see cref="XboxObjects.SetHandler"/> is where other shims bind slots.</summary>
    public XboxObjects Objects { get; }

    /// <summary>The opt-in debug layer, info-queue log and DRED report (<c>--d3d12-debug</c>).</summary>
    public D3D12Debugging Debugging { get; }

    /// <summary>The command-list wrappers device slots 12 and 73 hand out.</summary>
    public XboxCommandLists CommandLists { get; }

    /// <summary>The pipeline slots (55, 57, 62, 67, 86) once a device exists and the shader folders are known.</summary>
    public PipelineSlots? Pipelines { get; private set; }

    /// <summary>Device slot 14 and the title-memory descriptor handles.</summary>
    public XboxDescriptorHeaps DescriptorHeaps { get; }

    /// <summary>Device slot 133 and the console-to-desktop GPU address table.</summary>
    public XboxPlacedResources PlacedResources { get; }

    /// <summary>HRESULT D3D12XboxCreateDevice(IGraphicsUnknown* adapter, const PARAMETERS*, REFIID, void** device).</summary>
    public int CreateDevice(nint adapter, byte* parameters, Guid* iid, nint* device)
    {
        if (device == null)
        {
            return HResult.Pointer;
        }

        *device = 0;
        LogParameters(adapter, parameters, iid);
        Guid desktopIid = D3D12XLayout.DeviceIid;
        nint inner;
        Debugging.BeforeDevice();
        int hr = _d3d12.CreateDevice(D3D12XLayout.MinimumFeatureLevel, &desktopIid, &inner);

        if (HResult.Failed(hr))
        {
            _log.Error($"  [d3d12_xs] D3D12CreateDevice failed: {HResult.Format(hr)}");

            return hr;
        }

        Debugging.AttachDevice(inner);
        *device = Objects.WrapDevice(inner);
        CreatePipelines(inner);

        return HResult.Ok;
    }

    /// <summary>
    /// HRESULT D3D12ConfigureVirtualMemoryX(const CONFIGURATION*). The desktop device manages its own GPU virtual
    /// addresses, so the call only logs what the game asked for.
    /// </summary>
    public int ConfigureVirtualMemory(byte* configuration)
    {
        uint count = 0;

        if (configuration != null)
        {
            count = *(uint*)(configuration + D3D12XLayout.VirtualMemoryCountOffset);
        }

        _log.Detail($"  [d3d12_xs] D3D12ConfigureVirtualMemoryX(dword@4={count}) -> S_OK (no-op)");

        return HResult.Ok;
    }

    /// <summary>
    /// The pipeline slots on the new device: ID3D12Device2 for pipeline streams, the bindless root signature every
    /// translated program uses, and the translated programs. Without the shader folders the slots stay stubs.
    /// </summary>
    private void CreatePipelines(nint inner)
    {
        string? programs = _pipelineFolders.Programs;
        string? translated = _pipelineFolders.Translated;

        if (programs is null || translated is null)
        {
            _log.Detail("  [d3d12_xs] pipeline slots stay stubs: no --shader-dir and --translated-dir");

            return;
        }

        Guid device2Iid = DesktopDevice.Device2Iid;
        nint device2 = 0;
        nint root = 0;

        if (HResult.Failed(DesktopDevice.QueryInterface(inner, &device2Iid, &device2)) || HResult.Failed(BindlessRoot.Create(inner, &root)))
        {
            _log.Warn("[d3d12_xs] pipeline slots stay stubs: no ID3D12Device2 or bindless root signature");

            return;
        }

        var warnings = new OnceLog(_log);
        var library = new TranslatedProgramLibrary(ProgramIndex.Load(programs), translated);
        Pipelines = new PipelineSlots(new PipelineFactory(device2, root, library, warnings), warnings);
        PipelineSlotHandlers.Register(Objects);
        _log.Detail($"  [d3d12_xs] pipeline slots on: programs {programs}, translated {translated}");
    }

    public void Dispose()
    {
        Debugging.Dispose();
    }

    private void BindDeviceSlots()
    {
        delegate* unmanaged<XboxObject*, void*, Guid*, nint*, int> queue = &XboxDeviceCreation.CreateCommandQueue;
        delegate* unmanaged<XboxObject*, int, Guid*, nint*, int> allocator = &XboxDeviceCreation.CreateCommandAllocator;
        delegate* unmanaged<XboxObject*, uint, int, nint, nint, Guid*, nint*, int> list = &XboxDeviceCreation.CreateCommandList;
        delegate* unmanaged<XboxObject*, DescriptorHeapDescription*, Guid*, nint*, int> descriptorHeap = &XboxDeviceCreation.CreateDescriptorHeap;

        delegate* unmanaged<XboxObject*, void*, int, ResourceDescription*, int, void*, Guid*, nint*, int> committed = &XboxDeviceCreation.CreateCommittedResource;
        delegate* unmanaged<XboxObject*, void*, Guid*, nint*, int> heap = &XboxDeviceCreation.CreateHeap;
        delegate* unmanaged<XboxObject*, nint, ulong, ResourceDescription*, int, void*, Guid*, nint*, int> placed = &XboxDeviceCreation.CreatePlacedResource;
        delegate* unmanaged<XboxObject*, ResourceDescription*, int, void*, Guid*, nint*, int> reserved = &XboxDeviceCreation.CreateReservedResource;

        delegate* unmanaged<XboxObject*, uint, nint*, int> residency = &XboxDeviceCreation.Residency;
        delegate* unmanaged<XboxObject*, ulong, int, Guid*, nint*, int> fence = &XboxDeviceCreation.CreateFence;
        delegate* unmanaged<XboxObject*, void*, Guid*, nint*, int> queryHeap = &XboxDeviceCreation.CreateQueryHeap;
        delegate* unmanaged<XboxObject*, void*, nint, Guid*, nint*, int> signature = &XboxDeviceCreation.CreateCommandSignature;

        BindDevice(XboxDeviceCreation.CreateCommandQueueSlot, (nint)queue);
        BindDevice(XboxDeviceCreation.CreateCommandAllocatorSlot, (nint)allocator);
        BindDevice(XboxDeviceCreation.CreateCommandListSlot, (nint)list);
        BindDevice(XboxDeviceCreation.CreateDescriptorHeapSlot, (nint)descriptorHeap);
        BindDevice(XboxDeviceCreation.CreateCommittedResourceSlot, (nint)committed);
        BindDevice(XboxDeviceCreation.CreateHeapSlot, (nint)heap);

        BindDevice(XboxDeviceCreation.CreatePlacedResourceSlot, (nint)placed);
        BindDevice(XboxDeviceCreation.CreateReservedResourceSlot, (nint)reserved);
        BindDevice(XboxDeviceCreation.MakeResidentSlot, (nint)residency);
        BindDevice(XboxDeviceCreation.EvictSlot, (nint)residency);
        BindDevice(XboxDeviceCreation.CreateFenceSlot, (nint)fence);
        BindDevice(XboxDeviceCreation.CreateQueryHeapSlot, (nint)queryHeap);

        BindDevice(XboxDeviceCreation.CreateCommandSignatureSlot, (nint)signature);
        BindResourceQueries();
        BindDescriptorSlots();
        BindXboxDeviceSlots();
    }

    /// <summary>The queries that take resource descs, which may carry console flags and layouts.</summary>
    private void BindResourceQueries()
    {
        delegate* unmanaged<ResourceAllocationInfo*, XboxObject*, uint, uint, ResourceDescription*, ResourceAllocationInfo*> allocationInfo = &XboxDeviceCreation.GetResourceAllocationInfo;
        delegate* unmanaged<XboxObject*, ResourceDescription*, uint, uint, ulong, void*, uint*, ulong*, ulong*, void> footprints = &XboxDeviceCreation.GetCopyableFootprints;
        BindDevice(XboxDeviceCreation.GetResourceAllocationInfoSlot, (nint)allocationInfo);
        BindDevice(XboxDeviceCreation.GetCopyableFootprintsSlot, (nint)footprints);
    }

    /// <summary>View creation and descriptor copies, whose CPU handles may be title memory (<see cref="XboxDescriptorMethods"/>).</summary>
    private void BindDescriptorSlots()
    {
        delegate* unmanaged<XboxObject*, void*, nint, void> constantBuffer = &XboxDescriptorMethods.CreateConstantBufferView;
        delegate* unmanaged<XboxObject*, nint, void*, nint, void> shaderResource = &XboxDescriptorMethods.CreateShaderResourceView;
        delegate* unmanaged<XboxObject*, nint, nint, void*, nint, void> unorderedAccess = &XboxDescriptorMethods.CreateUnorderedAccessView;
        delegate* unmanaged<XboxObject*, nint, void*, nint, void> renderTarget = &XboxDescriptorMethods.CreateRenderTargetView;
        delegate* unmanaged<XboxObject*, nint, void*, nint, void> depthStencil = &XboxDescriptorMethods.CreateDepthStencilView;
        delegate* unmanaged<XboxObject*, void*, nint, void> sampler = &XboxDescriptorMethods.CreateSampler;

        delegate* unmanaged<XboxObject*, uint, nint*, uint*, uint, nint*, uint*, int, void> copy = &XboxDescriptorMethods.CopyDescriptors;
        delegate* unmanaged<XboxObject*, uint, nint, nint, int, void> copySimple = &XboxDescriptorMethods.CopyDescriptorsSimple;
        BindDevice(XboxDescriptorMethods.CreateConstantBufferViewSlot, (nint)constantBuffer);
        BindDevice(XboxDescriptorMethods.CreateShaderResourceViewSlot, (nint)shaderResource);
        BindDevice(XboxDescriptorMethods.CreateUnorderedAccessViewSlot, (nint)unorderedAccess);
        BindDevice(XboxDescriptorMethods.CreateRenderTargetViewSlot, (nint)renderTarget);

        BindDevice(XboxDescriptorMethods.CreateDepthStencilViewSlot, (nint)depthStencil);
        BindDevice(XboxDescriptorMethods.CreateSamplerSlot, (nint)sampler);
        BindDevice(XboxDescriptorMethods.CopyDescriptorsSlot, (nint)copy);
        BindDevice(XboxDescriptorMethods.CopyDescriptorsSimpleSlot, (nint)copySimple);
    }

    /// <summary>The X-only device slots with a desktop translation (<see cref="XboxDeviceMethods"/>).</summary>
    private void BindXboxDeviceSlots()
    {
        delegate* unmanaged<XboxObject*, CommandQueueDescription*, Guid*, nint*, int> createQueue = &XboxDeviceMethods.CreateCommandQueue;
        delegate* unmanaged<XboxObject*, CommandListDescription*, nint, nint, Guid*, nint*, int> createList = &XboxDeviceMethods.CreateCommandList;
        delegate* unmanaged<XboxObject*, nint, uint, uint, uint, int> frameInterval = &XboxDeviceMethods.SetFrameInterval;
        delegate* unmanaged<XboxObject*, uint, uint, nint, uint, int> frameEvent = &XboxDeviceMethods.ScheduleFrameEvent;
        delegate* unmanaged<XboxObject*, ulong, ResourceDescription*, uint, void*, Guid*, nint*, int> placed = &XboxDeviceMethods.CreatePlacedResource;

        BindDevice(D3D12XLayout.CreateCommandQueueSlot, (nint)createQueue);
        BindDevice(D3D12XLayout.CreateCommandListSlot, (nint)createList);
        BindDevice(D3D12XLayout.SetFrameIntervalSlot, (nint)frameInterval);
        BindDevice(D3D12XLayout.ScheduleFrameEventSlot, (nint)frameEvent);
        BindDevice(CreatePlacedResourceSlot, (nint)placed);

        // The game breaks into the debugger when slot 80 fails (0x142482450); there is no frame event to wait for.
        BindDevice(WaitFrameEventSlot, _stubs.Create(D3D12XLayout.Module, $"ID3D12Device.X::Slot{WaitFrameEventSlot} (S_OK)", HResult.Ok, true, true));
    }

    private void BindObjectSlots()
    {
        delegate* unmanaged<XboxObject*, uint, nint*, nint, uint, int> executeX = &XboxQueueMethods.ExecuteCommandListsX;
        delegate* unmanaged<XboxObject*, uint, nint*, void> execute = &XboxQueueMethods.ExecuteCommandLists;
        delegate* unmanaged<XboxResourceObject*, ulong> gpuAddress = &XboxObjectMethods.GetGpuVirtualAddress;
        delegate* unmanaged<XboxDescriptorHeapObject*, nint> cpuStart = &XboxObjectMethods.GetCpuHandleForHeapStart;
        delegate* unmanaged<XboxObject*, nint> gpuStart = &XboxObjectMethods.GetGpuHandleForHeapStart;

        Objects.SetHandler(XboxInterfaceKind.CommandQueue, XboxQueueMethods.ExecuteCommandListsXSlot, (nint)executeX);
        Objects.SetHandler(XboxInterfaceKind.CommandQueue, XboxQueueMethods.ExecuteCommandListsSlot, (nint)execute);
        Objects.SetHandler(XboxInterfaceKind.Resource, ResourceGpuAddressSlot, (nint)gpuAddress);
        Objects.SetHandler(XboxInterfaceKind.DescriptorHeap, HeapCpuStartSlot, (nint)cpuStart);
        Objects.SetHandler(XboxInterfaceKind.DescriptorHeap, HeapGpuStartSlot, (nint)gpuStart);

        // Present has no swap chain behind it yet: logged once, S_OK so the frame loop goes on.
        string present = $"ID3D12CommandQueue.X::Slot{XboxQueueMethods.PresentSlot} (S_OK)";
        Objects.SetHandler(XboxInterfaceKind.CommandQueue, XboxQueueMethods.PresentSlot, _stubs.Create(D3D12XLayout.Module, present, HResult.Ok, true, true));
    }

    private void BindDevice(int slot, nint handler)
    {
        Objects.SetHandler(XboxInterfaceKind.Device, slot, handler);
    }

    private void LogParameters(nint adapter, byte* parameters, Guid* iid)
    {
        if (!_log.DetailEnabled)
        {
            return;
        }

        var text = new StringBuilder($"  [d3d12_xs] D3D12XboxCreateDevice(adapter=0x{adapter:x}");

        if (iid != null)
        {
            text.Append($", iid={*iid}");
        }

        if (parameters != null)
        {
            text.Append(", parameters=");

            for (int offset = 0; offset < D3D12XLayout.ParametersSize; offset += sizeof(uint))
            {
                text.Append($" {*(uint*)(parameters + offset):x8}");
            }
        }

        _log.Detail(text.Append(')').ToString());
    }
}
