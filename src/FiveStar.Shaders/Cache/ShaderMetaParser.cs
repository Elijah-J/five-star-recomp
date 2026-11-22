// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Buffers.Binary;

namespace FiveStar.Shaders.Cache;

/// <summary>
/// Reads a shader's metadata entry: a header (0x10 bytes for version 2, 0x18 for 0x403), 32-byte entries tagged 0x41
/// or 0x42, and a last entry tag 0x43 with a 16-byte hash.
/// </summary>
public static class ShaderMetaParser
{
    /// <summary>Header version of graphics shaders.</summary>
    public const uint GraphicsVersion = 2;

    /// <summary>Header version of compute shaders.</summary>
    public const uint ComputeVersion = 0x403;

    private const int GraphicsHeader = 0x10;
    private const int ComputeHeader = 0x18;
    private const int EntrySize = 0x20;
    private const uint UserDataTag = 0x41;
    private const uint ResourceTag = 0x42;
    private const uint HashTag = 0x43;
    private const int HashSize = 16;

    // Entry fields after the tag.
    private const int KindField = 4;
    private const int OffsetField = 8;
    private const int RegisterField = 12;
    private const int ValueField = 16;
    private const int LayoutField = 20;

    /// <summary>The entries of the metadata, or null when it is not a shader's metadata.</summary>
    public static ShaderMeta? Parse(ReadOnlySpan<byte> meta)
    {
        if (meta.Length < ComputeHeader)
        {
            return null;
        }

        var version = BinaryPrimitives.ReadUInt32LittleEndian(meta);
        int position;

        if (version == GraphicsVersion)
        {
            position = GraphicsHeader;
        }
        else if (version == ComputeVersion)
        {
            position = ComputeHeader;
        }
        else
        {
            return null;
        }

        return ReadEntries(meta, version, position);
    }

    private static ShaderMeta? ReadEntries(ReadOnlySpan<byte> meta, uint version, int position)
    {
        var userData = new List<UserDataEntry>();
        var resources = new List<ResourceEntry>();

        while (position + sizeof(uint) <= meta.Length)
        {
            var entry = meta[position..];
            var tag = BinaryPrimitives.ReadUInt32LittleEndian(entry);

            if (tag == HashTag && entry.Length >= sizeof(uint) + HashSize)
            {
                var hash = Convert.ToHexStringLower(entry.Slice(sizeof(uint), HashSize));

                return new ShaderMeta(version, userData, resources, hash);
            }

            if (entry.Length < EntrySize)
            {
                return null;
            }

            if (tag == UserDataTag)
            {
                userData.Add(new UserDataEntry(U32(entry, KindField), U32(entry, OffsetField), U32(entry, RegisterField), U32(entry, ValueField), U32(entry, LayoutField)));
            }
            else if (tag == ResourceTag)
            {
                resources.Add(new ResourceEntry(U32(entry, OffsetField), U32(entry, RegisterField), U32(entry, ValueField)));
            }
            else
            {
                return null;
            }

            position += EntrySize;
        }

        return null;
    }

    private static uint U32(ReadOnlySpan<byte> entry, int field)
    {
        return BinaryPrimitives.ReadUInt32LittleEndian(entry[field..]);
    }
}
