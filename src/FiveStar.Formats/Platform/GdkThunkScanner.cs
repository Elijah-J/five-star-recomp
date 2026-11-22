// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Buffers.Binary;

namespace FiveStar.Formats.Platform;

/// <summary>
/// Finds the GDK thunks compiled into a module: a <c>call</c> preceded by <c>lea rcx, [clsid]</c> and
/// <c>lea rdx, [iid]</c> (or <c>mov rdx, rcx</c>), followed by a vtable call. Only class ids from the library count,
/// which keeps byte matches inside other instructions out.
/// </summary>
public static class GdkThunkScanner
{
    private const string CodeSectionPrefix = ".text";
    private const byte CallRel32 = 0xE8;
    private const int CallLength = 5;
    private const int GuidSize = 16;

    // The GUID loads sit within a few instructions of the query call; the vtable call follows within ~40 instructions.
    private const int LookBack = 40;
    private const int LookAhead = 192;

    /// <summary>The distinct (clsid, iid, slot) thunks of a module, unnamed.</summary>
    public static IReadOnlyList<GdkThunk> Scan(PeImage image, IReadOnlySet<Guid> knownClasses)
    {
        var found = new HashSet<GdkThunk>();

        foreach (var section in image.Sections.Where(section => section.Name.StartsWith(CodeSectionPrefix, StringComparison.Ordinal)))
        {
            var start = (int)image.RawStart(section);
            var length = (int)Math.Min(section.RawSize, section.VirtualSize);
            var code = image.Data.AsSpan(start, Math.Min(length, image.Data.Length - start));

            for (var at = 0; at + CallLength <= code.Length; at++)
            {
                if (code[at] != CallRel32)
                {
                    continue;
                }

                var thunk = ThunkAt(image, section, code, at, knownClasses);

                if (thunk is not null)
                {
                    found.Add(thunk);
                }
            }
        }

        return [.. found];
    }

    private static GdkThunk? ThunkAt(PeImage image, PeSection section, ReadOnlySpan<byte> code, int call, IReadOnlySet<Guid> knownClasses)
    {
        Guid? clsid = null;
        Guid? iid = null;
        var first = call;

        // Nearest lea per register, walking back from the call.
        for (var lea = call - GdkThunkCode.LeaLength; lea >= Math.Max(0, call - LookBack); lea--)
        {
            var target = GdkThunkCode.LeaTarget(code, lea);

            if (target == GdkRegister.Rcx && clsid is null)
            {
                clsid = GuidAt(image, section, code, lea);
                first = lea;
            }
            else if (target == GdkRegister.Rdx && iid is null)
            {
                iid = GuidAt(image, section, code, lea);
            }
        }

        if (clsid is null || !knownClasses.Contains(clsid.Value))
        {
            return null;
        }

        if (iid is null && GdkThunkCode.CopiesRcxToRdx(code, first, call))
        {
            iid = clsid;
        }

        var slot = GdkThunkCode.FirstApiSlot(code, call + CallLength, call + CallLength + LookAhead);

        if (iid is null || slot is null)
        {
            return null;
        }

        return new GdkThunk(clsid.Value, iid.Value, slot.Value, null);
    }

    private static Guid? GuidAt(PeImage image, PeSection section, ReadOnlySpan<byte> code, int lea)
    {
        var displacement = BinaryPrimitives.ReadInt32LittleEndian(code[(lea + GdkThunkCode.LeaDisplacement)..]);
        var rva = (uint)(section.VirtualAddress + lea + GdkThunkCode.LeaLength + displacement);
        var offset = image.Offset(rva);

        if (offset is null || offset.Value + GuidSize > image.Data.Length)
        {
            return null;
        }

        return new Guid(image.Data.AsSpan((int)offset.Value, GuidSize));
    }
}
