// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Runtime.InteropServices;
using FiveStar.Loader.Native;

namespace FiveStar.Loader.Shims.Gdk;

/// <summary>
/// XGameUI.h: there is no system UI offline, so every dialog completes as if the player closed it at once. Dialogs
/// that only inform (achievements, profile card, invites, errors, state share) complete with S_OK; the message dialog
/// answers its cancel button; the player picker picks nobody; text entry and web sign-in complete with E_ABORT, the
/// result of a canceled dialog. The async block is the first argument of every *Async function, so the shims ignore
/// the arguments after the ones they read.
/// </summary>
internal static unsafe class XGameUiShim
{
    private const string MessageDialogApi = "XGameUiShowMessageDialogAsync";
    private const string ErrorDialogApi = "XGameUiShowErrorDialogAsync";
    private const string AchievementsApi = "XGameUiShowAchievementsAsync";
    private const string ProfileCardApi = "XGameUiShowPlayerProfileCardAsync";
    private const string SendInviteApi = "XGameUiShowSendGameInviteAsync";
    private const string ActivityInviteApi = "XGameUiShowMultiplayerActivityGameInviteAsync";
    private const string StateShareApi = "XGameUiShowStateShareAsync";
    private const string PlayerPickerApi = "XGameUiShowPlayerPickerAsync";
    private const string TextEntryApi = "XGameUiShowTextEntryAsync";
    private const string WebAuthenticationApi = "XGameUiShowWebAuthenticationAsync";

    /// <summary>HRESULT XGameUiShowMessageDialogAsync(XAsyncBlock*, title, content, three button texts, defaultButton, cancelButton).</summary>
    [Shim(GdkModule.Name, MessageDialogApi)]
    [UnmanagedCallersOnly]
    private static int XGameUiShowMessageDialogAsync(nint async, nint title, nint content, nint first, nint second, nint third, uint defaultButton, uint cancelButton)
    {
        return Begin(MessageDialogApi, async, DismissedUiOperation.WithValue(cancelButton));
    }

    /// <summary>HRESULT XGameUiShowMessageDialogResult(XAsyncBlock*, XGameUiMessageDialogButton* resultButton).</summary>
    [Shim(GdkModule.Name, "XGameUiShowMessageDialogResult")]
    [UnmanagedCallersOnly]
    private static int XGameUiShowMessageDialogResult(nint async, uint* button)
    {
        if (button == null)
        {
            return HResult.Pointer;
        }

        return Result(MessageDialogApi, async, sizeof(uint), (nint)button);
    }

    [Shim(GdkModule.Name, ErrorDialogApi)]
    [UnmanagedCallersOnly]
    private static int XGameUiShowErrorDialogAsync(nint async)
    {
        return Begin(ErrorDialogApi, async, DismissedUiOperation.Closed());
    }

    [Shim(GdkModule.Name, "XGameUiShowErrorDialogResult")]
    [UnmanagedCallersOnly]
    private static int XGameUiShowErrorDialogResult(nint async)
    {
        return Result(ErrorDialogApi, async, 0, 0);
    }

    [Shim(GdkModule.Name, AchievementsApi)]
    [UnmanagedCallersOnly]
    private static int XGameUiShowAchievementsAsync(nint async)
    {
        return Begin(AchievementsApi, async, DismissedUiOperation.Closed());
    }

    [Shim(GdkModule.Name, "XGameUiShowAchievementsResult")]
    [UnmanagedCallersOnly]
    private static int XGameUiShowAchievementsResult(nint async)
    {
        return Result(AchievementsApi, async, 0, 0);
    }

    [Shim(GdkModule.Name, ProfileCardApi)]
    [UnmanagedCallersOnly]
    private static int XGameUiShowPlayerProfileCardAsync(nint async)
    {
        return Begin(ProfileCardApi, async, DismissedUiOperation.Closed());
    }

    [Shim(GdkModule.Name, "XGameUiShowPlayerProfileCardResult")]
    [UnmanagedCallersOnly]
    private static int XGameUiShowPlayerProfileCardResult(nint async)
    {
        return Result(ProfileCardApi, async, 0, 0);
    }

    [Shim(GdkModule.Name, SendInviteApi)]
    [UnmanagedCallersOnly]
    private static int XGameUiShowSendGameInviteAsync(nint async)
    {
        return Begin(SendInviteApi, async, DismissedUiOperation.Closed());
    }

    [Shim(GdkModule.Name, "XGameUiShowSendGameInviteResult")]
    [UnmanagedCallersOnly]
    private static int XGameUiShowSendGameInviteResult(nint async)
    {
        return Result(SendInviteApi, async, 0, 0);
    }

    [Shim(GdkModule.Name, ActivityInviteApi)]
    [UnmanagedCallersOnly]
    private static int XGameUiShowMultiplayerActivityGameInviteAsync(nint async)
    {
        return Begin(ActivityInviteApi, async, DismissedUiOperation.Closed());
    }

