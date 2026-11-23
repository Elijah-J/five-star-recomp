// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using FiveStar.Loader.Native;
using FiveStar.Shaders.Hardware;

namespace FiveStar.Loader.Shims.D3D12X.Pipelines;

/// <summary>
/// Turns a game pipeline description into a desktop ID3D12PipelineState: each shader becomes its translated program
/// (found by code-block hash prefix in the ahead-of-time translation, never compiled here), the root signature becomes
/// the one translated programs share, the vertex elements are renamed to the translated attributes and the
/// fixed-function descs pass through. Created graphics pipelines are remembered so slot 55 can derive from them.
/// </summary>
internal sealed unsafe class PipelineFactory
{
    private readonly object _gate = new();
    private readonly nint _device2;
    private readonly nint _rootSignature;
    private readonly IProgramLibrary _programs;
    private readonly OnceLog _warnings;
    private readonly RectanglePipelines? _rectangles;
    private readonly Dictionary<nint, GraphicsPipelineDescription> _created = [];

    /// <param name="device2">A desktop ID3D12Device2 (the caller keeps it alive).</param>
    /// <param name="rootSignature">The root signature of translated programs on that device (<see cref="BindlessRoot"/>).</param>
    /// <param name="programs">The translated programs.</param>
    /// <param name="warnings">Where failures are reported, once each.</param>
    /// <param name="rectangles">Told every graphics pipeline, for its rectangle-list variant; null for none.</param>
    /// <param name="standIns">Hands out pipelines the driver takes too long for.</param>
    public PipelineFactory(nint device2, nint rootSignature, IProgramLibrary programs, OnceLog warnings, RectanglePipelines? rectangles, StandInPipelines standIns)
    {
        _device2 = device2;
        _rootSignature = rootSignature;
        _programs = programs;
        _warnings = warnings;
        _rectangles = rectangles;
        StandIns = standIns;
    }

    /// <summary>Pipelines handed out before the driver built them.</summary>
    public StandInPipelines StandIns { get; }

    /// <summary>The creations running now (<see cref="PipelineSlots"/> marks them).</summary>
    public PipelineCreationsInFlight InFlight { get; } = new();

    public int Graphics(GraphicsPipelineDescription description, Guid* iid, nint* result)
    {
        var programs = new List<TranslatedProgram>();
        ShaderSource? vertex = null;

        foreach (var source in description.Shaders)
        {
            var program = Resolve(source);

            if (program is null)
            {
                return HResult.NotFound;
            }

            if (program.Stage == HardwareStage.Vertex)
            {
                vertex = source;
            }

            programs.Add(program);
        }

        var desktop = description;

        if (vertex is not null)
        {
            desktop = description with { InputLayout = InputLayoutMapping.Map(description.InputLayout, vertex) };
        }

        var names = string.Join(" + ", programs.Select(program => program.Name));
        InFlight.Describe(names);
        var wanted = *iid;
        var hr = StandIns.Create(() => BuildGraphics(desktop, programs, wanted), result, names, built => _rectangles?.Remember(built, desktop, programs));

        return Remember(description, hr, result, names);
    }

    public int Compute(ComputePipelineDescription description, Guid* iid, nint* result)
    {
        var program = Resolve(description.Shader);

        if (program is null)
        {
            return HResult.NotFound;
        }

        InFlight.Describe(program.Name);
        var wanted = *iid;
        var hr = StandIns.Create(() => BuildCompute(program, wanted), result, program.Name, null);
        Report(hr, program.Name);

        return hr;
    }

    /// <summary>A graphics pipeline made by this factory with some of its fixed-function descs replaced.</summary>
    public int Derive(nint basePipeline, IReadOnlyList<PipelineStateOverride> overrides, Guid* iid, nint* result)
    {
        GraphicsPipelineDescription? known;

        lock (_gate)
        {
            _created.TryGetValue(basePipeline, out known);
        }

        if (known is null)
        {
            InFlight.Describe("derive from an unknown base");
            _warnings.Warn("derive-unknown-base", $"[d3d12_xs] pipeline derive: base 0x{basePipeline:x} was not created here");

            return HResult.InvalidArgument;
        }

        var description = known;

        foreach (var entry in overrides)
        {
            description = Apply(description, entry);
        }

        return Graphics(description, iid, result);
    }

    private GraphicsPipelineDescription Apply(GraphicsPipelineDescription description, PipelineStateOverride entry)
    {
        switch ((OverrideKind)entry.Kind)
        {
            case OverrideKind.Rasterizer:
                return description with { Rasterizer = entry.Payload[..PipelineLayout.RasterizerDescSize] };
            case OverrideKind.DepthStencil:
                return description with { DepthStencil = DesktopStateBytes.WidenDepthStencil(entry.Payload) };
            case OverrideKind.Blend:
                return description with { Blend = entry.Payload[..PipelineLayout.BlendDescSize] };
            default:
                _warnings.Warn($"override-{entry.Kind}", $"[d3d12_xs] pipeline derive: override kind {entry.Kind} ignored");

                return description;
        }
    }

    private PipelineResult BuildGraphics(GraphicsPipelineDescription description, List<TranslatedProgram> programs, Guid iid)
    {
        nint pipeline = 0;
        int hr = DesktopPipelineBuilder.Graphics(_device2, _rootSignature, description, programs, &iid, &pipeline);

        return new PipelineResult(hr, pipeline);
    }

    private PipelineResult BuildCompute(TranslatedProgram program, Guid iid)
    {
        nint pipeline = 0;
        int hr = DesktopPipelineBuilder.Compute(_device2, _rootSignature, program, &iid, &pipeline);

        return new PipelineResult(hr, pipeline);
    }

    private TranslatedProgram? Resolve(ShaderSource source)
    {
        byte[] prefix;

        try
        {
            prefix = ProgramKeys.Prefix(source);
        }
        catch (InvalidDataException exception)
        {
            _warnings.Warn($"key-{exception.Message}", $"[d3d12_xs] pipeline shader not readable: {exception.Message}");

            return null;
        }

        var program = _programs.Find(prefix);

        if (program is null)
        {
            var key = ProgramKeys.Text(prefix);
            var names = _programs.Names(prefix);
            _warnings.Warn($"program-{key}", $"[d3d12_xs] no translated program for shader {key}");

            if (names.Count == 0)
            {
                InFlight.Describe($"no extracted program {key}");
            }
            else
            {
                InFlight.Describe($"no loadable form of {names[0]}");
            }
        }

        return program;
    }

    private int Remember(GraphicsPipelineDescription description, int hr, nint* result, string names)
    {
        Report(hr, names);

        if (HResult.Failed(hr) || *result == 0)
        {
            return hr;
        }

        lock (_gate)
        {
            _created[*result] = description;
        }

        return hr;
    }

    private void Report(int hr, string what)
    {
        if (HResult.Failed(hr))
        {
            InFlight.Describe($"desktop creation {HResult.Format(hr)} ({what})");
            _warnings.Warn($"create-{hr:x8}-{what}", $"[d3d12_xs] desktop pipeline creation failed ({what}): {HResult.Format(hr)}");
        }
    }
}
