// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Runtime.InteropServices;

namespace FiveStar.Loader.Shims.Gdk;

/// <summary>
/// XAccessibility.h entry points and the voice list of XSpeechSynthesizer.h; the work is in
/// <see cref="AccessibilityService"/>. Synthesis is in <see cref="XSpeechSynthesizerShim"/>; speech-to-text stays in
/// the not-implemented catalog.
/// </summary>
internal static unsafe partial class XAccessibilityShim
{
    [Shim(GdkModule.Name, "XClosedCaptionGetProperties")]
    [UnmanagedCallersOnly]
    private static int XClosedCaptionGetProperties(byte* properties)
    {
        return ShimContext.Runtime.Gdk.Accessibility.GetClosedCaptionProperties(properties);
    }

    [Shim(GdkModule.Name, "XClosedCaptionSetEnabled")]
    [UnmanagedCallersOnly]
    private static int XClosedCaptionSetEnabled(byte enabled)
    {
        return ShimContext.Runtime.Gdk.Accessibility.SetClosedCaptionsEnabled(enabled != 0);
    }

    [Shim(GdkModule.Name, "XHighContrastGetMode")]
    [UnmanagedCallersOnly]
    private static int XHighContrastGetMode(uint* mode)
    {
        return AccessibilityService.GetHighContrastMode(mode);
    }

    /// <summary>HRESULT XSpeechSynthesizerEnumerateInstalledVoices(void* context, XSpeechSynthesizerInstalledVoicesCallback*).</summary>
    [Shim(GdkModule.Name, "XSpeechSynthesizerEnumerateInstalledVoices")]
    [UnmanagedCallersOnly]
    private static int XSpeechSynthesizerEnumerateInstalledVoices(nint context, nint callback)
    {
        return AccessibilityService.EnumerateInstalledVoices(callback);
    }
}
