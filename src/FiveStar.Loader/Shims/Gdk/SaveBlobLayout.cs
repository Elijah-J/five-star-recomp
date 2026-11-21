// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text;

namespace FiveStar.Loader.Shims.Gdk;

/// <summary>Lays blobs out in the caller's buffer: XGameSaveBlob[count], then the names, then the data.</summary>
internal static unsafe class SaveBlobLayout
{
    public static nuint Size(IReadOnlyList<SaveBlobData> blobs)
    {
        long total = (long)blobs.Count * SaveLayout.BlobSize;

        foreach (var blob in blobs)
        {
            total += AlignedNameSize(blob.Name) + blob.Data!.Length;
        }

        return (nuint)total;
    }

    public static void Write(IReadOnlyList<SaveBlobData> blobs, byte* buffer)
    {
        byte* tail = buffer + (blobs.Count * SaveLayout.BlobSize);

        for (int i = 0; i < blobs.Count; i++)
        {
            byte* entry = buffer + (i * SaveLayout.BlobSize);
            var name = Encoding.UTF8.GetBytes(blobs[i].Name);
            name.CopyTo(new Span<byte>(tail, name.Length));
            tail[name.Length] = 0;
            *(nint*)(entry + SaveLayout.BlobNameOffset) = (nint)tail;
            *(ulong*)(entry + SaveLayout.BlobSizeOffset) = (uint)blobs[i].Data!.Length;

            tail += AlignedNameSize(blobs[i].Name);
        }

        for (int i = 0; i < blobs.Count; i++)
        {
            byte* entry = buffer + (i * SaveLayout.BlobSize);
            var data = blobs[i].Data!;
            data.CopyTo(new Span<byte>(tail, data.Length));
            *(nint*)(entry + SaveLayout.BlobDataOffset) = (nint)tail;
            tail += data.Length;
        }
    }

    private static int AlignedNameSize(string name)
    {
        int size = Encoding.UTF8.GetByteCount(name) + 1;

        return (size + SaveLayout.NameAlignment - 1) & ~(SaveLayout.NameAlignment - 1);
    }
}
