// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Buffers.Binary;
using System.Numerics;
using FiveStar.Formats.Models;

namespace FiveStar.Cli.Graphics;

/// <summary>
/// Turns decoded geometries into <see cref="UnlitScene"/>-layout vertices for the unlit pass, which has no depth test:
/// the model is turned by yaw (about z) and pitch, fitted into the target with an orthographic view, back faces are
/// dropped on the CPU, the rest are sorted far to near and wound counter-clockwise on screen (the cache state's front
/// face). The vertex colour carries a Lambert term from the decoded normals; the texture adds the rest.
/// </summary>
public sealed record ModelView(int Width, int Height, float YawDegrees, float PitchDegrees)
{
    private const float Fill = 0.9f;
    private const float Ambient = 0.3f;
    private const float Diffuse = 0.7f;
    private const float FullByte = 255;
    private const int Corners = 3;
    private const int Pair = 2;
    private const uint OpaqueAlpha = 0xFF00_0000;
    private const int GreenShift = 8;
    private const int BlueShift = 16;
    private const int PositionY = 4;
    private const int ColourOffset = 12;
    private const int TexcoordU = 16;
    private const int TexcoordV = 20;

    // Light from the upper left, towards the viewer (view space: x right, y away from the viewer, z up).
    private static readonly Vector3 Light = Vector3.Normalize(new Vector3(-0.4f, -0.8f, 0.5f));

    /// <summary>The vertex data of every front-facing triangle, far to near.</summary>
    public byte[] Vertices(IReadOnlyList<MeshGeometry> geometries)
    {
        var rotation = Rotation();
        var (centre, scale) = Fit(geometries, rotation);
        var triangles = new List<ViewTriangle>();

        foreach (var geometry in geometries)
        {
            Collect(geometry, rotation, centre, scale, triangles);
        }

        triangles.Sort((left, right) => right.Depth.CompareTo(left.Depth));
        var data = new byte[triangles.Count * Corners * UnlitScene.VertexStride];

        for (var index = 0; index < triangles.Count; index++)
        {
            triangles[index].Write(data.AsSpan(index * Corners * UnlitScene.VertexStride));
        }

        return data;
    }

    private Matrix4x4 Rotation()
    {
        var yaw = Matrix4x4.CreateRotationZ(float.DegreesToRadians(YawDegrees));
        var pitch = Matrix4x4.CreateRotationX(-float.DegreesToRadians(PitchDegrees));

        return yaw * pitch;
    }

    /// <summary>The view-space centre of the rotated bounds and the pixels per unit that fit them into the target.</summary>
    private (Vector3 Centre, float Scale) Fit(IReadOnlyList<MeshGeometry> geometries, Matrix4x4 rotation)
    {
        var low = new Vector3(float.MaxValue);
        var high = new Vector3(float.MinValue);

        foreach (var geometry in geometries)
        {
            for (var vertex = 0; vertex < geometry.VertexCount; vertex++)
            {
                var point = Vector3.Transform(Position(geometry.Positions, vertex), rotation);
                low = Vector3.Min(low, point);
                high = Vector3.Max(high, point);
            }
        }

        var size = high - low;
        var scale = Fill * MathF.Min(Width / MathF.Max(size.X, float.Epsilon), Height / MathF.Max(size.Z, float.Epsilon));

        return ((low + high) / Pair, scale);
    }

    private void Collect(MeshGeometry geometry, Matrix4x4 rotation, Vector3 centre, float scale, List<ViewTriangle> triangles)
    {
        for (var corner = 0; corner < geometry.Indices.Length; corner += Corners)
        {
            var corners = new ViewVertex[Corners];
            var points = new Vector3[Corners];

            for (var index = 0; index < Corners; index++)
            {
                var vertex = geometry.Indices[corner + index];
                points[index] = Vector3.Transform(Position(geometry.Positions, vertex), rotation) - centre;
                corners[index] = Corner(geometry, vertex, points[index], rotation, scale);
            }

            var faceNormal = Vector3.Cross(points[1] - points[0], points[Pair] - points[0]);

            if (faceNormal.Y >= 0)
            {
                continue;
            }

            triangles.Add(new ViewTriangle(corners, (points[0].Y + points[1].Y + points[Pair].Y) / Corners));
        }
    }

    private ViewVertex Corner(MeshGeometry geometry, int vertex, Vector3 point, Matrix4x4 rotation, float scale)
    {
        var shade = 1f;

        if (geometry.Normals is not null)
        {
            var normal = Vector3.TransformNormal(Position(geometry.Normals, vertex), rotation);
            shade = Ambient + (Diffuse * MathF.Max(0, Vector3.Dot(normal, Light)));
        }

        var grey = (uint)MathF.Round(shade * FullByte);
        var colour = OpaqueAlpha | grey | (grey << GreenShift) | (grey << BlueShift);
        var texCoord = Vector2.Zero;

        if (geometry.TexCoords is not null)
        {
            texCoord = new Vector2(geometry.TexCoords[vertex * Pair], geometry.TexCoords[(vertex * Pair) + 1]);
        }

        return new ViewVertex((Width / Pair) + (point.X * scale), (Height / Pair) - (point.Z * scale), colour, texCoord);
    }

    private static Vector3 Position(float[] values, int vertex)
    {
        return new Vector3(values[vertex * Corners], values[(vertex * Corners) + 1], values[(vertex * Corners) + Pair]);
    }

    private readonly record struct ViewVertex(float X, float Y, uint Colour, Vector2 TexCoord);

    private sealed record ViewTriangle(ViewVertex[] Corners, float Depth)
    {
        /// <summary>Writes the three corners, swapped if needed so the screen-space cross product is negative (y down).</summary>
        public void Write(Span<byte> data)
        {
            var (a, b, c) = (Corners[0], Corners[1], Corners[Pair]);
            var cross = ((b.X - a.X) * (c.Y - a.Y)) - ((c.X - a.X) * (b.Y - a.Y));

            if (cross > 0)
            {
                (b, c) = (c, b);
            }

            WriteVertex(data, a);
            WriteVertex(data[UnlitScene.VertexStride..], b);
            WriteVertex(data[(Pair * UnlitScene.VertexStride)..], c);
        }

        private static void WriteVertex(Span<byte> vertex, ViewVertex corner)
        {
            BinaryPrimitives.WriteSingleLittleEndian(vertex, corner.X);
            BinaryPrimitives.WriteSingleLittleEndian(vertex[PositionY..], corner.Y);
            BinaryPrimitives.WriteUInt32LittleEndian(vertex[ColourOffset..], corner.Colour);
            BinaryPrimitives.WriteSingleLittleEndian(vertex[TexcoordU..], corner.TexCoord.X);
            BinaryPrimitives.WriteSingleLittleEndian(vertex[TexcoordV..], corner.TexCoord.Y);
        }
    }
}
