// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Runtime.InteropServices;
using FiveStar.Loader.Native;

namespace FiveStar.Loader.Shims.Input;

/// <summary>The implemented IGameInput methods. Every other slot is a not-implemented stub.</summary>
internal static unsafe class GameInputMethods
{
    private const long NoSequence = 0;

    public static void Fill(nint* vtable)
    {
        delegate* unmanaged<nint, Guid*, nint*, int> query = &QueryInterface;
        delegate* unmanaged<nint, ulong> timestamp = &GetCurrentTimestamp;
        delegate* unmanaged<nint, uint, nint, nint*, int> current = &GetCurrentReading;
        delegate* unmanaged<nint, nint, uint, nint, nint*, int> next = &GetNextReading;
        delegate* unmanaged<nint, nint, uint, nint, nint*, int> previous = &GetPreviousReading;
        delegate* unmanaged<nint, nint, uint, uint, uint, nint, nint, ulong*, int> register = &RegisterDeviceCallback;

        delegate* unmanaged<nint, ulong, void> stop = &StopCallback;
        delegate* unmanaged<nint, ulong, ulong, byte> unregister = &UnregisterCallback;
        delegate* unmanaged<nint, byte*, nint*, int> find = &FindDeviceFromId;
        vtable[GameInputLayout.QueryInterfaceSlot] = (nint)query;
        vtable[GameInputLayout.InputGetCurrentTimestamp] = (nint)timestamp;
        vtable[GameInputLayout.InputGetCurrentReading] = (nint)current;

        vtable[GameInputLayout.InputGetNextReading] = (nint)next;
        vtable[GameInputLayout.InputGetPreviousReading] = (nint)previous;
        vtable[GameInputLayout.InputRegisterDeviceCallback] = (nint)register;
        vtable[GameInputLayout.InputStopCallback] = (nint)stop;
        vtable[GameInputLayout.InputUnregisterCallback] = (nint)unregister;
        vtable[GameInputLayout.InputFindDeviceFromId] = (nint)find;
    }

    [UnmanagedCallersOnly]
    private static int QueryInterface(nint self, Guid* iid, nint* result)
    {
        return InputObjects.Query(self, iid, result, GameInputLayout.InputIid);
    }

    [HotPath]
    [UnmanagedCallersOnly]
    private static ulong GetCurrentTimestamp(nint self)
    {
        return InputObjects.Session(self).Timestamp();
    }

    [HotPath]
    [UnmanagedCallersOnly]
    private static int GetCurrentReading(nint self, uint kind, nint device, nint* reading)
    {
        return InputObjects.Session(self).GetReading(kind, device, NoSequence, long.MaxValue, true, reading);
    }

    [HotPath]
    [UnmanagedCallersOnly]
    private static int GetNextReading(nint self, nint reference, uint kind, nint device, nint* reading)
    {
        if (reference == 0)
        {
            return HResult.InvalidArgument;
        }

        long after = ((ReadingObject*)reference)->Sequence;

        return InputObjects.Session(self).GetReading(kind, device, after, long.MaxValue, false, reading);
    }

    [HotPath]
    [UnmanagedCallersOnly]
    private static int GetPreviousReading(nint self, nint reference, uint kind, nint device, nint* reading)
    {
        if (reference == 0)
        {
            return HResult.InvalidArgument;
        }

        long before = ((ReadingObject*)reference)->Sequence;

        return InputObjects.Session(self).GetReading(kind, device, NoSequence, before, true, reading);
    }

    [UnmanagedCallersOnly]
    private static int RegisterDeviceCallback(nint self, nint device, uint kind, uint statusFilter, uint enumeration, nint context, nint function, ulong* token)
    {
        return InputObjects.Session(self).RegisterDeviceCallback(device, kind, statusFilter, enumeration, context, function, token);
    }

    /// <summary>StopCallback: the callback is removed at once.</summary>
    [UnmanagedCallersOnly]
    private static void StopCallback(nint self, ulong token)
    {
        InputObjects.Session(self).Unregister(token);
    }

    [UnmanagedCallersOnly]
    private static int FindDeviceFromId(nint self, byte* id, nint* device)
    {
        return InputObjects.Session(self).FindDevice(id, device);
    }

    [UnmanagedCallersOnly]
    private static byte UnregisterCallback(nint self, ulong token, ulong timeoutMicroseconds)
    {
        bool removed = InputObjects.Session(self).Unregister(token);
        byte result = removed ? (byte)1 : (byte)0;

        return result;
    }
}
