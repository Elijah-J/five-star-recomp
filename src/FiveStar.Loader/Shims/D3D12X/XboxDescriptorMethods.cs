// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Runtime.InteropServices;
using FiveStar.Loader.Native;
using FiveStar.Loader.Shims.D3D12X.Resources;

namespace FiveStar.Loader.Shims.D3D12X;

/// <summary>
/// Device slots 17 to 24 (view creation and descriptor copies, desktop order): CPU handles in title memory become
/// desktop handles (<see cref="XboxDescriptorHeaps.Translate"/>) and wrapped resources desktop resources. Views are
/// written while streaming, so these stay allocation-free.
/// </summary>
internal static unsafe class XboxDescriptorMethods
{
    public const int CreateConstantBufferViewSlot = 17;
    public const int CreateShaderResourceViewSlot = 18;
    public const int CreateUnorderedAccessViewSlot = 19;
    public const int CreateRenderTargetViewSlot = 20;
    public const int CreateDepthStencilViewSlot = 21;
    public const int CreateSamplerSlot = 22;
    public const int CopyDescriptorsSlot = 23;
    public const int CopyDescriptorsSimpleSlot = 24;

    /// <summary>Descriptor ranges translated per desktop CopyDescriptors call.</summary>
    private const int Batch = 64;

    private static XboxDeviceFactory D3D12X => ShimContext.Runtime.D3D12X;

    [HotPath]
    [UnmanagedCallersOnly]
    public static void CreateConstantBufferView(XboxObject* self, ConstantBufferViewDescription* description, nint handle)
    {
        nint inner = self->Inner;
        ConstantBufferViewDescription desktop = default;
        ConstantBufferViewDescription* view = null;

        // The console address becomes a desktop one and the size grows to the desktop's 256-byte granularity.
        if (description != null)
        {
            desktop = *description;
            desktop.Size = (desktop.Size + ConstantBufferViewDescription.Alignment - 1) & ~(ConstantBufferViewDescription.Alignment - 1);
            view = &desktop;

            if (desktop.Location != 0 && !D3D12X.GameMemory.TryTranslate(desktop.Location, out desktop.Location))
            {
                D3D12X.Warnings.Warn("cbv", "[d3d12_xs] CreateConstantBufferView: a GPU address outside known game memory, null view written");
                view = null;
            }
            else if ((desktop.Location & (ConstantBufferViewDescription.Alignment - 1)) != 0)
            {
                // The console takes constant buffers at 16-byte boundaries; the desktop removes the device for these.
                D3D12X.Warnings.Warn("cbv-alignment", "[d3d12_xs] CreateConstantBufferView: an address off the desktop's 256-byte alignment, null view written");
                view = null;
            }
        }

        ((delegate* unmanaged<nint, ConstantBufferViewDescription*, nint, void>)ComObject.Slot(inner, CreateConstantBufferViewSlot))(inner, view, Handle(handle));
    }

    [HotPath]
    [UnmanagedCallersOnly]
    public static void CreateShaderResourceView(XboxObject* self, nint resource, void* description, nint handle)
    {
        nint desktop = D3D12X.Objects.Unwrap(resource);
        byte* copy = stackalloc byte[DepthViewDescriptions.ShaderResourceViewSize];
        CreateDesktopView(self, CreateShaderResourceViewSlot, desktop, DepthViewDescriptions.ShaderResourceView(desktop, description, copy), handle);
    }

    [HotPath]
    [UnmanagedCallersOnly]
    public static void CreateRenderTargetView(XboxObject* self, nint resource, void* description, nint handle)
    {
        CreateView(self, CreateRenderTargetViewSlot, resource, description, handle);
    }

    [HotPath]
    [UnmanagedCallersOnly]
    public static void CreateDepthStencilView(XboxObject* self, nint resource, void* description, nint handle)
    {
        nint desktop = D3D12X.Objects.Unwrap(resource);
        byte* copy = stackalloc byte[DepthViewDescriptions.DepthStencilViewSize];
        CreateDesktopView(self, CreateDepthStencilViewSlot, desktop, DepthViewDescriptions.DepthStencilView(desktop, description, copy), handle);
    }

