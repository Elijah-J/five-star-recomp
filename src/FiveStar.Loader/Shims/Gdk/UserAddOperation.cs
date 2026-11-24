// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using FiveStar.Loader.Native;

namespace FiveStar.Loader.Shims.Gdk;

/// <summary>XUserAddAsync: always adds the offline user; the result is its XUserHandle.</summary>
internal sealed class UserAddOperation : ManagedAsyncOperation
{
    private readonly UserService _users;
    private readonly TaskQueueService _taskQueues;
    private readonly NativeBoundary _boundary;
    private readonly uint _options;

    public UserAddOperation(UserService users, TaskQueueService taskQueues, NativeBoundary boundary, uint options)
    {
        _users = users;
        _taskQueues = taskQueues;
        _boundary = boundary;
        _options = options;
    }

    public override AsyncWorkResult Work()
    {
        _boundary.Info($"XUserAddAsync(options 0x{_options:x}) -> offline user \"{UserService.Gamertag}\" xuid 0x{UserService.Xuid:x}");

        return new AsyncWorkResult(HResult.Ok, (nuint)sizeof(long));
    }

    public override unsafe int Write(byte* buffer, nuint size)
    {
        *(nint*)buffer = _users.Reference();

        _users.Added(_taskQueues, _boundary);

        return HResult.Ok;
    }
}
