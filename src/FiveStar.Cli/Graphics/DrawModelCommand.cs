// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Diagnostics;
using FiveStar.Cli.Model;
using FiveStar.Cli.Shader;
using FiveStar.Formats.Common;
using FiveStar.Formats.Compression;
using FiveStar.Formats.Images;
using FiveStar.Formats.Models;
using FiveStar.Formats.Rpf;
using FiveStar.Graphics;
using FiveStar.Graphics.Rendering;
using FiveStar.Shaders.Disassembly;

namespace FiveStar.Cli.Graphics;

/// <summary>
/// <c>fivestar graphics draw-model</c>: decodes one drawable carved from an archive and draws it with the game's
/// translated immediate-mode unlit pass, textured with its main shader's <c>DiffuseTex</c> and shaded by its normals.
/// </summary>
internal static class DrawModelCommand
{
    public const string Usage =
        "draw-model <out.png> --archive <file.rpf> --from 0xOFFSET [--name TEXT] [--lod N] [--yaw DEG] [--pitch DEG] [--size N] [--plain] [--dump <folder>] [--programs <fssm folder>] [--adapter <index|name|warp>] [--work <dir>] [--debug-layer]";

    private const int Success = 0;
    private const int Failure = 1;
    private const string DumpVariable = "FIVESTAR_DUMP";
    private const string ProgramsVariable = "FIVESTAR_SHADER_DIR";
    private const string DebugFlag = "debug-layer";
    private const string PlainFlag = "plain";
    private const string Warp = "warp";
    private const int DefaultSize = 768;
    private const int DefaultYaw = 35;
    private const int DefaultPitch = 20;
    private const int Corners = 3;

    public static int Run(IReadOnlyList<string> args)
    {
        var arguments = new CommandArguments(args, new HashSet<string> { DebugFlag, PlainFlag });

        if (arguments.Positional.Count != 1)
        {
            throw new ArgumentException(Usage);
        }

        var output = Path.GetFullPath(arguments.Positional[0]);
        var dump = Required(arguments.Option("dump") ?? Environment.GetEnvironmentVariable(DumpVariable), "--dump or " + DumpVariable);
        var programs = Required(arguments.Option("programs") ?? Environment.GetEnvironmentVariable(ProgramsVariable), "--programs or " + ProgramsVariable);
        var work = arguments.Option("work") ?? Path.Combine(Path.GetDirectoryName(output)!, Path.GetFileNameWithoutExtension(output) + "-shaders");
        var clock = Stopwatch.StartNew();

        var drawable = FindDrawable(Required(arguments.Option("archive"), "--archive"), ModelReports.HexOption(arguments.Option("from")), arguments.Option("name") ?? string.Empty);
        var (geometries, texture) = Select(drawable, arguments.IntegerOption("lod") ?? 0, dump, arguments.Flag(PlainFlag));
        var size = arguments.IntegerOption("size") ?? DefaultSize;
        var view = new ModelView(size, size, arguments.IntegerOption("yaw") ?? DefaultYaw, arguments.IntegerOption("pitch") ?? DefaultPitch);
        var vertices = view.Vertices(geometries);
        var pass = UnlitPass.Load(dump, programs, work, new RdnaDecoder(), ShaderTools.Dxc(arguments));

        using var device = Open(arguments.Option("adapter"), arguments.Flag(DebugFlag));
        var image = UnlitRenderer.Draw(device, pass.Pipeline, pass.Vertex, pass.Pixel, texture, size, size, vertices);
        PngWriter.Write(output, image.Width, image.Height, image.Pixels);
        Console.WriteLine($"{device.AdapterName}: wrote {output} ({vertices.Length / UnlitScene.VertexStride / Corners} front-facing triangles) in {clock.Elapsed.TotalSeconds:F1} s");

        foreach (var error in image.Errors)
        {
            Console.Error.WriteLine($"debug layer: {error}");
        }

        if (image.Errors.Count > 0)
        {
            return Failure;
        }

        return Success;
    }

    private static Drawable FindDrawable(string archive, long from, string name)
    {
        using var source = new MappedFileSource(archive);
        var carver = new ZlibEntryCarver(source, new ZlibStreamProbe(source, new ZlibInflater()));

        foreach (var carved in DrawableScanner.Scan(carver.CarveFrom(from, 0)))
        {
            if (carved.Drawable.Name.Contains(name, StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"{carved.Drawable.Name} at 0x{carved.Offset:x}: {carved.Drawable.Geometries.Count} geometries, {carved.Drawable.Problems.Count} problems");

                return carved.Drawable;
            }
        }

        throw new ArgumentException($"no drawable named *{name}* after 0x{from:x}");
    }

    /// <summary>The geometries to draw and their texture: the main shader's geometries and diffuse texture, or all of them plain.</summary>
    private static (IReadOnlyList<MeshGeometry> Geometries, TextureData Texture) Select(Drawable drawable, int lod, string dump, bool plain)
    {
        var geometries = drawable.Lod(lod);

        if (geometries.Count == 0)
        {
            throw new ArgumentException($"{drawable.Name} has no decoded geometry at level of detail {lod}");
        }

        var shader = geometries.GroupBy(geometry => geometry.Shader).MaxBy(group => group.Sum(geometry => geometry.TriangleCount))!.Key;
        var hash = drawable.Shaders.ElementAtOrDefault(shader)?.Diffuse;
        (string Path, int Index)? found = null;

        if (!plain && hash.HasValue)
        {
            found = ModelTexture.Find(dump, hash.Value);
        }

        if (!found.HasValue)
        {
            Console.WriteLine($"{geometries.Count} geometries untextured (main shader {shader}, DiffuseTex {hash:x16}, plain {plain})");

            return (geometries, ModelTexture.Plain());
        }

        var chosen = geometries.Where(geometry => geometry.Shader == shader).ToList();
        Console.WriteLine($"shader {shader}: {chosen.Count} of {geometries.Count} geometries, DiffuseTex {hash:x16} = {found.Value.Path} #{found.Value.Index}");

        return (chosen, ModelTexture.Load(found.Value.Path, found.Value.Index));
    }

    private static GpuDevice Open(string? adapter, bool debugLayer)
    {
        if (string.Equals(adapter, Warp, StringComparison.OrdinalIgnoreCase))
        {
            return GpuDevice.CreateWarp(debugLayer);
        }

        return new GpuDevice(PsoCommand.SelectAdapter(adapter).Index, debugLayer);
    }

    private static string Required(string? value, string source)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"set {source}");
        }

        return value;
    }
}
