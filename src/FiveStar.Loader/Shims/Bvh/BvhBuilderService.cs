// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using FiveStar.Loader.Diagnostics;
using FiveStar.Loader.Native;
using FiveStar.Loader.Shims.D3D12X;
using FiveStar.Loader.Shims.D3D12X.Pipelines;

namespace FiveStar.Loader.Shims.Bvh;

/// <summary>
/// What stands behind <see cref="BvhBuilderShim"/>: no acceleration structure is built yet (translating the console's
/// batched builds to desktop DXR is its own design), and sizes come from the desktop device's prebuild query, so the
/// game allocates buffers the desktop would accept. Each unbuilt call is logged once.
/// </summary>
internal sealed unsafe class BvhBuilderService
{
    /// <summary>ID3D12Device5::GetRaytracingAccelerationStructurePrebuildInfo (d3d12.h).</summary>
    private const int PrebuildInfoSlot = 63;

    /// <summary>D3D12_RAYTRACING_ACCELERATION_STRUCTURE_PREBUILD_INFO: result, scratch and update-scratch sizes.</summary>
    private const int PrebuildInfoFields = 3;

    private static readonly Guid Device5Iid = new("8b4f173b-2fea-4b80-8f58-4307191ab95d");

    private readonly XboxObjects _objects;
    private readonly OnceLog _once;
    private nint _device5;

    public BvhBuilderService(XboxObjects objects, LoaderLog log)
    {
        _objects = objects;
        _once = new OnceLog(log);
    }

    public void Note(string call)
    {
        _once.Warn(call, $"[bvh] XboxBvhBuilder::{call}: stubbed, no desktop acceleration structures yet");
    }

    /// <summary>The desktop prebuild sizes for <paramref name="inputs"/>, or zeros without an ID3D12Device5.</summary>
    public void PrebuildInfo(void* inputs, ulong* info)
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
