// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Numerics;

namespace FiveStar.Shaders.Bvh;

/// <summary>
/// A 4-wide hierarchy over triangles, split top-down: each box sorts its triangles by centroid along the longest axis
/// of their centroid bounds and hands each quarter to a child. Boxes are numbered in preorder (the root is 0); a
/// triangle child is a position in <see cref="Order"/>.
/// </summary>
internal sealed class Bvh4Tree
{
    private const float Half = 0.5f;
    private const int AxisX = 0;
    private const int AxisY = 1;
    private const int AxisZ = 2;

    private readonly IReadOnlyList<BvhTriangle> _triangles;
    private readonly BvhBounds[] _bounds;
    private readonly Vector3[] _centroids;
    private readonly float[] _keys;
    private readonly List<Bvh4Child[]> _boxes = [];

    public Bvh4Tree(IReadOnlyList<BvhTriangle> triangles)
    {
        _triangles = triangles;
        _bounds = new BvhBounds[triangles.Count];
        _centroids = new Vector3[triangles.Count];
        _keys = new float[triangles.Count];
        Order = new int[triangles.Count];

        for (int i = 0; i < triangles.Count; i++)
        {
            _bounds[i] = BvhBounds.Of(triangles[i]);
            _centroids[i] = (_bounds[i].Min + _bounds[i].Max) * Half;
            Order[i] = i;
        }
    }

    /// <summary>Triangle indices in leaf order.</summary>
    public int[] Order { get; }

    /// <summary>The children of each box, by box index.</summary>
    public IReadOnlyList<Bvh4Child[]> Boxes => _boxes;

    public BvhTriangle Triangle(int position)
    {
        return _triangles[Order[position]];
    }

    /// <summary>Builds the boxes over every triangle; there must be at least one.</summary>
    public void Build()
    {
        Subdivide(0, Order.Length);
    }

    private int Subdivide(int start, int count)
    {
        int index = _boxes.Count;
        int chunks = Math.Min(AmdBvhLayout.Children, count);
        var children = new Bvh4Child[chunks];
        _boxes.Add(children);
        SortAlongLongestAxis(start, count);

        int begin = start;

        for (int chunk = 0; chunk < chunks; chunk++)
        {
            int size = (count * (chunk + 1) / chunks) - (count * chunk / chunks);
            var bounds = BoundsOf(begin, size);

            if (size == 1)
            {
                children[chunk] = new Bvh4Child(true, begin, bounds);
            }
            else
            {
                children[chunk] = new Bvh4Child(false, Subdivide(begin, size), bounds);
            }

            begin += size;
        }

        return index;
    }

    private void SortAlongLongestAxis(int start, int count)
    {
        var low = new Vector3(float.PositiveInfinity);
        var high = new Vector3(float.NegativeInfinity);

        for (int i = start; i < start + count; i++)
        {
            low = Vector3.Min(low, _centroids[Order[i]]);
            high = Vector3.Max(high, _centroids[Order[i]]);
        }

        int axis = LongestAxis(high - low);

        for (int i = start; i < start + count; i++)
        {
            _keys[i] = _centroids[Order[i]][axis];
        }

        Array.Sort(_keys, Order, start, count);
    }

    private static int LongestAxis(Vector3 extent)
    {
        if (extent.X >= extent.Y && extent.X >= extent.Z)
        {
            return AxisX;
        }

        if (extent.Y >= extent.Z)
        {
            return AxisY;
        }

        return AxisZ;
    }

    private BvhBounds BoundsOf(int start, int count)
    {
        var bounds = BvhBounds.Empty;

        for (int i = start; i < start + count; i++)
        {
            bounds = bounds.Union(_bounds[Order[i]]);
        }

        return bounds;
    }
}
