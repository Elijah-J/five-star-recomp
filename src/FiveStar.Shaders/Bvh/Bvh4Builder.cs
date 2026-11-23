// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

namespace FiveStar.Shaders.Bvh;

/// <summary>
/// Builds a BLAS on the CPU in the node layout the translated shaders read (<see cref="AmdBvhLayout"/>): box32
/// internal nodes with exact f32 bounds, one triangle per type-0 triangle node. For geometry the console builds at run
/// time (dynamic BLASes); the header that places the nodes in a console acceleration structure is the caller's.
/// </summary>
public static class Bvh4Builder
{
    /// <param name="triangles">The triangles, vertices already transformed.</param>
    /// <param name="nodeOffset">Byte offset of the first node from the BVH base; a multiple of 64.</param>
    public static Bvh4Image Build(IReadOnlyList<BvhTriangle> triangles, int nodeOffset)
    {
        if (nodeOffset % AmdBvhLayout.NodeAlignment != 0)
        {
            throw new ArgumentException($"node offset {nodeOffset} is not 64-byte aligned", nameof(nodeOffset));
        }

        if (triangles.Count == 0)
        {
            return new Bvh4Image([], AmdBvhLayout.InvalidNode, 0, 0);
        }

        var tree = new Bvh4Tree(triangles);
        tree.Build();

        return Write(tree, nodeOffset);
    }

    private static Bvh4Image Write(Bvh4Tree tree, int nodeOffset)
    {
        int boxBytes = tree.Boxes.Count * AmdBvhLayout.BoxNodeSize;
        var nodes = new byte[boxBytes + (tree.Order.Length * AmdBvhLayout.TriangleNodeSize)];
        Span<uint> pointers = stackalloc uint[AmdBvhLayout.Children];
        Span<BvhBounds> bounds = stackalloc BvhBounds[AmdBvhLayout.Children];

        for (int box = 0; box < tree.Boxes.Count; box++)
        {
            var children = tree.Boxes[box];

            for (int child = 0; child < children.Length; child++)
            {
                pointers[child] = ChildPointer(children[child], nodeOffset, boxBytes);
                bounds[child] = children[child].Bounds;
            }

            var node = nodes.AsSpan(box * AmdBvhLayout.BoxNodeSize, AmdBvhLayout.BoxNodeSize);
            AmdBvhLayout.WriteBox32(node, pointers[..children.Length], bounds[..children.Length]);
        }

        for (int position = 0; position < tree.Order.Length; position++)
        {
            var node = nodes.AsSpan(boxBytes + (position * AmdBvhLayout.TriangleNodeSize), AmdBvhLayout.TriangleNodeSize);
            AmdBvhLayout.WriteTriangle(node, tree.Triangle(position));
        }

        uint root = AmdBvhLayout.NodePointer(nodeOffset, AmdBvhLayout.Box32Type);

        return new Bvh4Image(nodes, root, tree.Boxes.Count, tree.Order.Length);
    }

    private static uint ChildPointer(Bvh4Child child, int nodeOffset, int boxBytes)
    {
        if (child.IsTriangle)
        {
            return AmdBvhLayout.NodePointer(nodeOffset + boxBytes + (child.Index * AmdBvhLayout.TriangleNodeSize), AmdBvhLayout.TriangleType);
        }

        return AmdBvhLayout.NodePointer(nodeOffset + (child.Index * AmdBvhLayout.BoxNodeSize), AmdBvhLayout.Box32Type);
    }
}
