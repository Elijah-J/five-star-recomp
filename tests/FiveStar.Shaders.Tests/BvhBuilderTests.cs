// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Numerics;
using FiveStar.Shaders.Bvh;
using FiveStar.Shaders.Tests.Reference;
using Xunit;

namespace FiveStar.Shaders.Tests;

/// <summary>
/// The CPU BVH4 builder: a ray walking the built nodes with the RT IP 1.1 reference model of image_bvh_intersect_ray
/// finds the same closest triangle, at the same distance, as testing every triangle with that model.
/// </summary>
public sealed class BvhBuilderTests
{
    private const int TriangleCount = 500;
    private const int RayCount = 400;
    private const float Extent = 1000f;

    [Fact]
    public void Traversal_of_a_built_bvh_finds_the_closest_triangle()
    {
        var random = new Random(1234);
        var triangles = Enumerable.Range(0, TriangleCount).Select(i => RandomTriangle(random, (uint)i)).ToList();
        var image = Bvh4Builder.Build(triangles, 0);
        var memory = Load(image.Nodes);
        var descriptor = BvhReference.Descriptor(0, sort: false, grow: 0, returnBarycentrics: false);
        int hits = 0;

        for (int i = 0; i < RayCount; i++)
        {
            var target = triangles[random.Next(TriangleCount)];
            var ray = RayTowards(random, (target.V0 + target.V1 + target.V2) / 3f);
            var walked = Walk(memory, descriptor, image.Root, ray);
            var brute = BruteForce(triangles, ray);

            Assert.Equal(brute, walked);

            if (brute.Primitive != uint.MaxValue)
            {
                hits++;
            }
        }

        Assert.True(hits > RayCount / 2, $"only {hits} rays hit");
        Assert.Equal(TriangleCount, image.TriangleNodes);
    }

    private static (uint Primitive, uint Distance) Walk(BvhMemory memory, uint[] descriptor, uint root, BvhRay ray)
    {
        var best = (Primitive: uint.MaxValue, Distance: float.PositiveInfinity);
        var stack = new Stack<uint>([root]);

        while (stack.Count > 0)
        {
            uint node = stack.Pop();
            var result = BvhReference.Intersect(memory, descriptor, node, ray);

            if ((node & 7) == AmdBvhLayout.Box32Type)
            {
                foreach (uint child in result.Where(child => child != BvhReference.Invalid))
                {
                    stack.Push(child);
                }

                continue;
            }

            uint primitive = memory.ReadWord((int)((node & ~7u) << 3) + 52);
            best = Closer(best, result, primitive);
        }

        return (best.Primitive, BitConverter.SingleToUInt32Bits(best.Distance));
    }

    private static (uint Primitive, uint Distance) BruteForce(List<BvhTriangle> triangles, BvhRay ray)
    {
        var best = (Primitive: uint.MaxValue, Distance: float.PositiveInfinity);
        var descriptor = BvhReference.Descriptor(0, sort: false, grow: 0, returnBarycentrics: false);

        foreach (var triangle in triangles)
        {
            var single = new BvhMemory(64);
            single.WriteTriangle(0, [Vertex(triangle.V0), Vertex(triangle.V1), Vertex(triangle.V2)], 9);
            best = Closer(best, BvhReference.Intersect(single, descriptor, 0, ray), triangle.PrimitiveIndex);
        }

        return (best.Primitive, BitConverter.SingleToUInt32Bits(best.Distance));
    }

    /// <summary>Return mode 0: t_num, t_denom, triangle_id, hit. Ties keep the lower primitive index.</summary>
    private static (uint Primitive, float Distance) Closer((uint Primitive, float Distance) best, uint[] result, uint primitive)
    {
        if (result[3] == 0)
        {
            return best;
        }

        float t = BitConverter.UInt32BitsToSingle(result[0]) / BitConverter.UInt32BitsToSingle(result[1]);
        bool closer = t < best.Distance || (t == best.Distance && primitive < best.Primitive);

        if (t > Extent || !closer)
        {
            return best;
        }

        return (primitive, t);
    }

    private static BvhMemory Load(byte[] nodes)
    {
        var memory = new BvhMemory(nodes.Length);

        for (int at = 0; at < nodes.Length; at += 4)
        {
            memory.WriteWord(at, BitConverter.ToUInt32(nodes, at));
        }

        return memory;
    }

    private static BvhTriangle RandomTriangle(Random random, uint index)
    {
        var center = new Vector3(Next(random, -50, 50), Next(random, -50, 50), Next(random, -50, 50));

        return new BvhTriangle(Corner(random, center), Corner(random, center), Corner(random, center), 0, index, 0);
    }

    private static Vector3 Corner(Random random, Vector3 center)
    {
        return center + new Vector3(Next(random, -4, 4), Next(random, -4, 4), Next(random, -4, 4));
    }

    private static BvhRay RayTowards(Random random, Vector3 target)
    {
        var origin = new Vector3(Next(random, -80, 80), Next(random, -80, 80), Next(random, -80, 80));
        var direction = Vector3.Normalize(target - origin);
        var inverse = new Vector3(1f / direction.X, 1f / direction.Y, 1f / direction.Z);

        return new BvhRay(Extent, Vertex(origin), Vertex(direction), Vertex(inverse));
    }

    private static double[] Vertex(Vector3 value)
    {
        return [value.X, value.Y, value.Z];
    }

    private static float Next(Random random, float low, float high)
    {
        return low + ((high - low) * random.NextSingle());
    }
}
