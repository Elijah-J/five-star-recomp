// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Runtime.InteropServices;

namespace FiveStar.Loader.Shims.Storage;

/// <summary>The implemented IDStorageQueueX slots. Every other slot is a not-implemented stub.</summary>
internal static unsafe class StorageQueueMethods
{
    public static void Fill(nint* vtable)
    {
        delegate* unmanaged<nint, uint> addRef = &AddRef;
        delegate* unmanaged<nint, uint> release = &Release;

        delegate* unmanaged<nint, byte*, void> enqueueRequest = &EnqueueRequest;
        delegate* unmanaged<nint, nint, uint, void> enqueueStatus = &EnqueueStatus;
        delegate* unmanaged<nint, nint, ulong, void> enqueueSignal = &EnqueueSignal;
        delegate* unmanaged<nint, void> submit = &Submit;
        delegate* unmanaged<nint, void> close = &Close;

        vtable[DStorageLayout.AddRefSlot] = (nint)addRef;
        vtable[DStorageLayout.ReleaseSlot] = (nint)release;

        vtable[DStorageLayout.QueueEnqueueRequest] = (nint)enqueueRequest;
        vtable[DStorageLayout.QueueEnqueueStatus] = (nint)enqueueStatus;
        vtable[DStorageLayout.QueueEnqueueSignal] = (nint)enqueueSignal;
        vtable[DStorageLayout.QueueSubmit] = (nint)submit;
        vtable[DStorageLayout.QueueClose] = (nint)close;
    }

    [HotPath]
    [UnmanagedCallersOnly]
    private static uint AddRef(nint self)
    {
        return StorageObjects.AddRef(self);
    }

    /// <summary>The last reference closes the queue (waiting for the request in progress) and frees it.</summary>
    [UnmanagedCallersOnly]
    private static uint Release(nint self)
    {
        if (!StorageObjects.Drop(self, out uint left))
        {
            return left;
        }

        StorageObjects.Owner<StorageQueue>(self).Dispose();
        StorageObjects.Free(self);

        return 0;
    }

    [HotPath]
    [UnmanagedCallersOnly]
    private static void EnqueueRequest(nint self, byte* request)
    {
        if (request != null)
        {
            StorageObjects.Owner<StorageQueue>(self).EnqueueRequest(request);
        }
    }

    [HotPath]
    [UnmanagedCallersOnly]
    private static void EnqueueStatus(nint self, nint statusArray, uint index)
    {
        StorageObjects.Owner<StorageQueue>(self).EnqueueStatus(statusArray, index);
    }

    [HotPath]
    [UnmanagedCallersOnly]
    private static void EnqueueSignal(nint self, nint fence, ulong value)
    {
        StorageObjects.Owner<StorageQueue>(self).EnqueueSignal(fence, value);
    }

    [HotPath]
    [UnmanagedCallersOnly]
    private static void Submit(nint self)
    {
        StorageObjects.Owner<StorageQueue>(self).Submit();
    }

    /// <summary>Close: stops the worker; the object stays valid until its last Release.</summary>
    [UnmanagedCallersOnly]
    private static void Close(nint self)
    {
        StorageObjects.Owner<StorageQueue>(self).Dispose();
    }
}
