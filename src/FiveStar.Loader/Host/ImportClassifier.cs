// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using FiveStar.Loader.Shims;

namespace FiveStar.Loader.Host;

/// <summary>
/// Classifies an import the way <see cref="Loading.ImportResolver"/> would, using the same <see cref="ShimTable"/>
/// lookups, but without mapping any image: shim, Xbox-only stub, or left to the PC host. This is the resolution the
/// <c>fivestar exe analyze</c> report reads, so the import census and the loader never keep two copies of the list.
/// </summary>
internal sealed class ImportClassifier
{
    private const string HostDetail = "no loader shim; resolved from the PC host (System32) or unresolved";

    private readonly ShimTable _shims;

    public ImportClassifier(ShimTable shims)
    {
        _shims = shims;
    }

    public ImportClassification Classify(string module, string? name, uint ordinal)
    {
        var entry = _shims.TryResolve(module, name, ordinal);

        if (entry is not null)
        {
            return new ImportClassification(ImportClassKind.Shim, $"{entry.Method.DeclaringType!.Name}.{entry.Method.Name}");
        }

        var fallback = _shims.TryFallback(module, name, ordinal);

        if (fallback is null)
        {
            return new ImportClassification(ImportClassKind.Host, HostDetail);
        }

        var value = fallback.ReturnValue.ToString("x", CultureInfo.InvariantCulture);

        if (fallback.NotImplemented)
        {
            return new ImportClassification(ImportClassKind.XboxStub, $"not implemented, returns 0x{value}");
        }

        return new ImportClassification(ImportClassKind.XboxStub, $"Xbox-only module default, returns 0x{value}");
    }
}
