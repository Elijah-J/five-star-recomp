// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Numerics;

namespace FiveStar.Formats.Models;

/// <summary>
/// Reads the skeleton of a drawable root (+0x18): +0x28 pointer to the bones, +0x6C u16 bone count. A bone is 0x50
/// bytes: +0x00 rotation quaternion (x, y, z, w), +0x10 translation, +0x20 scale, +0x30 name pointer, +0x44 s16 next
/// sibling, +0x46 s16 parent (-1 for the root). Transforms are relative to the parent.
/// </summary>
public static class SkeletonReader
{
    private const int SkeletonPointer = 0x18;
    private const int BonesPointer = 0x28;
    private const int BoneCount = 0x6C;
    private const int BoneBytes = 0x50;
    private const int TranslationOffset = 0x10;
    private const int ScaleOffset = 0x20;
    private const int NamePointer = 0x30;
    private const int ParentOffset = 0x46;
    private const int MaxName = 128;
    private const int VectorY = 4;
    private const int VectorZ = 8;
    private const int VectorW = 12;

    public static Skeleton Read(ResourceView view, int root)
    {
        var skeleton = view.Follow(root + SkeletonPointer);

        if (!skeleton.HasValue)
        {
            return Skeleton.Empty;
        }

        var bones = view.Follow(skeleton.Value + BonesPointer);

        if (!bones.HasValue)
        {
            return Skeleton.Empty;
        }

        var count = view.U16(skeleton.Value + BoneCount);
        var list = new List<Bone>(count);

        for (var index = 0; index < count; index++)
        {
            list.Add(ReadBone(view, bones.Value + (index * BoneBytes)));
        }

        return new Skeleton(list);
    }

    private static Bone ReadBone(ResourceView view, int bone)
    {
        var name = string.Empty;
        var text = view.Follow(bone + NamePointer);

        if (text.HasValue)
        {
            name = view.Text(text.Value, MaxName) ?? string.Empty;
        }

        var rotation = new Quaternion(view.F32(bone), view.F32(bone + VectorY), view.F32(bone + VectorZ), view.F32(bone + VectorW));
        var parent = (short)view.U16(bone + ParentOffset);

        return new Bone(name, parent, rotation, Vector(view, bone + TranslationOffset), Vector(view, bone + ScaleOffset));
    }

    private static Vector3 Vector(ResourceView view, int at)
    {
        return new Vector3(view.F32(at), view.F32(at + VectorY), view.F32(at + VectorZ));
    }
}
