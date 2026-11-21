// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Runtime.InteropServices;
using FiveStar.Loader.Native;

namespace FiveStar.Loader.Shims.Gdk;

/// <summary>
/// XUser.h entry points; the work is in <see cref="UserService"/>. Token, picture and UI functions are in the
/// not-implemented catalog.
/// </summary>
internal static unsafe class XUserShim
{
    /// <summary>HRESULT XUserAddAsync(XUserAddOptions options, XAsyncBlock* async).</summary>
    [Shim(GdkModule.Name, "XUserAddAsync")]
    [UnmanagedCallersOnly]
    private static int XUserAddAsync(uint options, nint async)
    {
        var gdk = ShimContext.Runtime.Gdk;

        try
        {
            return gdk.Async.BeginManaged(async, UserService.AddApi, new UserAddOperation(gdk.Users, gdk.Boundary, options));
        }
        catch (Exception e)
        {
            return gdk.Boundary.Failed(nameof(XUserAddAsync), e);
        }
    }

    /// <summary>HRESULT XUserAddResult(XAsyncBlock* async, XUserHandle* newUser).</summary>
    [Shim(GdkModule.Name, "XUserAddResult")]
    [UnmanagedCallersOnly]
    private static int XUserAddResult(nint async, nint* newUser)
    {
        var gdk = ShimContext.Runtime.Gdk;

        if (newUser == null)
        {
            return HResult.Pointer;
        }

        try
        {
            return gdk.Async.ManagedResult(async, UserService.AddApi, (nuint)sizeof(nint), (nint)newUser);
        }
        catch (Exception e)
        {
            return gdk.Boundary.Failed(nameof(XUserAddResult), e);
        }
    }

    [Shim(GdkModule.Name, "XUserDuplicateHandle")]
    [UnmanagedCallersOnly]
    private static int XUserDuplicateHandle(nint handle, nint* duplicated)
    {
        return ShimContext.Runtime.Gdk.Users.Duplicate(handle, duplicated);
    }

    [Shim(GdkModule.Name, "XUserCloseHandle")]
    [UnmanagedCallersOnly]
    private static void XUserCloseHandle(nint user)
    {
        ShimContext.Runtime.Gdk.Users.Close(user);
    }

    /// <summary>int32_t XUserCompare(XUserHandle user1, XUserHandle user2): ordered by pointer.</summary>
    [Shim(GdkModule.Name, "XUserCompare")]
    [UnmanagedCallersOnly]
    private static int XUserCompare(nint first, nint second)
    {
        return ((ulong)first).CompareTo((ulong)second);
    }

    [Shim(GdkModule.Name, "XUserGetMaxUsers")]
    [UnmanagedCallersOnly]
    private static int XUserGetMaxUsers(uint* maxUsers)
    {
        if (maxUsers == null)
        {
            return HResult.Pointer;
        }

        *maxUsers = 1;

        return HResult.Ok;
    }

    [Shim(GdkModule.Name, "XUserGetId")]
    [UnmanagedCallersOnly]
    private static int XUserGetId(nint user, ulong* id)
    {
        return ShimContext.Runtime.Gdk.Users.WriteValue(user, id, UserService.Xuid);
    }

    [Shim(GdkModule.Name, "XUserFindUserById")]
    [UnmanagedCallersOnly]
    private static int XUserFindUserById(ulong id, nint* handle)
    {
        return ShimContext.Runtime.Gdk.Users.FindById(id, handle);
    }

    /// <summary>HRESULT XUserGetLocalId(XUserHandle user, XUserLocalId* userLocalId); XUserLocalId is one uint64_t.</summary>
    [Shim(GdkModule.Name, "XUserGetLocalId")]
    [UnmanagedCallersOnly]
    private static int XUserGetLocalId(nint user, ulong* localId)
    {
        return ShimContext.Runtime.Gdk.Users.WriteValue(user, localId, UserService.LocalId);
    }