    [HotPath]
    [UnmanagedCallersOnly]
    public static void CreateUnorderedAccessView(XboxObject* self, nint resource, nint counter, void* description, nint handle)
    {
        nint inner = self->Inner;
        var objects = D3D12X.Objects;
        var create = (delegate* unmanaged<nint, nint, nint, void*, nint, void>)ComObject.Slot(inner, CreateUnorderedAccessViewSlot);
        create(inner, objects.Unwrap(resource), objects.Unwrap(counter), description, Handle(handle));
    }

    [HotPath]
    [UnmanagedCallersOnly]
    public static void CreateSampler(XboxObject* self, void* description, nint handle)
    {
        nint inner = self->Inner;
        ((delegate* unmanaged<nint, void*, nint, void>)ComObject.Slot(inner, CreateSamplerSlot))(inner, description, Handle(handle));
    }

    [HotPath]
    [UnmanagedCallersOnly]
    public static void CopyDescriptorsSimple(XboxObject* self, uint count, nint destination, nint source, int type)
    {
        nint inner = self->Inner;
        var copy = (delegate* unmanaged<nint, uint, nint, nint, int, void>)ComObject.Slot(inner, CopyDescriptorsSimpleSlot);
        copy(inner, count, Handle(destination), Handle(source), type);
    }

    /// <summary>
    /// CopyDescriptors(dest range count, dest starts, dest sizes, source range count, source starts, source sizes,
    /// type). Ranges are translated in batches; a batch boundary splits nothing because each range keeps its size.
    /// </summary>
    [HotPath]
    [UnmanagedCallersOnly]
    public static void CopyDescriptors(XboxObject* self, uint destinationCount, nint* destinations, uint* destinationSizes, uint sourceCount, nint* sources, uint* sourceSizes, int type)
    {
        nint inner = self->Inner;
        var copy = (delegate* unmanaged<nint, uint, nint*, uint*, uint, nint*, uint*, int, void>)ComObject.Slot(inner, CopyDescriptorsSlot);

        if (destinationCount > Batch || sourceCount > Batch)
        {
            // Rare: translate in place, the game does not reuse the arrays after the call.
            TranslateAll(destinations, destinationCount);
            TranslateAll(sources, sourceCount);
            copy(inner, destinationCount, destinations, destinationSizes, sourceCount, sources, sourceSizes, type);

            return;
        }

        nint* desktopDestinations = stackalloc nint[Batch];
        nint* desktopSources = stackalloc nint[Batch];
        Copy(destinations, desktopDestinations, destinationCount);
        Copy(sources, desktopSources, sourceCount);
        copy(inner, destinationCount, desktopDestinations, destinationSizes, sourceCount, desktopSources, sourceSizes, type);
    }

    [HotPath]
    private static void CreateView(XboxObject* self, int slot, nint resource, void* description, nint handle)
    {
        CreateDesktopView(self, slot, D3D12X.Objects.Unwrap(resource), description, handle);
    }

    [HotPath]
    private static void CreateDesktopView(XboxObject* self, int slot, nint resource, void* description, nint handle)
    {
        nint inner = self->Inner;
        var create = (delegate* unmanaged<nint, nint, void*, nint, void>)ComObject.Slot(inner, slot);
        create(inner, resource, description, Handle(handle));
    }

    [HotPath]
    private static nint Handle(nint handle)
    {
        return D3D12X.DescriptorHeaps.Translate(handle);
    }

    [HotPath]
    private static void Copy(nint* handles, nint* desktop, uint count)
    {
        for (uint i = 0; i < count; i++)
        {
            desktop[i] = Handle(handles[i]);
        }
    }

    [HotPath]
    private static void TranslateAll(nint* handles, uint count)
    {
        for (uint i = 0; i < count; i++)
        {
            handles[i] = Handle(handles[i]);
        }
    }
}
