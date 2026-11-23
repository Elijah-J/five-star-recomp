// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Runtime.InteropServices;
using FiveStar.Loader.Native;

namespace FiveStar.Loader.Shims.Storage;

/// <summary>The implemented IDStorageFileX slots. Queued requests hold a reference, so a file outlives its reads.</summary>
internal static unsafe class StorageFileMethods
{
    public static void Fill(nint* vtable)
    {
        delegate* unmanaged<nint, uint> addRef = &AddRef;
        delegate* unmanaged<nint, uint> release = &Release;
        delegate* unmanaged<nint, int> close = &Close;
        delegate* unmanaged<nint, void*, int> information = &GetFileInformation;

        vtable[DStorageLayout.AddRefSlot] = (nint)addRef;
        vtable[DStorageLayout.ReleaseSlot] = (nint)release;
        vtable[DStorageLayout.FileClose] = (nint)close;
        vtable[DStorageLayout.FileGetInformation] = (nint)information;
    }

    /// <summary>Gives up one reference; the last closes the file and frees the object.</summary>
    [HotPath]
    public static uint ReleaseFile(nint self)
    {
        if (!StorageObjects.Drop(self, out uint left))
        {
            return left;
        }

        StorageObjects.Owner<StorageFile>(self).Dispose();
        StorageObjects.Free(self);

        return 0;
    }

    [HotPath]
    [UnmanagedCallersOnly]
    private static uint AddRef(nint self)
    {
        return StorageObjects.AddRef(self);
    }

    [UnmanagedCallersOnly]
    private static uint Release(nint self)
    {
        return ReleaseFile(self);
    }

    [UnmanagedCallersOnly]
    private static int Close(nint self)
    {
        StorageObjects.Owner<StorageFile>(self).Dispose();

        return HResult.Ok;
    }

    [UnmanagedCallersOnly]
    private static int GetFileInformation(nint self, void* information)
    {
        return StorageObjects.Owner<StorageFile>(self).GetInformation(information);
    }
}
