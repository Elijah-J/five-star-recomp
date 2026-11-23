// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Runtime.InteropServices;
using FiveStar.Loader.Codegen;
using FiveStar.Loader.Diagnostics;
using FiveStar.Loader.Loading;
using FiveStar.Loader.Native;

namespace FiveStar.Loader.Shims.Storage;

/// <summary>
/// A DirectStorage factory that does nothing: one object whose vtable answers IUnknown, accepts the staging buffer
/// size the game sets at start-up, and returns E_NOTIMPL (logged once) from every other method, so creating a queue
/// or opening a file fails cleanly. The console interface (IDStorageFactoryX) is not public; the slot numbers come from
/// the executable's calls on the factory.
/// </summary>
internal sealed unsafe class DStorageFactoryStub : IDisposable
{
    private const string InterfaceName = "IDStorageFactoryX";

    /// <summary>The executable calls slots up to 10 (+0x50); the rest leave room for calls not seen yet (a guess).</summary>
    private const int VtableSlots = 16;

    private const int QueryInterfaceSlot = 0;
    private const int AddRefSlot = 1;
    private const int ReleaseSlot = 2;

    /// <summary>
    /// Slot 8 (+0x40), called once right after DStorageGetFactory as (this, 0x2000000); a failure there hits an int3.
    /// Read as SetStagingBufferSize (slot 7 on the PC IDStorageFactory; the console interface has one more method).
    /// </summary>
    private const int StagingBufferSizeSlot = 8;

    private readonly object _gate = new();
    private readonly UnresolvedStubs _stubs;
    private readonly CodeHeap _heap;
    private readonly LoaderLog _log;
    private nint _vtable;
    private nint _factory;

    public DStorageFactoryStub(UnresolvedStubs stubs, CodeHeap heap, LoaderLog log)
    {
        _stubs = stubs;
        _heap = heap;
        _log = log;
    }

    /// <summary>DStorageGetFactory: the one factory object for any interface id.</summary>
    public int GetFactory(Guid* iid, nint* result)
    {
        if (result == null)
        {
            return HResult.Pointer;
        }

        lock (_gate)
        {
            if (_factory == 0)
            {
                Build();
            }

            *result = _factory;
        }

        string requested = iid == null ? "null" : iid->ToString();
        _log.Info($"  [xcrdapi] DStorageGetFactory({requested}) -> stub factory (no DirectStorage on this host)");

        return HResult.Ok;
    }

    public void Dispose()
    {
        NativeMemory.Free((void*)_factory);
        NativeMemory.Free((void*)_vtable);
        _factory = 0;
        _vtable = 0;
    }

    private void Build()
    {
        var vtable = (nint*)NativeMemory.AllocZeroed((nuint)(VtableSlots * sizeof(nint)));
        delegate* unmanaged<nint, uint> addRef = &DStorageFactoryMethods.AddRef;
        delegate* unmanaged<nint, uint> release = &DStorageFactoryMethods.Release;
        delegate* unmanaged<nint, uint, int> stagingBufferSize = &DStorageFactoryMethods.SetStagingBufferSize;

        vtable[QueryInterfaceSlot] = _stubs.Create(DStorageShim.Module, $"{InterfaceName}::QueryInterface", HResult.NoInterface, true, false);
        vtable[AddRefSlot] = (nint)addRef;
        vtable[ReleaseSlot] = (nint)release;
        vtable[StagingBufferSizeSlot] = (nint)stagingBufferSize;

        for (int slot = 0; slot < VtableSlots; slot++)
        {
            if (vtable[slot] == 0)
            {
                vtable[slot] = _stubs.Create(DStorageShim.Module, $"{InterfaceName}::slot{slot}", HResult.NotImplemented, true, false);
            }
        }

        // One read-write to read-execute flip for all stubs.
        _heap.Seal();
        _vtable = (nint)vtable;
        _factory = (nint)NativeMemory.AllocZeroed((nuint)sizeof(nint));
        *(nint*)_factory = _vtable;
    }
}
