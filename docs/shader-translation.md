# Shader translation

`src/FiveStar.Shaders/Translation` turns a native RDNA program (FSSM, see [fssm.md](fssm.md)) into HLSL for shader
model 6.7. Public entry: `HlslTranslator.Translate(ShaderProgram) -> TranslationResult`.

## Pipeline

1. **Load** (`ProgramLoader`): decode the FSSM header; disassemble the code with llvm-mc/llvm-objdump (gfx1013, wave64
   programs with `+wavefrontsize64`); read ISG1/OSG1/PSV0 from `<fssm folder>/../dxbc/<name>.dxbc` when present.
2. **Blocks** (`ControlFlowTranslator`): rewrite long jumps, split into basic blocks, drop unreachable blocks.
3. **Emitters** (`Translation/Emitters`): one emitter per instruction family turns each instruction into HLSL statements.
4. **Program** (`Translation/Stages`, `Translation/Structure`): structs, declarations, prologue, dispatch loop (or structured
   control flow) and output mapping.
5. **Compile** (`CorpusTranslator`): dxc, with the mesh fallback where needed.

## Execution model

- One HLSL invocation is one GPU lane. Every SGPR and VGPR is a `uint` variable (`s4`, `v7`, `s_vcc`, `s_m0`, `s_exec`).
- EXEC is the per-lane bool `active`. VALU and memory results are written `if (active)`; stores and atomics are guarded.
- A lane mask held in SGPRs (v_cmp results, saved EXEC) is a per-lane bool `m_sN`; v_cmp writes `active && cond`.
- A lane mask used as an integer (`s_bcnt1`, `s_ff1`, stored, compared) is rebuilt with `WaveActiveBallot` /
  `WaveActiveCountBits`; an integer SGPR pair used as a mask is tested with `lane_bit(lo, hi)`.
- SCC is the bool `scc`, set by compares, logic, carries and shifts as the hardware does.
- Registers read before any write are hardware inputs, assigned in a prologue (rules in [fssm.md](fssm.md)); a read
  outside the input layout is 0.

## Control flow

Every branch condition is scalar (SCC) or a wave-wide reduction (`execz`/`vccz` become `WaveActiveAnyTrue`), so all
lanes of a wave follow the same path and divergence lives only in `active`. Blocks become `case`s of a
`switch (pc)` inside `[loop] while (pc < PC_YIELD)`; each block ends by setting `pc` (`PC_END` after `s_endpgm`). Block
leaders are the entry, branch targets and the instruction after a branch or an early `s_endpgm`; blocks not reachable
from the entry (constant tables after the last `s_endpgm`) are skipped. A program without branches is emitted as
straight-line code without the loop.

**Structured form** (`TranslationOptions.StructuredControlFlow`, CLI `--structured`). The blocks become nested
statements instead of `switch` cases:

| Graph shape | HLSL |
|---|---|
| Natural loop (back edge to a header that dominates its source) | `[loop] for (;;) { ... }`; back edge = end of body or `continue`, edge out = `break` |
| Loop with several exit targets | `uint loop_exitN` set before each `break`, tested by an if/else chain after the loop |
| Conditional branch | `if`/`else` whose arms end at the branch's immediate post-dominator |
| Arms that leave the loop on some paths | the join is the post-dominator once the edges out of the loop are set aside; the leaving arm ends in `break` |
| Arms that end the program on some paths | the join is the post-dominator once small exit tails (at most 64 lines that only lead to the end, such as a kill-and-end block) are set aside; the tails are copied into each arm |
| Arms that never join | the smaller ones inside the `if`, ending in `break`/`continue`; the largest after it |
| Early end outside every loop | `break` out of a `do { ... } while (false)` around the body |

