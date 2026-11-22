// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using FiveStar.Graphics;
using FiveStar.Graphics.Pipelines;
using FiveStar.Graphics.Rendering;
using Vortice.Direct3D;
using Vortice.Direct3D12;
using Vortice.Mathematics;

namespace FiveStar.Cli.Graphics;

/// <summary>
/// Draws <see cref="UnlitScene"/> with the translated unlit pass: the cache's blend, rasterizer and depth state and
/// input layout, the bindless root signature, the descriptor table of <see cref="UnlitBindings"/> and one texture.
/// </summary>
public static class UnlitRenderer
{
    /// <summary>The colour the target is cleared to (opaque dark grey).</summary>
    public static readonly Color4 Clear = new(0.125f, 0.125f, 0.125f, 1);

    private const float AlphaScale = 1;
    private const float ColourScale = 1;

    public static SampleFrame Render(GpuDevice gpu, SamplePipeline pipeline, TranslatedShader vertex, TranslatedShader pixel, TextureData texture)
    {
        var device = gpu.Device;
        var scene = new UnlitScene(texture.Width, texture.Height);
        var targets = CacheStateMapping.Targets(pipeline.State);
        using var root = BindlessRootSignature.Create(device);
        using var state = CreatePipeline(gpu, root, pipeline, vertex, pixel);

        using var context = new CommandContext(device);
        using var arena = new ResourceArena(device);
        using var heaps = new BindlessHeaps(device, UnlitBindings.ResourceSlots, HardwareSampler.SamplerHeapSlots);
        using var target = new RenderTarget(device, targets[0], scene.Width, scene.Height);

        var list = context.List;
        Bind(heaps, arena, list, scene, texture);
        StaticSamplers(heaps, pixel);
        var userData = arena.UploadBuffer(UnlitBindings.UserData());
        var vertices = arena.UploadBuffer(scene.Vertices());

        heaps.Bind(list);
        list.SetGraphicsRootSignature(root);
        list.SetGraphicsRootConstantBufferView(BindlessRootSignature.UserDataParameter, userData.GPUVirtualAddress);
        list.SetPipelineState(state);

        target.Begin(list, Clear);
        list.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
        list.IASetVertexBuffers(0, new VertexBufferView(vertices.GPUVirtualAddress, (uint)(UnlitScene.VertexCount * UnlitScene.VertexStride), UnlitScene.VertexStride));
        list.DrawInstanced((uint)UnlitScene.VertexCount, 1, 0, 0);
        target.CopyOut(list);
        context.ExecuteAndWait();

        return new SampleFrame(scene, target.ReadPixels(), gpu.TakeErrors());
    }

    /// <summary>The pass's pipeline state; a rejection reports the debug layer's messages when it runs.</summary>
    private static ID3D12PipelineState CreatePipeline(GpuDevice gpu, ID3D12RootSignature root, SamplePipeline pipeline, TranslatedShader vertex, TranslatedShader pixel)
    {
        var layout = CacheStateMapping.InputLayout(pipeline.InputLayout);
        var targets = CacheStateMapping.Targets(pipeline.State);
        var program = new GraphicsProgram(vertex.Dxil, null, pixel.Dxil, layout, targets, Vortice.DXGI.Format.Unknown, PrimitiveTopologyType.Triangle);

        try
        {
            return new PipelineBuilder(gpu.Device, root).Graphics(program, CacheStateMapping.Describe(pipeline.State));
        }
        catch (SharpGen.Runtime.SharpGenException exception)
        {
            var messages = string.Join(Environment.NewLine, gpu.TakeErrors());

            throw new InvalidOperationException($"the pipeline state was rejected ({exception.ResultCode}): {messages}", exception);
        }
    }

    /// <summary>Fills the heap slots the descriptor table names: the window, the three constant buffers and the texture.</summary>
    private static void Bind(BindlessHeaps heaps, ResourceArena arena, ID3D12GraphicsCommandList list, UnlitScene scene, TextureData texture)
    {
        heaps.RawBuffer(arena.UploadBuffer(UnlitBindings.Window()), UnlitBindings.WindowBytes, UnlitBindings.WindowSlot);
        heaps.RawBuffer(arena.UploadBuffer(UnlitBindings.PixelConstants(AlphaScale, ColourScale)), UnlitBindings.PixelConstantsBytes, UnlitBindings.PixelConstantsSlot);
        heaps.RawBuffer(arena.UploadBuffer(UnlitBindings.ObjectConstants()), UnlitBindings.ObjectConstantsBytes, UnlitBindings.ObjectConstantsSlot);
        heaps.RawBuffer(arena.UploadBuffer(UnlitBindings.ViewConstants(scene.Width, scene.Height)), UnlitBindings.ViewConstantsBytes, UnlitBindings.ViewConstantsSlot);
        heaps.Texture(arena.Texture2D(list, texture), UnlitBindings.TextureSlot);
    }

    /// <summary>Creates the samplers the pixel program builds from literal S# words at STATIC_SAMPLER_BASE + k.</summary>
    private static void StaticSamplers(BindlessHeaps heaps, TranslatedShader pixel)
    {
        var samplers = HardwareSampler.StaticSamplers(pixel.Hlsl);

        for (var slot = 0; slot < samplers.Count; slot++)
        {
            heaps.Sampler(HardwareSampler.Describe(samplers[slot]), HardwareSampler.StaticSamplerBase + slot);
        }
    }
}
