// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Runtime.InteropServices;
using FiveStar.Loader.Codegen;
using FiveStar.Loader.Diagnostics;
using FiveStar.Loader.Native;

namespace FiveStar.Loader.Loading;

/// <summary>
/// SetLastError for shims. A value set inside an [UnmanagedCallersOnly] method does not survive the return to native
/// code: the runtime's reverse-P/Invoke epilogue calls Win32 functions (TlsGetValue) that reset it. Shims marked
/// <c>SetsLastError</c> are therefore entered through a generated wrapper that calls the managed method and then,
/// when the shim asked for it, writes TEB.LastErrorValue itself. The requested value travels in a TLS slot that the
/// wrapper reads straight from TEB.TlsSlots, so it calls nothing. Shims with up to 4 integer arguments are supported;
/// they pass through untouched.
/// </summary>
internal sealed class LastErrorBridge : IDisposable
{
    /// <summary>The wrapper's frame: 0x20 bytes of home space for the callee, 8 to keep rax, 8 to realign rsp.</summary>
    private const byte FrameSize = 0x28;
    private const byte SavedRaxOffset = 0x20;

    private readonly CodeHeap _heap;
    private readonly LoaderLog _log;
    private readonly uint _slot;

    public LastErrorBridge(CodeHeap heap, LoaderLog log)
    {
        _heap = heap;
        _log = log;
        _slot = Kernel32.TlsAlloc();
    }

    /// <summary>TEB.TlsSlots holds 64 entries; a higher slot lives in the expansion array, which the wrapper cannot read.</summary>
    public bool Available => _slot < ThreadEnvironmentBlock.TlsSlotCount;

    /// <summary>Requests <paramref name="error"/> as the last error the native caller of the current shim sees.</summary>
    public void Set(int error)
    {
        if (!Available)
        {
            Marshal.SetLastSystemError(error);

            return;
        }

        // Stored plus one, so 0 means "no request".
        _ = Kernel32.TlsSetValue(_slot, (nint)(uint)error + 1);
    }

    /// <summary>Returns the entry point that calls <paramref name="target"/> and then applies a requested last error.</summary>
    public nint Wrap(nint target)
    {
        if (!Available)
        {
            return target;
        }

        int slotOffset = ThreadEnvironmentBlock.TlsSlots + (sizeof(ulong) * (int)_slot);
        var code = new X64Emitter();
        code.SubRsp(FrameSize);
        byte prologSize = (byte)code.Length;

        // Call the shim; rcx, rdx, r8 and r9 are untouched.
        code.MovRax((ulong)target);
        code.CallRax();
        code.StoreRaxToStack(SavedRaxOffset);

        // Skip ahead unless the shim requested an error.
        code.LoadRcxFromGs(slotOffset);
        code.TestRcxRcx();
        int skip = code.JzForward();

        // TEB.LastErrorValue = request - 1, then consume the request.
        code.DecRcx();
        code.StoreEcxToGs(ThreadEnvironmentBlock.LastErrorValue);
        code.StoreQwordToGs(slotOffset, 0);
        code.PatchJumpHere(skip);

        code.LoadRaxFromStack(SavedRaxOffset);
        code.AddRsp(FrameSize);
        code.Ret();

        return FramedFunction.Commit(_heap, code, prologSize, FrameSize, _log, nameof(LastErrorBridge));
    }

    public void Dispose()
    {
        _ = Kernel32.TlsFree(_slot);
    }
}
