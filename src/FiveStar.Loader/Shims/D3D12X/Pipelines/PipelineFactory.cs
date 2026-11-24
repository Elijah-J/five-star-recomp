// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using FiveStar.Loader.Native;
using FiveStar.Shaders.Containers;
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
    /// <summary>
    /// DXIL size from which a pipeline is built alone: the NVIDIA driver took over 40 GB and minutes for one of the
    /// 10 MB ray-traced shadow programs (docs/d3d12x-pipelines.md), so two at once can exhaust memory.
    /// </summary>
    public const long HeavyShaderBytes = 4L << 20;

    private readonly object _gate = new();
    private readonly object _heavy = new();
    private readonly nint _device2;
    private readonly nint _rootSignature;
    private readonly IProgramLibrary _programs;
    private readonly ProgramSignatures _signatures;
    private readonly OnceLog _warnings;
    private readonly RectanglePipelines? _rectangles;
    private readonly Dictionary<nint, GraphicsPipelineDescription> _created = [];

    /// <param name="device2">A desktop ID3D12Device2 (the caller keeps it alive).</param>
    /// <param name="rootSignature">The root signature of translated programs on that device (<see cref="BindlessRoot"/>).</param>
    /// <param name="programs">The translated programs.</param>
    /// <param name="signatures">The input signatures of the extracted programs, for cache layouts.</param>
    /// <param name="warnings">Where failures are reported, once each.</param>
    /// <param name="rectangles">Told every graphics pipeline, for its rectangle-list variant; null for none.</param>
    /// <param name="standIns">Hands out pipelines the driver takes too long for.</param>
    /// <param name="cache">The disk cache of built pipelines; null for none.</param>
    public PipelineFactory(
        nint device2, nint rootSignature, IProgramLibrary programs, ProgramSignatures signatures, OnceLog warnings, RectanglePipelines? rectangles, StandInPipelines standIns,
        PipelineLibraryCache? cache)
    {
        _device2 = device2;
        _rootSignature = rootSignature;
        _programs = programs;
        _signatures = signatures;

        _warnings = warnings;

        _rectangles = rectangles;
        StandIns = standIns;
        Cache = cache;
    }

    /// <summary>The disk cache of built pipelines, or null.</summary>
    public PipelineLibraryCache? Cache { get; }

    /// <summary>Pipelines handed out before the driver built them.</summary>
    public StandInPipelines StandIns { get; }

    /// <summary>Bytes of DXIL the program library keeps in memory.</summary>
    public long ProgramBytes => _programs.HeldBytes;

    /// <summary>The creations running now (<see cref="PipelineSlots"/> marks them).</summary>
    public PipelineCreationsInFlight InFlight { get; } = new();

    public int Graphics(GraphicsPipelineDescription description, Guid* iid, nint* result)
    {
        var recipe = PrepareGraphics(description);

        if (recipe is null)
        {
            return HResult.NotFound;
        }

        InFlight.Describe(recipe.Names);
        Action<nint> built = pipeline => _rectangles?.Remember(pipeline, recipe.Desktop!, recipe.Programs);
        var hr = Create(recipe.Stream, iid, result, recipe.Names, built);

        return Remember(description, hr, result, recipe.Names);
    }

    public int Compute(ComputePipelineDescription description, Guid* iid, nint* result)
    {
        var recipe = PrepareCompute(description);

        if (recipe is null)
        {
            return HResult.NotFound;
        }

        InFlight.Describe(recipe.Names);
        var hr = Create(recipe.Stream, iid, result, recipe.Names, null);
        Report(hr, recipe.Names);

        return hr;
    }

    /// <summary>
    /// The desktop stream of a graphics description: its translated programs, the vertex elements renamed to the
    /// translated attributes. Null when a shader has no translation (reported once).
    /// </summary>
    public PipelineRecipe? PrepareGraphics(GraphicsPipelineDescription description)
    {
        var programs = new List<TranslatedProgram>();
        IReadOnlyList<SignatureElement>? inputs = null;

        foreach (var source in description.Shaders)
        {
            var program = Resolve(source);

            if (program is null)
            {
                return null;
            }

            if (program.Stage == HardwareStage.Vertex)
            {
                inputs = VertexInputs(source, program);
            }

            programs.Add(program);
        }

        var desktop = description;

        if (programs.Exists(program => program.Stage == HardwareStage.Vertex))
        {
            desktop = description with { InputLayout = InputLayoutMapping.Map(description.InputLayout, inputs) };
        }

        var names = string.Join(" + ", programs.Select(program => program.Name));

        return new PipelineRecipe(DesktopPipelineBuilder.GraphicsStream(_device2, _rootSignature, desktop, programs), desktop, programs, names);
    }

    /// <summary>The desktop stream of a compute description; null when its shader has no translation.</summary>
    public PipelineRecipe? PrepareCompute(ComputePipelineDescription description)
    {
        var program = Resolve(description.Shader);

        if (program is null)
        {
            return null;
        }

        return new PipelineRecipe(DesktopPipelineBuilder.ComputeStream(_rootSignature, program), null, [program], program.Name);
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

    /// <summary>
    /// The pipeline of <paramref name="stream"/>: from the cache on this thread when it is there, else built on the
    /// build threads (<see cref="StandInPipelines"/>) and stored in the cache. Takes ownership of the stream, whose
    /// native copy is freed while it waits for a build thread.
    /// </summary>
    private int Create(DesktopStream stream, Guid* iid, nint* result, string names, Action<nint>? built)
    {
        if (Cache is not null && !HResult.Failed(Cache.Load(stream, iid, result)))
        {
            stream.Dispose();
            built?.Invoke(*result);

            return HResult.Ok;
        }

        *result = 0;
        var wanted = *iid;
        stream.Release();

        return StandIns.Create(() => Build(stream, wanted), result, names, built);
    }

    /// <summary>
    /// Builds the pipeline of <paramref name="stream"/> on this thread and stores it in the cache; disposes the stream.
    /// Pipelines with <see cref="HeavyShaderBytes"/> of DXIL or more are built one at a time, and no build starts while
    /// the machine is short of commit (<see cref="WarmMemoryGuard"/>), since one driver compile can take gigabytes.
    /// </summary>
    public PipelineResult Build(DesktopStream stream, Guid iid)
    {
        using (stream)
        {
            WarmMemoryGuard.WaitForRoom(static () => false);

            if (stream.ShaderBytes < HeavyShaderBytes)
            {
                return BuildNow(stream, iid);
            }

            lock (_heavy)
            {
                return BuildNow(stream, iid);
            }
        }
    }

    private PipelineResult BuildNow(DesktopStream stream, Guid iid)
    {
        nint pipeline = 0;
        int hr = stream.Create(_device2, &iid, &pipeline);

        if (!HResult.Failed(hr) && pipeline != 0)
        {
            Cache?.Store(stream.Key, pipeline);
        }

        return new PipelineResult(hr, pipeline);
    }

    /// <summary>The vertex shader's inputs: from its container, or for a cache code block from its program's container.</summary>
    private IReadOnlyList<SignatureElement>? VertexInputs(ShaderSource source, TranslatedProgram program)
    {
        if (source.Kind != ShaderSourceKind.CacheCode)
        {
            return InputLayoutMapping.Inputs(source.Bytes);
        }

        var inputs = _signatures.Inputs(program.Name);

        if (inputs is null)
        {
            _warnings.Warn($"signature-{program.Name}", $"[d3d12_xs] no DXBC container for {program.Name}: its cache input layout is taken in attribute order");
        }

        return inputs;
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
                InFlight.Describe(PipelineFailureTally.MissingProgram + key);
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