A block reached on paths that do not join before it is copied into each path. When the copies make a scope (the top
level or one loop body) more than 1.5 times its own lines plus 256, or nesting passes 120 levels, that scope is
emitted guarded instead: `uint flowN` holds the number of the node to run, and the scope's blocks follow in
topological order, each as `if (flowN == k) { ...; flowN = <successor>; }`, so one pass runs one path; loops nested in
it stay structured. A two-phase mesh shader keeps its phase loops and `[loop] while (pc < PC_YIELD) switch (pc)`, with
one case per region (the entry and each resume point after a barrier) holding the structured code up to the next barrier
or the end; WARP's shader compiler crashes when the `while` is left out.

The whole program keeps the dispatch form, with a comment `// control flow: dispatch loop (<reason>)`, when its graph
is irreducible (a cycle entered other than through one dominating header: `irreducible`) or has a loop that never
exits (`loop without exit`; DXIL requires every loop to have a break).

`s_getpc; s_add_u32/s_sub_u32; s_addc_u32/s_subb_u32; s_setpc` on one register pair is a long branch and becomes a
static branch. Other `s_setpc`/`s_swappc` (indirect jumps) are not supported, except the fetch-shader call at a
vertex shader's entry.

## Resources

The D3D12.X layer creates the views and samplers, so it decides what descriptor memory holds:

| Descriptor | Meaning in translated code |
|---|---|
| T# dword0 | `ResourceDescriptorHeap` index |
| S# dword0 | `SamplerDescriptorHeap` index; an S# built from literal constants gets `STATIC_SAMPLER_BASE + k` and its words are listed at the top of the program |
| V# dword0 / dword1 / dword3 | heap index / stride (idxen) / format code for `buffer_*_format` (layout × 8 + numeric type, GCN numbering) |
| 64-bit pointers (`s_load`, `global_*`) | byte address in the GPU-memory window `ResourceDescriptorHeap[GPU_MEM_HEAP]`, writable view at `+ 1` |
| `s_getpc_b64` pointers | high dword `CODE_TAG`; loads read the program's own code, emitted as `g_code[]` |
| BVH T# | dword0 = BVH base address >> 8 in the memory window (see [ray-intersection.md](ray-intersection.md)) |

A buffer the program also stores to is loaded through its RW view (`BufLoadRW`). Cube sampling rebuilds a direction
from (s, t, face) with `cube_dir`.

## Stages

| Stage | Entry | Inputs | Outputs |
|---|---|---|---|
| PS | `Out main(PSIn pin, SV_Position, SV_IsFrontFace, SV_SampleIndex, SV_Coverage, SV_Barycentrics)` (only those used) | attrN = N-th non-system ISG1 register, original semantics and types; `v_interp_mov p0` → nointerpolation; p10/p20 → `GetAttributeAtVertex` | mrtN → SV_TargetN with the OSG1 type, mrtz → SV_Depth |
| VS | `Out main(SV_VertexID, SV_InstanceID[, FetchIn fin])` | fetch-shader attributes → `FetchIn` members `ATTR<n>` | paramN → N-th non-system OSG1 register; pos0 → SV_Position; pos1.. → clip/cull distances |
| CS | `[numthreads(x, y, z)] main(SV_GroupID, SV_GroupThreadID)` | group and thread ids | none; LDS → `groupshared uint g_lds[]` |
| MS | `[numthreads(n,1,1)] [outputtopology("triangle")] main(..., out vertices Out verts[], out indices uint3 tris[], out primitives MSPrim prims[])` | group id, group index | as VS; prim export → `tris` + `SV_CullPrimitive` |
| GS | `[maxvertexcount(n)] main(<prim> GSIn gin[], SV_PrimitiveID, inout <Stream><Out>)` | ISG1 registers | via the copy shader |

**Pixel shaders.** `active` starts as `!IsHelperLane()` and the entry has `[WaveOpsIncludeHelperLanes]`, so wave and
quad operations see helper lanes. The final export (`done vm`) discards lanes with EXEC clear. With primitive-ordered
shading (POPS) every writable view becomes `RasterizerOrdered*`, the exiting-wave-id wait passes at once and
`MSG_ORDERED_PS_DONE` does nothing.

