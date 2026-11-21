# Shader translation

`src/FiveStar.Shaders/Translation` turns a native RDNA program (FSSM, see [fssm.md](fssm.md)) into HLSL for shader
model 6.7. Public entry: `HlslTranslator.Translate(ShaderProgram) -> TranslationResult`.

## Pipeline

1. **Load** (`ProgramLoader`): decode the FSSM header; disassemble the code with llvm-mc/llvm-objdump (gfx1013, wave64
   programs with `+wavefrontsize64`); read ISG1/OSG1/PSV0 from `<fssm folder>/../dxbc/<name>.dxbc` when present.
2. **Blocks** (`ControlFlowTranslator`): rewrite long jumps, split into basic blocks, drop unreachable blocks.
3. **Emitters** (`Translation/Emitters`): one emitter per instruction family turns each instruction into HLSL statements.
4. **Program** (`Translation/Stages`): structs, declarations, prologue, dispatch loop and output mapping.
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
| Wave size | AMD lane numbers are kept; `fivestar shader waves` lists which programs depend on them |
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
                          [--jobs N] [--limit N] [--step K] [--no-compile] [--llvm <folder>] [--dxc <path>]
```

Translates every `*.fssm` (in name order; `--step K` takes every K-th, `--limit N` the first N), writes
`<out>/<name>.hlsl`, compiles it to `<name>.dxil` and writes `<out>/results.json`. `--retry` re-runs only programs
whose status in that file is not final and merges the results. `--no-compile` skips dxc.

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

## Unknown

- The fetch-shader VGPR convention beyond what the fetch table states.
- PA_CL_VS_OUT_CNTL: a misc pos export (point size, layer, viewport) is passed as a plain `POSEXP<n>` interpolant.
- Typed views: one heap slot has one view type, but integer formats and image atomics need `uint` views.
- POPS ordering beyond what rasterizer-ordered views provide.
- Pipeline-time VS/PS linkage: producer outputs are emitted without the consumer's interpolation modes.
