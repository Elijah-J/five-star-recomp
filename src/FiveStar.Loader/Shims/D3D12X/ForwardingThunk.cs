// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using FiveStar.Loader.Codegen;

namespace FiveStar.Loader.Shims.D3D12X;

/// <summary>Tail-call thunks from a wrapper vtable slot to the same slot of the desktop object the wrapper holds.</summary>
internal static class ForwardingThunk
{
    /// <summary>
    /// <c>mov rcx,[rcx+inner]; mov rax,[rcx]; jmp [rax+slot*8]</c>, or for a struct-returning slot (object in rdx)
    /// <c>mov rdx,[rdx+inner]; mov rax,[rdx]; xchg rcx,rdx; jmp [rax+slot*8]</c>. Stack arguments stay in place.
    /// </summary>
    public static nint Emit(CodeHeap heap, int slot, bool structReturn)
    {
        var emitter = new X64Emitter();

        if (structReturn)
        {
            emitter.LoadRdxFromRdx(D3D12XLayout.InnerOffset);
            emitter.LoadRaxFromRdx();
            emitter.SwapRcxRdx();
        }
        else
        {
            emitter.LoadRcxFromRcx(D3D12XLayout.InnerOffset);
            emitter.LoadRaxFromRcx();
        }

        emitter.JmpRaxIndirect(slot * IntPtr.Size);

        return heap.Write(emitter.ToArray());
    }
}