**Packed f16 attributes.** A PS reading half of an attribute channel reads `ATTRLO<n>`/`ATTRHI<n>`; a VS with up to 10
params (a mesh shader with up to 2) also exports each param unpacked into those two halves.

**Mesh shaders (NGG).** `s_sendmsg(MSG_GS_ALLOC_REQ)` (M0 = primitives << 12 | vertices) records the output counts.
When every export follows a barrier that follows the allocation, the program runs in two phases: phase 1 without
output writes until that barrier, then `SetMeshOutputCounts` at top level, then phase 2 resumes and writes `verts`,
`tris` and `prims` directly. Otherwise exports are staged in compact `groupshared` structs (at most 28 KB with LDS) and
copied out after the loop. A primitive export holds three 9-bit indices at bits 0/10/20; bit 31 culls. Thread index
code uses a virtual wave64 lane (`in_gi & 63`).

**Geometry shaders (legacy GS).** One HLSL invocation is one lane with a private `static uint g_lds[]`. The input
vertices are written at `GS_IN_BASE` (register r component c at dword r × 4 + c, stride `GS_IN_STRIDE`), and the vertex
offsets in v0/v1/v3 point there. The ring-table load returns 0 and `mbcnt` 0, so output records start at 0. `GS_EMIT`
calls the translated copy shader as the function `gs_copy(record, 0)` and appends its `Out`; `GS_CUT` restarts the
strip. The record size `GS_VTX_DW` is derived from the LDS write offsets between emits.

## Limits and fallbacks

