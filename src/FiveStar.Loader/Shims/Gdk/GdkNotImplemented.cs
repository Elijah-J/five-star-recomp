// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using FiveStar.Loader.Native;

namespace FiveStar.Loader.Shims.Gdk;

/// <summary>
/// GDK functions (names from the public headers) the offline shim deliberately does not implement. Each binds to a stub
/// that returns the listed value and logs its first call: E_NOTIMPL for HRESULT functions, 0 for bool and void ones.
/// </summary>
internal static class GdkNotImplemented
{
    private const long NotImplemented = HResult.NotImplemented;
    private const long Zero = 0;

    [NotImplementedTable(GdkModule.Name)]
    private static readonly (string Name, long ReturnValue)[] Catalog =
    [
        // XUser.h
        ("XUserGetTokenAndSignatureAsync", NotImplemented), ("XUserGetTokenAndSignatureResultSize", NotImplemented), ("XUserGetTokenAndSignatureResult", NotImplemented),
        ("XUserGetTokenAndSignatureUtf16Async", NotImplemented), ("XUserGetTokenAndSignatureUtf16ResultSize", NotImplemented), ("XUserGetTokenAndSignatureUtf16Result", NotImplemented),
        ("XUserResolveIssueWithUiAsync", NotImplemented), ("XUserResolveIssueWithUiResult", NotImplemented), ("XUserResolveIssueWithUiUtf16Async", NotImplemented), ("XUserResolveIssueWithUiUtf16Result", NotImplemented),
        ("XUserResolvePrivilegeWithUiAsync", NotImplemented), ("XUserResolvePrivilegeWithUiResult", NotImplemented),
        ("XUserGetGamerPictureAsync", NotImplemented), ("XUserGetGamerPictureResultSize", NotImplemented), ("XUserGetGamerPictureResult", NotImplemented),
        ("XUserAddByIdWithUiAsync", NotImplemented), ("XUserAddByIdWithUiResult", NotImplemented), ("XUserFindForWebAccount", NotImplemented),
        ("XUserGetMsaTokenSilentlyAsync", NotImplemented), ("XUserGetMsaTokenSilentlyResult", NotImplemented), ("XUserGetMsaTokenSilentlyResultSize", NotImplemented),
        ("XUserRegisterForDeviceAssociationChanged", NotImplemented), ("XUserUnregisterForDeviceAssociationChanged", Zero),
        ("XUserRegisterForDefaultAudioEndpointUtf16Changed", NotImplemented), ("XUserUnregisterForDefaultAudioEndpointUtf16Changed", Zero),
        ("XUserGetDefaultAudioEndpointUtf16", NotImplemented), ("XUserIsStoreUser", NotImplemented), ("XUserPlatformRemoteConnectSetEventHandlers", NotImplemented),
        ("XUserPlatformSpopPromptSetEventHandlers", NotImplemented), ("XUserPlatformUiSetEventHandlers", NotImplemented),

        // XStore.h (license, products, purchase UI)
        ("XStoreAcquireLicenseForPackageAsync", NotImplemented), ("XStoreAcquireLicenseForPackageResult", NotImplemented),
        ("XStoreAcquireLicenseForDurablesAsync", NotImplemented), ("XStoreAcquireLicenseForDurablesResult", NotImplemented),
        ("XStoreCanAcquireLicenseForPackageAsync", NotImplemented), ("XStoreCanAcquireLicenseForPackageResult", NotImplemented),
        ("XStoreCanAcquireLicenseForStoreIdAsync", NotImplemented), ("XStoreCanAcquireLicenseForStoreIdResult", NotImplemented),
        ("XStoreCloseLicenseHandle", Zero), ("XStoreIsLicenseValid", Zero),
        ("XStoreQueryAddOnLicensesAsync", NotImplemented), ("XStoreQueryAddOnLicensesResultCount", NotImplemented), ("XStoreQueryAddOnLicensesResult", NotImplemented),
        ("XStoreQueryAssociatedProductsAsync", NotImplemented), ("XStoreQueryAssociatedProductsResult", NotImplemented),
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
        ("XStoreRegisterGameLicenseChanged", NotImplemented), ("XStoreUnregisterGameLicenseChanged", Zero),
        ("XStoreRegisterPackageLicenseLost", NotImplemented), ("XStoreUnregisterPackageLicenseLost", Zero),
        ("XStoreQueryPackageIdentifier", NotImplemented), ("XStoreQueryGameAndDlcPackageUpdatesAsync", NotImplemented), ("XStoreQueryGameAndDlcPackageUpdatesResult", NotImplemented),
        ("XStoreQueryGameAndDlcPackageUpdatesResultCount", NotImplemented), ("XStoreDownloadPackageUpdatesAsync", NotImplemented), ("XStoreDownloadPackageUpdatesResult", NotImplemented),
        ("XStoreDownloadAndInstallPackageUpdatesAsync", NotImplemented), ("XStoreDownloadAndInstallPackageUpdatesResult", NotImplemented),
        ("XStoreDownloadAndInstallPackagesAsync", NotImplemented), ("XStoreDownloadAndInstallPackagesResult", NotImplemented), ("XStoreDownloadAndInstallPackagesResultCount", NotImplemented),
        ("XStoreQueryPackageUpdatesAsync", NotImplemented), ("XStoreQueryPackageUpdatesResult", NotImplemented), ("XStoreQueryPackageUpdatesResultCount", NotImplemented),

        // XGameUI.h
        ("XGameUiShowAchievementsAsync", NotImplemented), ("XGameUiShowAchievementsResult", NotImplemented),
        ("XGameUiShowMessageDialogAsync", NotImplemented), ("XGameUiShowMessageDialogResult", NotImplemented),
        ("XGameUiShowErrorDialogAsync", NotImplemented), ("XGameUiShowErrorDialogResult", NotImplemented),
        ("XGameUiShowTextEntryAsync", NotImplemented), ("XGameUiShowTextEntryResultSize", NotImplemented), ("XGameUiShowTextEntryResult", NotImplemented),
        ("XGameUiShowSendGameInviteAsync", NotImplemented), ("XGameUiShowSendGameInviteResult", NotImplemented),
        ("XGameUiShowMultiplayerActivityGameInviteAsync", NotImplemented), ("XGameUiShowMultiplayerActivityGameInviteResult", NotImplemented),
        ("XGameUiShowPlayerProfileCardAsync", NotImplemented), ("XGameUiShowPlayerProfileCardResult", NotImplemented),
        ("XGameUiShowPlayerPickerAsync", NotImplemented), ("XGameUiShowPlayerPickerResultCount", NotImplemented), ("XGameUiShowPlayerPickerResult", NotImplemented),
        ("XGameUiShowWebAuthenticationAsync", NotImplemented), ("XGameUiShowWebAuthenticationResultSize", NotImplemented), ("XGameUiShowWebAuthenticationResult", NotImplemented),
        ("XGameUiSetNotificationPositionHint", NotImplemented), ("XGameUiTextEntryOpen", NotImplemented), ("XGameUiTextEntryClose", NotImplemented),
        ("XGameUiTextEntryGetState", NotImplemented), ("XGameUiTextEntryGetExtents", NotImplemented), ("XGameUiTextEntryUpdatePositionHint", NotImplemented), ("XGameUiTextEntryUpdateVisibility", NotImplemented),

        // XLauncher.h, XGameProtocol.h, XGameInvite.h
        ("XLaunchUri", NotImplemented), ("XLaunchNewGame", Zero), ("XLaunchRestartOnCrash", NotImplemented),
        ("XGameProtocolRegisterForActivation", NotImplemented), ("XGameProtocolUnregisterForActivation", Zero),
        ("XGameInviteRegisterForEvent", NotImplemented), ("XGameInviteUnregisterForEvent", Zero),
        ("XGameInviteRegisterForPendingInvite", NotImplemented), ("XGameInviteUnregisterForPendingInvite", Zero),

        // XNetworking.h
        ("XNetworkingGetConnectivityHint", NotImplemented), ("XNetworkingRegisterConnectivityHintChanged", NotImplemented), ("XNetworkingUnregisterConnectivityHintChanged", Zero),
        ("XNetworkingQueryPreferredLocalUdpMultiplayerPort", NotImplemented), ("XNetworkingQueryPreferredLocalUdpMultiplayerPortAsync", NotImplemented),
        ("XNetworkingQueryPreferredLocalUdpMultiplayerPortAsyncResult", NotImplemented), ("XNetworkingRegisterPreferredLocalUdpMultiplayerPortChanged", NotImplemented),
        ("XNetworkingUnregisterPreferredLocalUdpMultiplayerPortChanged", Zero), ("XNetworkingQuerySecurityInformationForUrlAsync", NotImplemented),
        ("XNetworkingQuerySecurityInformationForUrlAsyncResult", NotImplemented), ("XNetworkingQuerySecurityInformationForUrlAsyncResultSize", NotImplemented),
        ("XNetworkingQuerySecurityInformationForUrlUtf16Async", NotImplemented), ("XNetworkingQuerySecurityInformationForUrlUtf16AsyncResult", NotImplemented),
        ("XNetworkingQuerySecurityInformationForUrlUtf16AsyncResultSize", NotImplemented), ("XNetworkingVerifyServerCertificate", NotImplemented),
        ("XNetworkingGetTitleTcpQueueingReservation", NotImplemented), ("XNetworkingGetTitleUdpQueueingReservation", NotImplemented),

        // XAppCapture.h, XDisplay.h, XAccessibility.h, XSpeechSynthesizer.h, XGameStreaming.h
        ("XAppCaptureTakeScreenshot", NotImplemented), ("XAppCaptureOpenScreenshotStream", NotImplemented), ("XAppCaptureReadScreenshotStream", NotImplemented),
        ("XAppCaptureCloseScreenshotStream", NotImplemented), ("XAppCaptureRecordTimespan", NotImplemented), ("XAppCaptureEnableRecord", Zero), ("XAppCaptureDisableRecord", Zero),
        ("XAppCaptureMetadataAddStringEvent", NotImplemented), ("XAppCaptureMetadataAddInt32Event", NotImplemented), ("XAppCaptureMetadataAddDoubleEvent", NotImplemented),
        ("XAppCaptureMetadataStartStringState", NotImplemented), ("XAppCaptureMetadataStartInt32State", NotImplemented), ("XAppCaptureMetadataStartDoubleState", NotImplemented),
        ("XAppCaptureMetadataStopState", NotImplemented), ("XAppCaptureMetadataStopAllStates", NotImplemented), ("XAppCaptureMetadataRemainingStorageBytesAvailable", NotImplemented),
        ("XAppCaptureRegisterMetadataPurged", NotImplemented), ("XAppCaptureUnRegisterMetadataPurged", Zero),
        ("XAppBroadcastGetStatus", NotImplemented), ("XAppBroadcastIsAppBroadcasting", Zero), ("XAppBroadcastShowUI", NotImplemented),
        ("XAppBroadcastRegisterIsAppBroadcastingChanged", NotImplemented), ("XAppBroadcastUnregisterIsAppBroadcastingChanged", Zero),
        ("XDisplayTryEnableHdrMode", Zero), ("XDisplayAcquireTimeoutDeferral", NotImplemented), ("XDisplayCloseTimeoutDeferralHandle", Zero),
        ("XAccessibilityGetClosedCaptionProperties", NotImplemented), ("XAccessibilityGetHighContrastMode", NotImplemented), ("XAccessibilityGetTextToSpeechProperties", NotImplemented),
        ("XClosedCaptionGetProperties", NotImplemented), ("XClosedCaptionSetEnabled", NotImplemented), ("XHighContrastGetMode", NotImplemented),
        ("XSpeechSynthesizerCreate", NotImplemented), ("XSpeechSynthesizerCloseHandle", NotImplemented), ("XSpeechToTextSendString", NotImplemented), ("XSpeechToTextSetPositionHint", NotImplemented),
        ("XSpeechToTextBeginHypothesisString", NotImplemented), ("XSpeechToTextUpdateHypothesisString", NotImplemented), ("XSpeechToTextFinalizeHypothesisString", NotImplemented),
        ("XSpeechToTextCancelHypothesisString", NotImplemented),
        ("XGameStreamingInitialize", NotImplemented), ("XGameStreamingUninitialize", Zero), ("XGameStreamingIsStreaming", Zero),
        ("XGameStreamingGetClientCount", Zero), ("XGameStreamingGetClients", NotImplemented), ("XGameStreamingRegisterConnectionStateChanged", NotImplemented),
        ("XGameStreamingUnregisterConnectionStateChanged", Zero), ("XGameStreamingGetStreamPhysicalDimensions", NotImplemented),

        // XPackage.h (the rest)
        ("XPackageEstimateDownloadSize", NotImplemented), ("XPackageGetWriteStats", NotImplemented), ("XPackageRegisterPackageInstalled", NotImplemented),
        ("XPackageUnregisterPackageInstalled", Zero), ("XPackageUninstallPackage", Zero), ("XPackageUninstallUWPInstance", NotImplemented),
        ("XPackageEnumerateFeatures", NotImplemented),
        ("XPackageInstallChunks", NotImplemented), ("XPackageInstallChunksAsync", NotImplemented), ("XPackageInstallChunksResult", NotImplemented),
        ("XPackageEnumerateChunkAvailability", NotImplemented), ("XPackageChangeChunkInstallOrder", NotImplemented),

        // XSystem.h and the rest
        ("XSystemGetAppSpecificDeviceId", NotImplemented), ("XSystemHandleTrack", NotImplemented), ("XSystemAllowFullDownloadBandwidth", NotImplemented),
        ("XGameGetXboxTitleIdUtf16", NotImplemented), ("XGameEventWriteUtf16", NotImplemented),
        ("XUserGetSignOutDeferralUtf16", NotImplemented),
        ("XGameSaveFilesGetFolderWithUiUtf16Async", NotImplemented), ("XGameSaveFilesGetFolderWithUiUtf16Result", NotImplemented),
    ];
}
