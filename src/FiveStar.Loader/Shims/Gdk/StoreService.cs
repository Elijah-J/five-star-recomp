// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text;
using FiveStar.Loader.Native;

namespace FiveStar.Loader.Shims.Gdk;

/// <summary>
/// XStore.h: store contexts and an always active, full (non-trial) game license. The rest of XStore is in the
/// not-implemented catalog.
/// </summary>
internal sealed unsafe class StoreService
{
    public const string GameLicenseApi = "XStoreQueryGameLicenseAsync";

    /// <summary>
    /// struct XStoreGameLicense { char skuStoreId[18]; bool isActive; bool isTrialOwnedByThisUser; bool isDiscLicense;
    /// bool isTrial; uint32_t trialTimeRemainingInSeconds; char trialUniqueId[64]; time_t expirationDate; }: 104 bytes.
    /// The member list is unconfirmed.
    /// </summary>
    public const int GameLicenseSize = 104;

    public const int IsActiveOffset = 18;
    public const int IsTrialOffset = 21;
    private const int SkuCapacity = 17;
    private const int ExpirationOffset = 96;
    private const string SkuSuffix = "/0010";

    private readonly HandleTable<object> _contexts = new("XSTORCTX");
    private readonly GdkConfig _config;

    public StoreService(GdkConfig config)
    {
        _config = config;
    }

    public int CreateContext(nint* context)
    {
        if (context == null)
        {
            return HResult.Pointer;
        }

        *context = _contexts.Add(new object());

        return HResult.Ok;
    }

    public void CloseContext(nint context)
    {
        _contexts.Remove(context);
    }

    public bool IsContext(nint context)
    {
        return _contexts.Get(context) is not null;
    }

    /// <summary>Writes the license: the store id with "/0010" as SKU, active, never expiring.</summary>
    public void WriteGameLicense(byte* license)
    {
        new Span<byte>(license, GameLicenseSize).Clear();
        string storeId = _config.Identity.StoreId;
        string sku = (storeId + SkuSuffix)[..Math.Min(SkuCapacity, storeId.Length + SkuSuffix.Length)];
        var bytes = Encoding.ASCII.GetBytes(sku);
        bytes.CopyTo(new Span<byte>(license, Math.Min(SkuCapacity, bytes.Length)));

        license[IsActiveOffset] = GdkModule.True;
        *(long*)(license + ExpirationOffset) = long.MaxValue;
    }
}