| Limit | Handling |
|---|---|
| Mesh output over 32 KB (D3D12 PC limit) | rewritten as a compute shader plus a vertex-pulling vertex shader (`MeshComputeFallback`, files `.msfb_cs.hlsl` / `.msfb_vs.hlsl`) |
| HLSL over 150,000 characters | compiled with `-Od` (dxc's optimizer is very slow on huge dispatch loops) |
| Wave size | AMD lane numbers are kept; a compute program that reads other lanes by number gets `[WaveSize(32 or 64)]` (`TranslationOptions.PinWaveSize`, on in `fivestar shader translate`), so it fails to load on a GPU without that wave size instead of computing wrong results. Other stages cannot pin a wave size; `fivestar shader waves` lists which programs depend on it |
| f32 → f16 | `f32tof16_rne` (round to nearest even, as AMD) |
| FMA | `mad` (the prelude's exact `fma32` exists but is slow) |

**Mesh fallback contract** (register space 9, bound through a descriptor table):

| Binding | Content |
|---|---|
| CS `u0` | vertex records, `MSFB_STRIDE` bytes each (the Out struct packed in declaration order) |
| CS `u1` | uint32 triangle list |
| CS `u2` | `D3D12_DRAW_INDEXED_ARGUMENTS` + vertex counter: [0] index count, [1] instance count, [2..4] 0, [5] vertex count |
| VS `t0` | the vertex buffer of `u0`, read by `SV_VertexID` |

DispatchMesh(x, y, z) becomes: clear `u2` to {0, 1, 0, 0, 0, 0}; Dispatch(x, y, z); UAV barrier; ExecuteIndirect
DrawIndexed with arguments `u2` and index buffer `u1` (R32_UINT). A group that does not fit is dropped and its
counted index range is made degenerate; culled primitives become degenerate triangles.

## Runtime headers

`rdna_prelude.hlsli` (user SGPRs from root constants `g_user4`, memory and buffer access, formatted loads/stores,
cross-lane helpers, cube math, exact conversions) and `rdna_bvh.hlsli` are embedded in the assembly and written next
to the generated HLSL.

## Commands

```
fivestar shader translate <program.fssm | folder> <out> [--retry results.json] [--stages ps,vs,cs,ms,gs]
                          [--jobs N] [--limit N] [--step K] [--no-compile] [--structured] [--llvm <folder>]
                          [--dxc <path>]
```

Translates every `*.fssm` (in name order; `--step K` takes every K-th, `--limit N` the first N), writes
`<out>/<name>.hlsl`, compiles it to `<name>.dxil` and writes `<out>/results.json`. `--retry` re-runs only programs
whose status in that file is not final and merges the results. `--no-compile` skips dxc. `--structured` emits the
structured control-flow form (see Control flow).

dxc runs as `-T <stage>_6_7 -E main -HV 2021 -I <out> -Fo <name>.dxil`, with a 180 s limit; a timed-out compile is
retried with `-Od` for 300 s (900 s with `--retry`); fallback compiles have 600 s. dxc comes from `--dxc`, then
`FIVESTAR_DXC`, then the Windows SDK default when it exists, then `PATH`.

`results.json` is an array of `{"name", "stage", "status", "reason"}`:

| Status | Meaning |
|---|---|
| `ok` | translated and compiled |
| `ok-msfb` | mesh shader compiled through the fallback |
| `skip-stage` | stage unknown or not selected |
| `regs-fail` | FSSM header not decodable |
| `disasm-fail` | disassembly failed |
| `unsupported` | an instruction or shape the translator does not handle (reason: the instruction or detail) |
| `crash` | unexpected translator error |
| `dxc-fail` | dxc rejected the HLSL (reason: its first error) |
| `dxc-timeout` | dxc did not finish (reason: instruction count) |

```
fivestar shader waves <out> <fssm-folder>
```

Classifies every `ok` program of a run by wave size and dependence: wave-size agnostic, lane/EXEC bookkeeping only,
quad and first-lane operations only, or lane-indexed data movement (readlane, permlane, bpermute, DPP rows), which
breaks when a wave64 program runs on 32-lane waves or the reverse.

```
fivestar shader pso <out> [--adapter <index | name part>] [--stages ps,vs,cs,ms,gs] [--limit N] [--step K]
                    [--jobs N] [--timeout S] [--max-memory GB] [--debug-layer] [--recheck] [--dxc <path>]
fivestar shader pso --list-adapters
```

Creates a D3D12 pipeline state for every `ok` and `ok-msfb` program of `<out>/results.json` on one adapter (default:
the first hardware adapter) and records whether the runtime and driver accept it. This finds programs dxc compiles
but a driver refuses, such as a `[WaveSize(64)]` compute program on a GPU without 64-lane waves. `--list-adapters`
prints each adapter's shader model, `WaveLaneCountMin`-`Max` (D3D12_OPTIONS1) and mesh-shader tier.

| Kind | Pipeline |
|---|---|
| `cs`, `msfb_cs` | compute |
| `vs`, `msfb_vs` | VS only; input layout from ISG1 (slot 0, 32-bit formats of the element's type and width) |
| `ps` | generated pass-through VS + PS; targets from the SV_Target outputs as RGBA32 of their type (RGBA16 for min-precision), D32_FLOAT with SV_Depth, D32_FLOAT_S8X24_UINT with SV_StencilRef |
| `gs` | generated pass-through VS + GS, topology type from the GS input primitive |
| `ms` | mesh shader only (pipeline-state stream) |

Every pipeline has no culling, no blending, depth test off and one sample. The pass-through VS (`PassThroughVertexShader`)
declares the consumer's PSV0 input elements in their order with the same types, widths, rows and interpolation
modes, so dxc packs them into the same registers; it leaves out values the rasterizer generates and adds
SV_Position for a PS that does not read it. Each one is compiled once into `<out>/pso-vs/vs_<hash>.dxil`.

**Root signature** (`BindlessRootSignature`, root signature 1.1, flags
CBV_SRV_UAV_HEAP_DIRECTLY_INDEXED | SAMPLER_HEAP_DIRECTLY_INDEXED | ALLOW_INPUT_ASSEMBLER_INPUT_LAYOUT):

| Parameter | Content |
|---|---|
| 0 | root CBV `b0` space 0: `cbuffer UserData` (128 dwords, more than root constants allow) |
| 1 | descriptor table, space 9: UAV `u0`-`u2`, SRV `t0` (mesh fallback) |

Everything else goes through `ResourceDescriptorHeap[]` / `SamplerDescriptorHeap[]`.

Results are appended to `<out>/pso-<adapter name>.jsonl`, one line per pipeline as soon as it is known:
`{"name", "kind", "result", "hresult", "message", "wave", "milliseconds"}`. `wave` is the size a `cs` program pins.

| Result | Meaning |
|---|---|
| `accepted` | the driver created the pipeline state |
| `rejected` | refused: the HRESULT and the exception text, or the debug layer's errors with `--debug-layer` |
| `setup-fail` | no DXIL, or the pass-through VS did not compile |
| `timeout` | no result after `--timeout` seconds (default 600) |
| `over-memory` | the process passed `--max-memory` GB of private memory (default 16); logged for the longest-running pipeline |
| `crash` | the process ended during this pipeline in two runs |

A driver compile cannot be cancelled, so the limits end the process (exit code 3): once a pipeline is past the time
limit no new one starts, and the late ones are logged when nothing else runs; above the memory limit (checked every
second) the longest-running pipeline is logged at once. Pipelines still running for another reason are retried by the
next run. Driver compiles of the largest dispatch-form programs (2-6 MB of DXIL) take minutes and up to about 50 GB
each.

A run skips pipelines already in the log, so a run after an exit resumes; `--recheck` re-creates every pipeline whose
last result is not `accepted`, and the last line of a pipeline wins. `<out>/pso-<adapter name>.started` lists each
pipeline before creation. `--jobs N` creates N pipelines at once (`--debug-layer` uses 1 so each message belongs to one
pipeline); an E_OUTOFMEMORY under several jobs can be memory pressure, so recheck those with `--recheck --jobs 1`. The
driver's implicit shader cache is turned off for the run (`ID3D12Device9::ShaderCacheControl`, needs Windows developer
mode; a warning is printed otherwise), since tens of thousands of one-off pipelines would fill its disk cache. The run
ends with the peak private memory, counts per kind and result, the pinned compute programs per wave size and the
largest error groups (numbers in messages replaced by `#`).

## Testing

`tests/FiveStar.Shaders.Tests` assembles handwritten gfx1013 programs with llvm-mc, translates them, compiles them with
dxc and runs them on D3D12 WARP. Results are compared with a reference model of the ISA (`Reference/`), never with the
translator's output. Covered: pixel shaders (VALU, SALU, packed f16, cross-lane, EXEC, helper lanes, control flow,
memory, exports), compute (ids, LDS, wave32/wave64 wave operations, buffer and global memory, atomics), ray
intersection, the mesh path and its fallback (including overflow), vertex fetch, a handwritten GS and the real
VoxelizeGeneric GS programs.

Run `dotnet test tests/FiveStar.Shaders.Tests`. `FIVESTAR_LLVM` (LLVM bin folder) and `FIVESTAR_DXC` (dxc path) override
the default tool locations; the real-GS tests need `FIVESTAR_DUMP` and `FIVESTAR_SHADER_DIR` (folder of extracted
`.fssm`, containers in `../dxbc`). A test skips with the reason when a tool or WARP is missing. WARP runs 4-lane waves,
so cases that need a full 32/64-lane wave are skipped, and `v_fma_f32` is compared unfused (see FMA above). Test
classes run one at a time (`xunit.runner.json`): WARP crashes the test host when they run in parallel.

## Unknown

- The fetch-shader VGPR convention beyond what the fetch table states.
- PA_CL_VS_OUT_CNTL: a misc pos export (point size, layer, viewport) is passed as a plain `POSEXP<n>` interpolant.
- Typed views: one heap slot has one view type, but integer formats and image atomics need `uint` views.
- POPS ordering beyond what rasterizer-ordered views provide.
- Pipeline-time VS/PS linkage: producer outputs are emitted without the consumer's interpolation modes.