    [Shim(GdkModule.Name, "XGameUiShowMultiplayerActivityGameInviteResult")]
    [UnmanagedCallersOnly]
    private static int XGameUiShowMultiplayerActivityGameInviteResult(nint async)
    {
        return Result(ActivityInviteApi, async, 0, 0);
    }

    [Shim(GdkModule.Name, StateShareApi)]
    [UnmanagedCallersOnly]
    private static int XGameUiShowStateShareAsync(nint async)
    {
        return Begin(StateShareApi, async, DismissedUiOperation.Closed());
    }

    [Shim(GdkModule.Name, "XGameUiShowStateShareResult")]
    [UnmanagedCallersOnly]
    private static int XGameUiShowStateShareResult(nint async)
    {
        return Result(StateShareApi, async, 0, 0);
    }

    [Shim(GdkModule.Name, PlayerPickerApi)]
    [UnmanagedCallersOnly]
    private static int XGameUiShowPlayerPickerAsync(nint async)
    {
        return Begin(PlayerPickerApi, async, DismissedUiOperation.Closed());
    }

    /// <summary>HRESULT XGameUiShowPlayerPickerResultCount(XAsyncBlock*, uint32_t* resultPlayersCount): nobody was picked.</summary>
    [Shim(GdkModule.Name, "XGameUiShowPlayerPickerResultCount")]
    [UnmanagedCallersOnly]
    private static int XGameUiShowPlayerPickerResultCount(nint async, uint* count)
    {
        return NoPlayers(async, count);
    }

    /// <summary>HRESULT XGameUiShowPlayerPickerResult(XAsyncBlock*, uint32_t count, uint64_t* players, uint32_t* resultPlayersUsed).</summary>
    [Shim(GdkModule.Name, "XGameUiShowPlayerPickerResult")]
    [UnmanagedCallersOnly]
    private static int XGameUiShowPlayerPickerResult(nint async, uint count, ulong* players, uint* used)
    {
        return NoPlayers(async, used);
    }

    [Shim(GdkModule.Name, TextEntryApi)]
    [UnmanagedCallersOnly]
    private static int XGameUiShowTextEntryAsync(nint async)
    {
        return Begin(TextEntryApi, async, DismissedUiOperation.Canceled());
    }

    [Shim(GdkModule.Name, "XGameUiShowTextEntryResultSize")]
    [UnmanagedCallersOnly]
    private static int XGameUiShowTextEntryResultSize(nint async, uint* size)
    {
        return Result(TextEntryApi, async, 0, 0);
    }

    [Shim(GdkModule.Name, "XGameUiShowTextEntryResult")]
    [UnmanagedCallersOnly]
    private static int XGameUiShowTextEntryResult(nint async, uint size, byte* text, uint* used)
    {
        return Result(TextEntryApi, async, 0, 0);
    }

    [Shim(GdkModule.Name, WebAuthenticationApi)]
    [Shim(GdkModule.Name, "XGameUiShowWebAuthenticationWithOptionsAsync")]
    [UnmanagedCallersOnly]
    private static int XGameUiShowWebAuthenticationAsync(nint async)
    {
        return Begin(WebAuthenticationApi, async, DismissedUiOperation.Canceled());
    }

    [Shim(GdkModule.Name, "XGameUiShowWebAuthenticationResultSize")]
    [UnmanagedCallersOnly]
    private static int XGameUiShowWebAuthenticationResultSize(nint async, nuint* size)
    {
        return Result(WebAuthenticationApi, async, 0, 0);
    }

    [Shim(GdkModule.Name, "XGameUiShowWebAuthenticationResult")]
    [UnmanagedCallersOnly]
    private static int XGameUiShowWebAuthenticationResult(nint async, nuint size, nint buffer, nint* result, nuint* used)
    {
        return Result(WebAuthenticationApi, async, 0, 0);
    }

    /// <summary>HRESULT XGameUiSetNotificationPositionHint(XGameUiNotificationPositionHint): there are no notifications to place.</summary>
    [Shim(GdkModule.Name, "XGameUiSetNotificationPositionHint")]
    [UnmanagedCallersOnly]
    private static int XGameUiSetNotificationPositionHint(uint position)
    {
        return HResult.Ok;
    }

    private static int Begin(string api, nint async, ManagedAsyncOperation operation)
    {
        var gdk = ShimContext.Runtime.Gdk;

        try
        {
            return gdk.Async.BeginManaged(async, api, operation);
        }
        catch (Exception e)
        {
            return gdk.Boundary.Failed(api, e);
        }
    }

    private static int Result(string api, nint async, nuint size, nint buffer)
    {
        var gdk = ShimContext.Runtime.Gdk;

        try
        {
            return gdk.Async.ManagedResult(async, api, size, buffer);
        }
        catch (Exception e)
        {
            return gdk.Boundary.Failed(api, e);
        }
    }

    private static int NoPlayers(nint async, uint* count)
    {
        int hr = Result(PlayerPickerApi, async, 0, 0);

        if (count != null && !HResult.Failed(hr))
        {
            *count = 0;
        }

        return hr;
    }
}
