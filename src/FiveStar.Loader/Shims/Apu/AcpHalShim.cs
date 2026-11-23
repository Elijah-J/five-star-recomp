// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Runtime.InteropServices;
using FiveStar.Loader.Native;

namespace FiveStar.Loader.Shims.Apu;

/// <summary>
/// acphal.dll, the console's audio co-processor HAL. Not in the public GDK headers: every signature here is inferred
/// from the executable's call sites (argument registers, how the result is tested) and matches the shape of the
/// older XDK APU API. The APU memory functions run on <see cref="ApuHeap"/>; the SHAPE functions on
/// <see cref="AcpHalDevice"/>. They must succeed: when one fails, the game's audio start-up executes an <c>int3</c>.
/// </summary>
[ShimModule(Module, UnresolvedReturn = HResult.NotImplemented)]
internal static unsafe class AcpHalShim
{
    public const string Module = "acphal.dll";

    /// <summary>HRESULT AcpHalCreate(IAcpHal** out): the caller then calls (*out)->vtbl[0](obj, 0x1000, 0x1000).</summary>
    [Shim(Module, "AcpHalCreate")]
    [UnmanagedCallersOnly]
    private static int AcpHalCreate(nint* result)
    {
        return ShimContext.Runtime.AcpHal.Create(result);
    }

    /// <summary>HRESULT AcpHalAllocateShapeContexts(SHAPE_CONTEXTS* contexts): the caller zeroes count * 64 bytes at +0x38 on success.</summary>
    [Shim(Module, "AcpHalAllocateShapeContexts")]
    [UnmanagedCallersOnly]
    private static int AcpHalAllocateShapeContexts(byte* contexts)
    {
        return ShimContext.Runtime.AcpHal.AllocateContexts(contexts);
    }

    /// <summary>AcpHalReleaseShapeContexts(): called with no argument at teardown; the result is not read.</summary>
    [Shim(Module, "AcpHalReleaseShapeContexts")]
    [UnmanagedCallersOnly]
    private static int AcpHalReleaseShapeContexts()
    {
        ShimContext.Runtime.AcpHal.ReleaseContexts();

        return HResult.Ok;
    }

    /// <summary>HRESULT ApuCreateHeap(UINT32 cachedSizeInBytes, UINT32 nonCachedSizeInBytes).</summary>
    [Shim(Module, "ApuCreateHeap")]
    [UnmanagedCallersOnly]
    private static int ApuCreateHeap(uint cachedBytes, uint nonCachedBytes)
    {
        return ShimContext.Runtime.Apu.Create(cachedBytes, nonCachedBytes);
    }

    /// <summary>HRESULT ApuAlloc(void** virtualAddress, APU_ADDRESS* apuAddress, UINT32 size, UINT32 alignment, UINT32 flags).</summary>
    [Shim(Module, "ApuAlloc")]
    [UnmanagedCallersOnly]
    private static int ApuAlloc(nint* virtualAddress, uint* apuAddress, uint size, uint alignment, uint flags)
    {
        return ShimContext.Runtime.Apu.Allocate(virtualAddress, apuAddress, size, alignment);
    }

    /// <summary>HRESULT ApuFree(void* virtualAddress).</summary>
    [Shim(Module, "ApuFree")]
    [UnmanagedCallersOnly]
    private static int ApuFree(nint virtualAddress)
    {
        return ShimContext.Runtime.Apu.Free(virtualAddress);
    }

    /// <summary>BOOL ApuIsVirtualAddressValid(const void* virtualAddress, UINT32 requirement); the second argument (0x40, 0x80, 0x800 at the call sites) is not used.</summary>
    [Shim(Module, "ApuIsVirtualAddressValid")]
    [UnmanagedCallersOnly]
    private static int ApuIsVirtualAddressValid(nint virtualAddress, uint requirement)
    {
        int valid = ShimContext.Runtime.Apu.IsValid(virtualAddress) ? 1 : 0;

        return valid;
    }

    /// <summary>APU_ADDRESS ApuMapVirtualAddress(const void* virtualAddress).</summary>
    [Shim(Module, "ApuMapVirtualAddress")]
    [UnmanagedCallersOnly]
    private static uint ApuMapVirtualAddress(nint virtualAddress)
    {
        return ShimContext.Runtime.Apu.ToApuAddress(virtualAddress);
    }

    /// <summary>void* ApuMapApuAddress(APU_ADDRESS apuAddress).</summary>
    [Shim(Module, "ApuMapApuAddress")]
    [UnmanagedCallersOnly]
    private static nint ApuMapApuAddress(uint apuAddress)
    {
        return ShimContext.Runtime.Apu.ToVirtualAddress(apuAddress);
    }
}
