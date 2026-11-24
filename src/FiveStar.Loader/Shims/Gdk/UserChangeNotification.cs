// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

namespace FiveStar.Loader.Shims.Gdk;

/// <summary>One call of an XUserChangeEventCallback(void* context, XUserLocalId user, XUserChangeEvent event) on its queue.</summary>
internal sealed unsafe class UserChangeNotification : AsyncStep
{
    private readonly nint _callback;
    private readonly nint _context;
    private readonly ulong _localId;
    private readonly uint _change;

    public UserChangeNotification(nint callback, nint context, ulong localId, uint change)
    {
        _callback = callback;
        _context = context;
        _localId = localId;
        _change = change;
    }

    public override void Run(bool canceled)
    {
        if (!canceled)
        {
            ((delegate* unmanaged<nint, ulong, uint, void>)_callback)(_context, _localId, _change);
        }
    }
}
