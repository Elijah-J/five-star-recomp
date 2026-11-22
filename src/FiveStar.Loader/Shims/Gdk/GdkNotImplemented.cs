// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using FiveStar.Loader.Native;

namespace FiveStar.Loader.Shims.Gdk;

/// <summary>
/// GDK functions (names from the public headers) the offline shim deliberately does not implement. Each binds to a stub
/// that returns the listed value and logs its first call: E_NOTIMPL for HRESULT functions, 0 (false, nothing) for
/// bool, count and void ones, and E_GAMESTREAMING_NOT_INITIALIZED for game streaming, which is never initialized.
/// </summary>
internal static class GdkNotImplemented
{
    private const long NotImplemented = HResult.NotImplemented;
    private const long Zero = 0;

    /// <summary>E_GAMESTREAMING_NOT_INITIALIZED (XGameErr.h).</summary>
    private const long StreamingNotInitialized = unchecked((int)0x89245400);

    [NotImplementedTable(GdkModule.Name)]
    private static readonly (string Name, long ReturnValue)[] Catalog =
    [
        // XUser.h: tokens, pictures, UI and the PC platform hooks need Xbox services.
        ("XUserGetTokenAndSignatureAsync", NotImplemented), ("XUserGetTokenAndSignatureResultSize", NotImplemented), ("XUserGetTokenAndSignatureResult", NotImplemented),
        ("XUserGetTokenAndSignatureUtf16Async", NotImplemented), ("XUserGetTokenAndSignatureUtf16ResultSize", NotImplemented), ("XUserGetTokenAndSignatureUtf16Result", NotImplemented),
        ("XUserResolveIssueWithUiAsync", NotImplemented), ("XUserResolveIssueWithUiResult", NotImplemented), ("XUserResolveIssueWithUiUtf16Async", NotImplemented), ("XUserResolveIssueWithUiUtf16Result", NotImplemented),
        ("XUserResolvePrivilegeWithUiAsync", NotImplemented), ("XUserResolvePrivilegeWithUiResult", NotImplemented),
        ("XUserGetGamerPictureAsync", NotImplemented), ("XUserGetGamerPictureResultSize", NotImplemented), ("XUserGetGamerPictureResult", NotImplemented),
        ("XUserAddByIdWithUiAsync", NotImplemented), ("XUserAddByIdWithUiResult", NotImplemented), ("XUserFindForWebAccount", NotImplemented),
        ("XUserGetMsaTokenSilentlyAsync", NotImplemented), ("XUserGetMsaTokenSilentlyResult", NotImplemented), ("XUserGetMsaTokenSilentlyResultSize", NotImplemented),
        ("XUserSignOutAsync", NotImplemented), ("XUserSignOutResult", NotImplemented), ("XUserIsStoreUser", Zero),
        ("XUserPlatformRemoteConnectSetEventHandlers", NotImplemented), ("XUserPlatformRemoteConnectCancelPrompt", NotImplemented),
        ("XUserPlatformSpopPromptSetEventHandlers", NotImplemented), ("XUserPlatformSpopPromptComplete", NotImplemented), ("XUserPlatformUiSetEventHandlers", NotImplemented),

        // XStore.h (license, products, purchase UI)
        ("XStoreAcquireLicenseForPackageAsync", NotImplemented), ("XStoreAcquireLicenseForPackageResult", NotImplemented),
        ("XStoreAcquireLicenseForDurablesAsync", NotImplemented), ("XStoreAcquireLicenseForDurablesResult", NotImplemented),
        ("XStoreCanAcquireLicenseForPackageAsync", NotImplemented), ("XStoreCanAcquireLicenseForPackageResult", NotImplemented),
        ("XStoreCanAcquireLicenseForStoreIdAsync", NotImplemented), ("XStoreCanAcquireLicenseForStoreIdResult", NotImplemented),
        ("XStoreCloseLicenseHandle", Zero), ("XStoreIsLicenseValid", Zero), ("XStoreIsAvailabilityPurchasable", Zero),
        ("XStoreQueryAddOnLicensesAsync", NotImplemented), ("XStoreQueryAddOnLicensesResultCount", NotImplemented), ("XStoreQueryAddOnLicensesResult", NotImplemented),
        ("XStoreQueryAssociatedProductsAsync", NotImplemented), ("XStoreQueryAssociatedProductsResult", NotImplemented),
        ("XStoreQueryAssociatedProductsForStoreIdAsync", NotImplemented), ("XStoreQueryAssociatedProductsForStoreIdResult", NotImplemented),
        ("XStoreQueryEntitledProductsAsync", NotImplemented), ("XStoreQueryEntitledProductsResult", NotImplemented),
        ("XStoreQueryProductsAsync", NotImplemented), ("XStoreQueryProductsResult", NotImplemented),
        ("XStoreQueryProductForCurrentGameAsync", NotImplemented), ("XStoreQueryProductForCurrentGameResult", NotImplemented),
        ("XStoreQueryProductForPackageAsync", NotImplemented), ("XStoreQueryProductForPackageResult", NotImplemented),
        ("XStoreProductsQueryHasMorePages", Zero), ("XStoreProductsQueryNextPageAsync", NotImplemented), ("XStoreProductsQueryNextPageResult", NotImplemented),
        ("XStoreEnumerateProductsQuery", NotImplemented), ("XStoreCloseProductsQueryHandle", Zero),
        ("XStoreQueryConsumableBalanceRemainingAsync", NotImplemented), ("XStoreQueryConsumableBalanceRemainingResult", NotImplemented),
        ("XStoreReportConsumableFulfillmentAsync", NotImplemented), ("XStoreReportConsumableFulfillmentResult", NotImplemented),
        ("XStoreGetUserCollectionsIdAsync", NotImplemented), ("XStoreGetUserCollectionsIdResultSize", NotImplemented), ("XStoreGetUserCollectionsIdResult", NotImplemented),
        ("XStoreGetUserPurchaseIdAsync", NotImplemented), ("XStoreGetUserPurchaseIdResultSize", NotImplemented), ("XStoreGetUserPurchaseIdResult", NotImplemented),
        ("XStoreQueryLicenseTokenAsync", NotImplemented), ("XStoreQueryLicenseTokenResultSize", NotImplemented), ("XStoreQueryLicenseTokenResult", NotImplemented),
        ("XStoreSendRequestAsync", NotImplemented), ("XStoreSendRequestResultSize", NotImplemented), ("XStoreSendRequestResult", NotImplemented),
        ("XStoreShowAssociatedProductsUIAsync", NotImplemented), ("XStoreShowAssociatedProductsUIResult", NotImplemented),
        ("XStoreShowProductPageUIAsync", NotImplemented), ("XStoreShowProductPageUIResult", NotImplemented),
        ("XStoreShowPurchaseUIAsync", NotImplemented), ("XStoreShowPurchaseUIResult", NotImplemented),
        ("XStoreShowRateAndReviewUIAsync", NotImplemented), ("XStoreShowRateAndReviewUIResult", NotImplemented),
        ("XStoreShowRedeemTokenUIAsync", NotImplemented), ("XStoreShowRedeemTokenUIResult", NotImplemented),
        ("XStoreShowGiftingUIAsync", NotImplemented), ("XStoreShowGiftingUIResult", NotImplemented),
        ("XStoreRegisterGameLicenseChanged", NotImplemented), ("XStoreUnregisterGameLicenseChanged", Zero),
        ("XStoreRegisterPackageLicenseLost", NotImplemented), ("XStoreUnregisterPackageLicenseLost", Zero),
        ("XStoreQueryPackageIdentifier", NotImplemented), ("XStoreQueryGameAndDlcPackageUpdatesAsync", NotImplemented), ("XStoreQueryGameAndDlcPackageUpdatesResult", NotImplemented),
        ("XStoreQueryGameAndDlcPackageUpdatesResultCount", NotImplemented), ("XStoreDownloadPackageUpdatesAsync", NotImplemented), ("XStoreDownloadPackageUpdatesResult", NotImplemented),
        ("XStoreDownloadAndInstallPackageUpdatesAsync", NotImplemented), ("XStoreDownloadAndInstallPackageUpdatesResult", NotImplemented),
        ("XStoreDownloadAndInstallPackagesAsync", NotImplemented), ("XStoreDownloadAndInstallPackagesResult", NotImplemented), ("XStoreDownloadAndInstallPackagesResultCount", NotImplemented),
        ("XStoreQueryPackageUpdatesAsync", NotImplemented), ("XStoreQueryPackageUpdatesResult", NotImplemented), ("XStoreQueryPackageUpdatesResultCount", NotImplemented),

        // XGameUI.h: text entry windows, the title-drawn UI callbacks and the storage manager.
        ("XGameUiTextEntryOpen", NotImplemented), ("XGameUiTextEntryClose", Zero), ("XGameUiTextEntryGetState", NotImplemented),
        ("XGameUiTextEntryGetExtents", NotImplemented), ("XGameUiTextEntryUpdatePositionHint", NotImplemented), ("XGameUiTextEntryUpdateVisibility", NotImplemented),
        ("XGameUiSetUiCallbacks", NotImplemented), ("XGameUiSetMessageDialogUiResponse", NotImplemented), ("XGameUiSetPlayerPickerUiResponse", NotImplemented),
        ("XGameUiSetTextEntryUiResponse", NotImplemented), ("XGameUiSetPlayerProfileCardUiResponse", NotImplemented), ("XGameUiSetSendGameInviteUiResponse", NotImplemented),
        ("XGameUiSetAchievementsUiResponse", NotImplemented), ("XGameUiSetMultiplayerActivityGameInviteUiResponse", NotImplemented), ("XGameUiSetErrorDialogUiResponse", NotImplemented),
        ("XGameUiShowManageSpaceAsync", NotImplemented), ("XGameUiShowManageSpaceResult", NotImplemented),

        // XGame.h, XGameInvite.h, XGameActivation.h: there is never an invite to accept.
        ("XLaunchNewGame", Zero), ("XGameInviteAcceptPendingInvite", NotImplemented), ("XGameActivationAcceptPendingInvite", NotImplemented),

        // XNetworking.h: URL security, certificates, statistics and settings.
        ("XNetworkingQuerySecurityInformationForUrlAsync", NotImplemented), ("XNetworkingQuerySecurityInformationForUrlAsyncResult", NotImplemented),
        ("XNetworkingQuerySecurityInformationForUrlAsyncResultSize", NotImplemented), ("XNetworkingQuerySecurityInformationForUrlUtf16Async", NotImplemented),
        ("XNetworkingQuerySecurityInformationForUrlUtf16AsyncResult", NotImplemented), ("XNetworkingQuerySecurityInformationForUrlUtf16AsyncResultSize", NotImplemented),
        ("XNetworkingVerifyServerCertificate", NotImplemented), ("XNetworkingQueryStatistics", NotImplemented),
        ("XNetworkingQueryConfigurationSetting", NotImplemented), ("XNetworkingSetConfigurationSetting", NotImplemented),
        ("XNetworkingGetTitleTcpQueueingReservation", NotImplemented), ("XNetworkingGetTitleUdpQueueingReservation", NotImplemented),

        // XAppCapture.h (feature reported unavailable)
        ("XAppCaptureTakeScreenshot", NotImplemented), ("XAppCaptureOpenScreenshotStream", NotImplemented), ("XAppCaptureReadScreenshotStream", NotImplemented),
        ("XAppCaptureCloseScreenshotStream", NotImplemented), ("XAppCaptureRecordTimespan", NotImplemented), ("XAppCaptureEnableRecord", Zero), ("XAppCaptureDisableRecord", Zero),
        ("XAppCaptureMetadataAddStringEvent", NotImplemented), ("XAppCaptureMetadataAddInt32Event", NotImplemented), ("XAppCaptureMetadataAddDoubleEvent", NotImplemented),
        ("XAppCaptureMetadataStartStringState", NotImplemented), ("XAppCaptureMetadataStartInt32State", NotImplemented), ("XAppCaptureMetadataStartDoubleState", NotImplemented),
        ("XAppCaptureMetadataStopState", NotImplemented), ("XAppCaptureMetadataStopAllStates", NotImplemented), ("XAppCaptureMetadataRemainingStorageBytesAvailable", NotImplemented),
        ("XAppCaptureRegisterMetadataPurged", NotImplemented), ("XAppCaptureUnRegisterMetadataPurged", Zero),
        ("XAppCaptureStartUserRecord", NotImplemented), ("XAppCaptureStopUserRecord", NotImplemented), ("XAppCaptureCancelUserRecord", NotImplemented),
        ("XAppCaptureReadLocalStream", NotImplemented), ("XAppCaptureCloseLocalStream", NotImplemented), ("XAppCaptureGetVideoCaptureSettings", NotImplemented),
        ("XAppCaptureTakeDiagnosticScreenshot", NotImplemented), ("XAppCaptureRecordDiagnosticClip", NotImplemented),
        ("XAppBroadcastGetStatus", NotImplemented), ("XAppBroadcastIsAppBroadcasting", Zero), ("XAppBroadcastShowUI", NotImplemented),
        ("XAppBroadcastRegisterIsAppBroadcastingChanged", NotImplemented), ("XAppBroadcastUnregisterIsAppBroadcastingChanged", Zero),

        // XAccessibility.h and XSpeechSynthesizer.h (speech feature reported unavailable)
        ("XAccessibilityGetClosedCaptionProperties", NotImplemented), ("XAccessibilityGetHighContrastMode", NotImplemented), ("XAccessibilityGetTextToSpeechProperties", NotImplemented),
        ("XSpeechSynthesizerCreate", NotImplemented), ("XSpeechSynthesizerCloseHandle", NotImplemented), ("XSpeechSynthesizerSetDefaultVoice", NotImplemented),
        ("XSpeechSynthesizerSetCustomVoice", NotImplemented), ("XSpeechSynthesizerCreateStreamFromText", NotImplemented), ("XSpeechSynthesizerCreateStreamFromSsml", NotImplemented),
        ("XSpeechSynthesizerGetStreamDataSize", NotImplemented), ("XSpeechSynthesizerGetStreamData", NotImplemented), ("XSpeechSynthesizerCloseStreamHandle", NotImplemented),
        ("XSpeechToTextSendString", NotImplemented), ("XSpeechToTextSetPositionHint", NotImplemented), ("XSpeechToTextBeginHypothesisString", NotImplemented),
        ("XSpeechToTextUpdateHypothesisString", NotImplemented), ("XSpeechToTextFinalizeHypothesisString", NotImplemented), ("XSpeechToTextCancelHypothesisString", NotImplemented),

        // XGameStreaming.h (feature reported unavailable; never initialized)
        ("XGameStreamingInitialize", NotImplemented), ("XGameStreamingUninitialize", Zero), ("XGameStreamingIsStreaming", Zero),
        ("XGameStreamingGetClientCount", Zero), ("XGameStreamingGetClients", StreamingNotInitialized), ("XGameStreamingRegisterConnectionStateChanged", StreamingNotInitialized),
        ("XGameStreamingUnregisterConnectionStateChanged", Zero), ("XGameStreamingGetStreamPhysicalDimensions", StreamingNotInitialized),
        ("XGameStreamingGetConnectionState", Zero), ("XGameStreamingRegisterClientPropertiesChanged", StreamingNotInitialized), ("XGameStreamingUnregisterClientPropertiesChanged", Zero),
        ("XGameStreamingGetAssociatedFrame", StreamingNotInitialized), ("XGameStreamingGetLastFrameDisplayed", StreamingNotInitialized), ("XGameStreamingGetClientIPAddress", StreamingNotInitialized),
        ("XGameStreamingGetDisplayDetails", StreamingNotInitialized), ("XGameStreamingGetGamepadPhysicality", StreamingNotInitialized), ("XGameStreamingGetSessionId", StreamingNotInitialized),
        ("XGameStreamingGetServerLocationName", StreamingNotInitialized), ("XGameStreamingGetServerLocationNameSize", Zero), ("XGameStreamingGetStreamAddedLatency", StreamingNotInitialized),
        ("XGameStreamingGetTouchBundleVersion", StreamingNotInitialized), ("XGameStreamingGetTouchBundleVersionNameSize", Zero), ("XGameStreamingIsTouchInputEnabled", StreamingNotInitialized),
        ("XGameStreamingSetResolution", StreamingNotInitialized), ("XGameStreamingSendDebugMessageToClient", StreamingNotInitialized),
        ("XGameStreamingShowTouchControlLayout", Zero), ("XGameStreamingShowTouchControlLayoutOnClient", Zero), ("XGameStreamingHideTouchControls", Zero),
        ("XGameStreamingHideTouchControlsOnClient", Zero), ("XGameStreamingShowTouchControlsWithStateUpdate", StreamingNotInitialized),
        ("XGameStreamingShowTouchControlsWithStateUpdateOnClient", StreamingNotInitialized), ("XGameStreamingUpdateTouchControlsState", StreamingNotInitialized),
        ("XGameStreamingUpdateTouchControlsStateOnClient", StreamingNotInitialized),
        ("EXPERIMENTAL_XGameStreamingGetClientOrientation", StreamingNotInitialized), ("EXPERIMENTAL_XGameStreamingGetMotionInfo", StreamingNotInitialized),
        ("EXPERIMENTAL_XGameStreamingGetMotionState", StreamingNotInitialized),

        // XPackage.h (the rest)
        ("XPackageUninstallPackage", Zero), ("XPackageUninstallUWPInstance", NotImplemented), ("XPackageGetIdentifier", NotImplemented),
        ("XPackageInstallChunks", NotImplemented), ("XPackageInstallChunksAsync", NotImplemented), ("XPackageInstallChunksResult", NotImplemented),
        ("XPackageUninstallChunks", NotImplemented), ("XPackageEnumerateChunkAvailability", NotImplemented), ("XPackageChangeChunkInstallOrder", NotImplemented),
        ("XPersistentLocalStorageMountForPackage", NotImplemented),

        // PFXGameSave.h: PlayFab cloud saves for PC; titles use XGameSave offline.
        ("PFXGameSaveInitializeConfig", NotImplemented), ("PFXGameSaveFreeConfig", Zero), ("PFXGameSaveFilesGetFolderWithUiAsync", NotImplemented),
        ("PFXGameSaveFilesGetFolderWithUiResult", NotImplemented), ("PFXGameSaveFilesGetRemainingQuota", NotImplemented), ("PFXGameSaveFilesUploadWithUiAsync", NotImplemented),
        ("PFXGameSaveFilesUploadWithUiResult", NotImplemented), ("PFXGameSaveFilesSetSaveDescriptionAsync", NotImplemented), ("PFXGameSaveFilesSetSaveDescriptionResult", NotImplemented),
        ("PFXGameSaveSetUiCallbacks", NotImplemented), ("PFXGameSaveSetActiveDeviceChangedCallback", NotImplemented), ("PFXGameSaveSetActiveDeviceContentionUiResponse", NotImplemented),
        ("PFXGameSaveSetConflictUiResponse", NotImplemented), ("PFXGameSaveSetOutOfStorageUiResponse", NotImplemented), ("PFXGameSaveSetProgressUiResponse", NotImplemented),
        ("PFXGameSaveSetSyncFailedUiResponse", NotImplemented), ("PFXGameSaveProgressUiGetProgress", NotImplemented),

        // XSystem.h and the rest
        ("XSystemHandleTrack", NotImplemented),
        ("XGameGetXboxTitleIdUtf16", NotImplemented), ("XGameEventWriteUtf16", NotImplemented),
        ("XUserGetSignOutDeferralUtf16", NotImplemented),
        ("XGameSaveFilesGetFolderWithUiUtf16Async", NotImplemented), ("XGameSaveFilesGetFolderWithUiUtf16Result", NotImplemented),
    ];
}
