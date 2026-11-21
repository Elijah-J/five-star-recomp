// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using FiveStar.Shaders.Containers;
using FiveStar.Shaders.Disassembly;
using FiveStar.Shaders.Hardware;

namespace FiveStar.Shaders.Translation.Core;

/// <summary>
/// The state of one program translation: the statements emitted so far, which registers exist, which hold lane masks
/// and known constants, and what the program declares (inputs, attributes, exports, static samplers).
/// </summary>
internal sealed class TranslationState
{
    private const int Wave64 = 64;

    // Mesh shaders without numthreads in their PSV0 run 128 threads (NGG groups hold up to 128 vertices).
    private const int DefaultMeshThreads = 128;

    private int _temporaries;

    public TranslationState(ShaderProgram program, IReadOnlyList<Instruction> instructions, ShaderStage stage, bool copyShader)
    {
        Stage = stage;
        Instructions = [.. instructions];
        Code = program.Code;
        Signatures = program.Signatures;
        Config = program.Config;
        PipelineState = program.PipelineState;

        IsCopyShader = copyShader;

        var threads = program.PipelineState?.NumThreads;
        var meshThreads = threads is null ? DefaultMeshThreads : threads.Value.X;
        MeshThreads = stage == ShaderStage.Mesh ? meshThreads : 0;
        Masks.Add(SpecialRegisters.Exec);
    }

    public ShaderStage Stage { get; }

    /// <summary>The instructions; long jumps are rewritten in place.</summary>
    public List<Instruction> Instructions { get; }

    public byte[]? Code { get; }

    public ShaderSignatures Signatures { get; }

    public ShaderConfig Config { get; }

    public PipelineStateInfo? PipelineState { get; }

    /// <summary>True while translating the VS copy shader of a geometry program (emitted as a function).</summary>
    public bool IsCopyShader { get; }

    public int MeshThreads { get; }

    public bool IsWave64 => Config.WaveSize == Wave64;

    /// <summary>Hardware-preloaded inputs, assigned before the first block.</summary>
    public List<string> Prologue { get; } = [];

    public List<CodeBlock> Blocks { get; } = [];

    /// <summary>The statements of the block being translated.</summary>
    public List<string> Body { get; set; } = [];

    public bool HasControlFlow { get; set; }

    /// <summary>Every register the program touches (declared as uint).</summary>
    public HashSet<RegisterKey> Variables { get; } = [];

    /// <summary>Registers written before the current point; reading any other register reads a hardware input.</summary>
    public HashSet<RegisterKey> Written { get; } = [];

    /// <summary>Registers currently holding a lane mask (a per-lane bool); EXEC starts as one.</summary>
    public HashSet<RegisterKey> Masks { get; } = [];

    /// <summary>Registers that ever held a lane mask (their bools are declared).</summary>
    public HashSet<RegisterKey> MasksEver { get; } = [];

    /// <summary>Wave64: SGPRs holding the high dword of a lane mask.</summary>
    public HashSet<RegisterKey> MaskHighHalves { get; } = [];

    /// <summary>Registers holding a known constant in the current block (for literal descriptors).</summary>
    public Dictionary<RegisterKey, long> Constants { get; } = [];

    /// <summary>VGPR number to the system input it holds (vid, iid, bary, pos_x, ...).</summary>
    public Dictionary<int, string> Inputs { get; } = [];

    /// <summary>Pixel-shader attribute number to how it is read (linear, flat, vtx1, vtx2, f16lo, f16hi).</summary>
    public Dictionary<int, HashSet<string>> Attributes { get; } = [];

    /// <summary>Export targets written (mrt0, mrtz, pos0, param3, ...).</summary>
    public HashSet<string> Outputs { get; } = [];

    /// <summary>Mesh staging: export target to the number of components written.</summary>
    public Dictionary<string, int> MeshWidths { get; } = [];

    /// <summary>Literal sampler descriptors, in first-use order (index = slot after STATIC_SAMPLER_BASE).</summary>
    public List<long[]> StaticSamplers { get; } = [];

    /// <summary>Vertex attributes read through the fetch shader: (attribute, component count).</summary>
    public HashSet<(int Attribute, int Components)> FetchInputs { get; } = [];

    /// <summary>The program starts with a call into the driver's vertex fetch subroutine.</summary>
    public bool UsesFetchShader { get; set; }

    /// <summary>The program reads its own code bytes (s_getpc_b64 pointers).</summary>
    public bool UsesCode { get; set; }

    public bool UsesBvh { get; set; }

    public bool UsesLds { get; set; }

    /// <summary>Mesh shader in two-phase direct-output mode (s_barrier becomes a yield point).</summary>
    public bool MeshYield { get; set; }

    /// <summary>The program ends a primitive-ordered (POPS) critical section.</summary>
    public bool Pops { get; set; }

    /// <summary>DPP: the bool that says whether this lane writes its destination (else it keeps its old value).</summary>
    public string? DppKeep { get; set; }

    /// <summary>Lines that assemble attrN values from the input signature (set when emitting).</summary>
    public List<string> InputMap { get; } = [];

    /// <summary>Lines that write signature outputs from the export registers (set when emitting).</summary>
    public List<string> OutputMap { get; } = [];

    /// <summary>Appends one statement to the current block.</summary>
    public void Emit(string line)
    {
        Body.Add(line);
    }

    /// <summary>A fresh number for a temporary name.</summary>
    public int NextTemporary()
    {
        var number = _temporaries;
        _temporaries++;

        return number;
    }

    /// <summary>Marks a register as written and present.</summary>
    public void Touch(RegisterKey key)
    {
        Written.Add(key);
        Variables.Add(key);
    }

    /// <summary>Records how a pixel-shader attribute is read.</summary>
    public void AddAttributeMode(int attribute, string mode)
    {
        if (!Attributes.TryGetValue(attribute, out var modes))
        {
            modes = [];
            Attributes[attribute] = modes;
        }

        modes.Add(mode);
    }
}
