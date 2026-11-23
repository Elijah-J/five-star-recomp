// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using FiveStar.Loader.Diagnostics;
using FiveStar.Loader.Loading;
using FiveStar.Loader.Native;
using FiveStar.Loader.Shims.D3D12X;
using FiveStar.Loader.Shims.D3D12X.Pipelines;
using FiveStar.Loader.Shims.D3D12X.Resources;

namespace FiveStar.Loader.Shims.Bvh;

/// <summary>
/// What stands behind <see cref="BvhBuilderShim"/>. By default nothing is built and sizes come from the desktop
/// device's prebuild query. With ray tracing on (<c>--raytracing</c>) the shipped DLL's CPU entry points take over:
/// console sizes, so the game lays out AMD-format structures as on the console, and CPU rehydration of the serialized
/// BLASes the game streams in (see docs/raytracing.md). Builds and copies stay skipped; each is logged once.
/// </summary>
internal sealed unsafe class BvhBuilderService
{
    /// <summary>ID3D12Device5::GetRaytracingAccelerationStructurePrebuildInfo (d3d12.h).</summary>
    private const int PrebuildInfoSlot = 63;

    /// <summary>D3D12_RAYTRACING_ACCELERATION_STRUCTURE_PREBUILD_INFO: result, scratch and update-scratch sizes.</summary>
    private const int PrebuildInfoFields = 3;

    private static readonly Guid Device5Iid = new("8b4f173b-2fea-4b80-8f58-4307191ab95d");

    private readonly XboxObjects _objects;
    private readonly ConsoleBvhModule _console;
    private readonly BvhRehydrator _rehydrator;
    private readonly OnceLog _once;
    private nint _device5;

    public BvhBuilderService(XboxObjects objects, ImageLoader loader, XMemPhysicalMemory memory, LoaderLog log)
    {
        _objects = objects;
        _once = new OnceLog(log);
        _console = new ConsoleBvhModule(loader, log);
        _rehydrator = new BvhRehydrator(_console, new GameMemorySource(memory), _once);
    }

    /// <summary>Whether the console DLL's CPU paths are used (<c>--raytracing</c>); off by default.</summary>
    public bool Enabled { get; set; }

    public void Note(string call)
    {
        _once.Warn(call, $"[bvh] XboxBvhBuilder::{call}: stubbed, no acceleration structure written");
    }

    /// <summary>Console sizes with ray tracing on; else the desktop prebuild sizes, or zeros without an ID3D12Device5.</summary>
    public void PrebuildInfo(void* inputs, void* stream, ulong* info)
    {
        if (Enabled && inputs != null && _console.TryBind())
        {
            _console.PrebuildInfo(inputs, stream, info);

            return;
        }

        DesktopPrebuildInfo(inputs, info);
    }

    /// <summary>Rehydrates serialized BLASes on the CPU with ray tracing on; else skips them.</summary>
    public int Rehydrate(uint count, void* descs, ulong* sources)
    {
        if (!Enabled || !_console.TryBind())
        {
            Note("RehydrateRaytracingAccelerationStructureBatched2X (no rehydration)");

            return HResult.Ok;
        }

        return _rehydrator.Rehydrate(count, (byte*)descs, sources);
    }

    private void DesktopPrebuildInfo(void* inputs, ulong* info)
    {
        for (int i = 0; i < PrebuildInfoFields; i++)
        {
            info[i] = 0;
        }

        nint device = Device5();

        if (device == 0 || inputs == null)
        {
            Note("GetRaytracingAccelerationStructurePrebuildInfo (no ID3D12Device5: zero sizes)");

            return;
        }

        ((delegate* unmanaged<nint, void*, ulong*, void>)ComObject.Slot(device, PrebuildInfoSlot))(device, inputs, info);
    }

    private nint Device5()
    {
        if (_device5 != 0)
        {
            return _device5;
        }

        nint desktop = _objects.DesktopDevice;

        if (desktop == 0)
        {
            return 0;
        }

        Guid iid = Device5Iid;
        nint device5 = 0;

        if (HResult.Failed(((delegate* unmanaged<nint, Guid*, nint*, int>)ComObject.Slot(desktop, ComObject.QueryInterfaceSlot))(desktop, &iid, &device5)))
        {
            return 0;
        }

        // The device lives for the process; the reference is kept.
        _device5 = device5;

        return device5;
    }
}
