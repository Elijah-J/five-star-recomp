// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Collections.Concurrent;
using FiveStar.Loader.Diagnostics;

namespace FiveStar.Loader.Shims.Network;

/// <summary>
/// The record of blocked outbound attempts: every WinHTTP connection, Winsock connect, send or name lookup and
/// libHttpClient request fails inside the process, and each distinct (function, target) is logged on its first attempt.
/// </summary>
internal sealed class NetworkSeal
{
    private readonly ConcurrentDictionary<string, int> _attempts = new(StringComparer.Ordinal);
    private readonly LoaderLog _log;

    public NetworkSeal(LoaderLog log)
    {
        _log = log;
    }

    /// <summary>Blocked attempts so far, by "function target".</summary>
    public IReadOnlyDictionary<string, int> Attempts => _attempts;

    public void Block(string api, string target)
    {
        string key = $"{api} {target}";
        int count = _attempts.AddOrUpdate(key, 1, (_, previous) => previous + 1);

        if (count == 1)
        {
            _log.Info($"  [network] blocked {key}");
        }
    }
}
