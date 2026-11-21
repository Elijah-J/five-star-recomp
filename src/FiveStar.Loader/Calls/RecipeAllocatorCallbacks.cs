// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Runtime.InteropServices;
using FiveStar.Loader.Shims;

namespace FiveStar.Loader.Calls;

/// <summary>
/// The HCMemAllocFunction / HCMemFreeFunction callbacks (<c>void* (size_t size, uint32_t memoryType)</c>,
/// <c>void (void* pointer, uint32_t memoryType)</c>) over the runtime's <see cref="RecipeAllocator"/>.
/// </summary>
internal static class RecipeAllocatorCallbacks
{
    [UnmanagedCallersOnly]
    public static nint Allocate(nuint size, uint memoryType)
    {
        return ShimContext.Runtime.RecipeAllocator.Allocate(size);
    }

    [UnmanagedCallersOnly]
    public static void Free(nint block, uint memoryType)
    {
        ShimContext.Runtime.RecipeAllocator.Free(block);
    }

    [UnmanagedCallersOnly]
    public static nint AlwaysFail(nuint size, uint memoryType)
    {
        return ShimContext.Runtime.RecipeAllocator.Fail();
    }

    [UnmanagedCallersOnly]
    public static void FreeNothing(nint block, uint memoryType)
    {
    }
}