    /// <summary>HRESULT XUserFindUserByLocalId(XUserLocalId userLocalId, XUserHandle* handle): the 8-byte struct travels by value.</summary>
    [Shim(GdkModule.Name, "XUserFindUserByLocalId")]
    [UnmanagedCallersOnly]
    private static int XUserFindUserByLocalId(ulong localId, nint* handle)
    {
        return ShimContext.Runtime.Gdk.Users.FindByLocalId(localId, handle);
    }

    [Shim(GdkModule.Name, "XUserGetIsGuest")]
    [UnmanagedCallersOnly]
    private static int XUserGetIsGuest(nint user, byte* isGuest)
    {
        return ShimContext.Runtime.Gdk.Users.GetIsGuest(user, isGuest);
    }

    [Shim(GdkModule.Name, "XUserGetState")]
    [UnmanagedCallersOnly]
    private static int XUserGetState(nint user, uint* state)
    {
        return ShimContext.Runtime.Gdk.Users.GetState(user, state);
    }

    /// <summary>HRESULT XUserGetGamertag(XUserHandle, XUserGamertagComponent, size_t gamertagSize, char* gamertag, size_t* gamertagUsed).</summary>
    [Shim(GdkModule.Name, "XUserGetGamertag")]
    [UnmanagedCallersOnly]
    private static int XUserGetGamertag(nint user, uint component, nuint size, byte* gamertag, nuint* used)
    {
        return ShimContext.Runtime.Gdk.Users.GetGamertag(user, component, size, gamertag, used);
    }

    [Shim(GdkModule.Name, "XUserGetAgeGroup")]
    [UnmanagedCallersOnly]
    private static int XUserGetAgeGroup(nint user, uint* ageGroup)
    {
        return ShimContext.Runtime.Gdk.Users.GetAgeGroup(user, ageGroup);
    }

    /// <summary>HRESULT XUserCheckPrivilege(XUserHandle, XUserPrivilegeOptions, XUserPrivilege, bool* hasPrivilege, XUserPrivilegeDenyReason* reason).</summary>
    [Shim(GdkModule.Name, "XUserCheckPrivilege")]
    [UnmanagedCallersOnly]
    private static int XUserCheckPrivilege(nint user, uint options, uint privilege, byte* hasPrivilege, uint* reason)
    {
        var gdk = ShimContext.Runtime.Gdk;

        return gdk.Users.CheckPrivilege(user, privilege, hasPrivilege, reason, gdk.Boundary);
    }

    /// <summary>HRESULT XUserRegisterForChangeEvent(XTaskQueueHandle, void* context, XUserChangeEventCallback*, XTaskQueueRegistrationToken*).</summary>
    [Shim(GdkModule.Name, "XUserRegisterForChangeEvent")]
    [UnmanagedCallersOnly]
    private static int XUserRegisterForChangeEvent(nint queue, nint context, nint callback, ulong* token)
    {
        return ShimContext.Runtime.Gdk.Users.RegisterForChangeEvent(callback, token);
    }

    /// <summary>bool XUserUnregisterForChangeEvent(XTaskQueueRegistrationToken token, bool wait).</summary>
    [Shim(GdkModule.Name, "XUserUnregisterForChangeEvent")]
    [UnmanagedCallersOnly]
    private static byte XUserUnregisterForChangeEvent(ulong token, byte wait)
    {
        return GdkModule.True;
    }

    [Shim(GdkModule.Name, "XUserGetSignOutDeferral")]
    [UnmanagedCallersOnly]
    private static int XUserGetSignOutDeferral(nint* deferral)
    {
        return ShimContext.Runtime.Gdk.Users.GetSignOutDeferral(deferral);
    }

    [Shim(GdkModule.Name, "XUserCloseSignOutDeferralHandle")]
    [UnmanagedCallersOnly]
    private static void XUserCloseSignOutDeferralHandle(nint deferral)
    {
    }
}
