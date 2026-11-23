# Shader translation

`src/FiveStar.Shaders/Translation` turns a native RDNA program (FSSM, see [fssm.md](fssm.md)) into HLSL for shader
model 6.7. Public entry: `HlslTranslator.Translate(ShaderProgram) -> TranslationResult`.

## Pipeline

1. **Load** (`ProgramLoader`): decode the FSSM header; disassemble the code (see Disassembly); read ISG1/OSG1/PSV0
   from `<fssm folder>/../dxbc/<name>.dxbc` when present.
2. **Blocks** (`ControlFlowTranslator`): rewrite long jumps, split into basic blocks, drop unreachable blocks.
3. **Emitters** (`Translation/Emitters`): one emitter per instruction family turns each instruction into HLSL statements.
4. **Program** (`Translation/Stages`, `Translation/Structure`): structs, declarations, prologue, structured control
   flow (or the dispatch loop) and output mapping.
5. **Compile** (`CorpusTranslator`, `DxcCompiler`): dxc, with the mesh fallback where needed or asked for;
   `TranslationCache` and `CachedCompiler` reuse earlier results (see Cache).

## Disassembly

`RdnaDecoder` (`src/FiveStar.Shaders/Disassembly`) decodes RDNA 2 code in-process. The emitters parse instruction
text, so the decoder prints every instruction exactly as llvm-objdump does for gfx1013 (wave64 lane masks as with
`+wavefrontsize64`), branch targets appended as `@T0x<target>`. It also keeps the listing's quirks: a dword that is no
instruction is `.long 0x...`, a run of 8 or more zero bytes is left out, and an instruction llvm-objdump lists with an
alignment warning is dropped, as the listing parser drops that line. Opcode numbers and field layouts come from AMD's
"RDNA 2 Instruction Set Architecture" reference guide.

`LlvmDisassembler` (llvm-mc wraps the bytes in an object file, llvm-objdump lists it, `ObjdumpParser` reads the
listing) is the reference: `fivestar shader decode-check` compares both on every program of a folder, and
`--llvm-disassembler` makes `disasm` and `translate` use it.

```
fivestar shader decode-check <fssm-folder> [--jobs N] [--limit N] [--step K] [--llvm <folder>]
```

Prints the program, code block and instruction counts and the first difference of each mismatched program; exit code
1 when any differ. On the 25,884 programs of the dump no program differs.

## Execution model

- One HLSL invocation is one GPU lane. Every SGPR and VGPR is a `uint` variable (`s4`, `v7`, `s_vcc`, `s_m0`, `s_exec`).
- EXEC is the per-lane bool `active`. A VALU result that only calls arithmetic and conversion functions is written
  `v = select(active, (uint)(value), v)`, straight-line code (`PureExpression`); other VALU and memory results are
  written `if (active)`; stores and atomics are guarded. EXEC stays a lane mask after an integer write (`active` is
  refreshed from the bits), so saving and combining it later is bool logic, not ballots.
- A lane mask held in SGPRs (v_cmp results, saved EXEC) is a per-lane bool `m_sN`; v_cmp writes `active && cond`.
- A lane mask used as an integer (`s_bcnt1`, `s_ff1`, stored, compared) is rebuilt with `WaveActiveBallot` /
  `WaveActiveCountBits`; an integer SGPR pair used as a mask is tested with `lane_bit(lo, hi)`.
- SCC is the bool `scc`, set by compares, logic, carries and shifts as the hardware does.
- Registers read before any write are hardware inputs, assigned in a prologue (rules in [fssm.md](fssm.md)); a read
  outside the input layout is 0.

## Control flow

Every branch condition is scalar (SCC) or a wave-wide reduction (`execz`/`vccz` become `WaveActiveAnyTrue`), so all
lanes of a wave follow the same path and divergence lives only in `active`. Block leaders are the entry, branch
targets and the instruction after a branch or an early `s_endpgm`; blocks not reachable from the entry (constant tables
after the last `s_endpgm`) are skipped. A program without branches is emitted as straight-line code.

**Dispatch form** (`--dispatch`, `TranslationOptions.StructuredControlFlow` false). Blocks become `case`s of a
`switch (pc)` inside `[loop] while (pc < PC_YIELD)`; each block ends by setting `pc` (`PC_END` after `s_endpgm`).

**Structured form** (the default). The blocks become nested statements:

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

Structured is the default since the control-flow differential (see Testing) ran both forms of real programs side by
side: their outputs are equal apart from fast-math rounding in dxc.

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

**Linkage.** D3D12 links a VS and a PS by semantic and register, and dxc assigns registers in declaration order. A VS
declares its params in number order, then the unpacked halves; a PS declares every non-system ISG1 register, read or
not, then `ATTRLO<k>`/`ATTRHI<k>` pairs from 0 up to the highest half it reads. A pair compiled together by the game
then links. A VS that does not export every param of its signature still shifts the later registers.

Translated on its own, a producer does not link with every PS it runs with. The runtime compares semantics,
registers and masks (not interpolation modes), and on the console cache's pairs it fails three ways: dxc never packs
elements of different interpolation modes into one register, and a PS gives its inputs modes (`nointerpolation` for
flat and integer reads) the producer lacks, so two outputs sharing a register sit in two in the PS; the PS declares
`SV_Position` after its halves while a VS declares it before; and a PS declares inputs the producer does not export
(signature registers it never writes, f16 halves of a producer with too many params).

**Pair-time linking** (`StageLinker.Link(producerHlsl, psInputs)`) rewrites the producer's `Out` struct (VS, MS or
mesh-fallback VS) from the PS's PSV0 input elements: the members the PS reads come first, in the PS's order and with
its modes, then the producer's other outputs. dxc packs a prefix the same way in both stages, so every input lands in
the PS's register; the PS's own layout stays as described above. A PS input the producer lacks is added: an
`ATTRLO<k>`/`ATTRHI<k>` half is computed from the members of param k (where the producer fills `o`, or at each export
of a mesh shader that writes its outputs directly), anything else is 0. `fivestar shader link` applies it to the pairs
of the console pipeline cache (see Commands).

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
| Mesh output over 32 KB (D3D12 PC limit), or a GPU without mesh shaders | rewritten as a compute shader plus a vertex-pulling vertex shader (`MeshComputeFallback`, files `.msfb_cs.hlsl` / `.msfb_vs.hlsl`); `--mesh-fallbacks` builds it for every mesh program |
| HLSL over 2,500,000 characters | compiled with `-Od` (dxc's optimizer needs minutes on the largest programs) |
| Wave size | AMD lane numbers are kept. Pixel, vertex and mesh programs emulate the console wave on any hardware wave size (see Wave size). A compute program that reads other lanes by number gets `[WaveSize(32 or 64)]` (`TranslationOptions.PinWaveSize`, on in `fivestar shader translate`), so it fails to load on a GPU without that wave size instead of computing wrong results |
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

GPUs without mesh shaders need the fallback for every mesh program (`--mesh-fallbacks`). All 8,765 build it (structured
form). On an AMD iGPU without mesh-shader support the driver accepted every fallback pair of a 697-program sample; the
largest compute halves need up to 16.6 GB of driver memory, so sweep them with `--jobs 1`.

## Wave size

A console program runs in waves of 32 or 64 lanes (the FSSM header says which) and names lanes by number: readlane,
writelane, permlane, DPP, ds_swizzle, ds_bpermute, lane masks, mbcnt. A PC GPU runs waves of 4 to 128 lanes, chosen by
the driver. Compute shaders can ask for a size; pixel, vertex and mesh shaders cannot in shader model 6.7.

Translated code therefore uses console (program) lane numbers through the prelude's `vw_lane`, `vw_read`,
`vw_ballot`, `vw_any` and `vw_count`; `readfirstlane` and `lane_bit` build on them. With
`TranslationOptions.EmulateWaveSize` (the default; `--native-waves` turns it off) a pixel, vertex or mesh program
starts with `#define VW_WAVE <32|64>` and `VW_EMULATE 1`, and each hardware wave of H lanes holds a window of the
program wave of W lanes:

| Hardware wave | Mapping | Result |
|---|---|---|
| H = W | the native operations | console results |
| H > W | every aligned W-lane segment is one program wave: reads add the segment's first lane, masks are cut out of the ballot, `any`, `count` and branch conditions are per segment | console results |
| H < W, pixel and vertex | the hardware wave is program lanes 0 .. H-1, a partly filled console wave; a lane at H or above is missing and reads 0 | console results when no read leaves aligned groups of H lanes |
| H < W, mesh | the hardware wave is program lanes k·H .. k·H+H-1 of the wave (64 consecutive threads, `VW_GROUP_LANES`) | reads inside the window are right; masks, counts, branch conditions and first lanes see only the window, while the console sees all 64 live threads |

The choice between paths depends only on `WaveGetLaneCount()`, which is a constant once the driver compiles the
program, so a GPU runs one path. The DXIL still holds every path: on a sample of 99 lane-indexed programs it is 14 %
larger than with `--native-waves`.

Each emulated program's first line names its floor, the narrowest hardware wave with console results:

```
// program wave 64; console results on hardware waves >= 16 lanes
```

The floor is the widest aligned lane group a cross-lane read reaches: quads 4, `row_half_mirror` and DPP8 8, DPP rows and
`v_permlane16` 16, `v_permlanex16` and `BITMASK_PERM` 32, `SWAP`/`REVERSE`/`BROADCAST` their group, `v_readlane` of a
constant lane c the next power of two above c, of a lane found in a lane mask (`s_ff1`) 1, of any other lane
(`v_readlane` from an SGPR, `ds_bpermute`, `ds_permute`) the whole wave. A mesh program that reads a lane mask, a first
lane or branches on any lane has the whole wave as floor.

**Compute programs** keep native wave operations and are pinned. A wave64 program whose group has at most 32 threads
is pinned to 32 lanes instead, since lanes 32..63 never exist. For a program pinned to 64 lanes, which GPUs with only
32-lane waves (NVIDIA) reject, `fivestar shader translate` also writes `<name>.w32.hlsl`/`.dxil`
(`HlslTranslator.GroupWaveVariant`) when the thread group fits in one console wave: the group runs as the wave
(`VW_GROUP_WAVE`), every wave operation stores to groupshared memory, passes one barrier and reads back (lane masks
with `InterlockedOr`), so it is right on any hardware wave. A barrier needs every thread of the group, which holds
because each branch condition is itself such an operation and so is the same on every thread; a program that calls
one under a per-lane condition (`if (active)`, `&&`, `||`, `?:` before the call) gets no variant. The variant is built
from the structured form, since WARP's shader compiler crashes on barriers inside the dispatch `switch`. The result's
reason says `wave32 variant ok` or why there is none. Groups of several console waves (more than 64 threads)
need a GPU with 64-lane waves: their waves branch apart, so a group barrier cannot stand in for a wave.

**Variant selection** (`FiveStar.Graphics.Programs.VariantSelector`). `fivestar shader translate` writes
`<out>/variants.json` (`ShaderManifest`): per compiled program its stage, native DXIL, console wave, floor, pinned
wave size, wave32 variant and mesh fallback, so a runtime picks the DXIL without reading HLSL. For an adapter
(`AdapterInfo`):

| Program | Runs | Outcome |
|---|---|---|
| any, adapter below shader model 6.7 | nothing | incorrect |
| cs, not pinned or pin within `WaveLaneCountMin`-`Max` | native | ok |
| cs, pin outside, with a wave32 variant | `<name>.w32.dxil` | variant |
| cs, pin outside, no variant | native (fails to load) | incorrect |
| ms, adapter with mesh shaders | native, or the fallback when only that compiled | ok / fallback |
| ms, adapter without mesh shaders | the fallback (`--mesh-fallbacks`), else native (fails) | fallback / incorrect |
| ps, vs, gs, ms with a floor above `WaveLaneCountMin` | as above | incorrect |

The driver picks a graphics wave anywhere between `WaveLaneCountMin` and `Max`, so a floor counts only as met when
`WaveLaneCountMin` reaches it. `fivestar shader plan` prints the counts per adapter and `fivestar shader pso` creates
the form each program runs on its adapter.

Corpus (lane-indexed programs, by floor):

| Programs | 4 | 8 | 16 | 32 | 64 |
|---|---|---|---|---|---|
| ps wave32 (959) | 31 | 1 | 2 | 925 | |
| ps wave64 (907) | 368 | 41 | 49 | 4 | 445 |
| vs wave64 (1) | | | | | 1 |
| ms wave64 (115) | | | | | 115 |

All 1,982 compile (115 mesh programs, 15 of them through the fallback). Of the 203 compute programs pinned to 64 lanes,
3 have groups of at most 32 threads and are pinned to 32, 181 get a variant that NVIDIA (RTX 4090) accepts, and 19
have groups of 128 to 512 threads.

What cannot be emulated:

- Pixel and vertex waves narrower than the program wave cannot read a missing lane: those stages have no groupshared
  memory and no barrier, so the value exists in no wave that could share it. Example: a wave64 reduction that ends with
  `v_readlane 32` or `63` on a 32-lane GPU.
- Mesh waves narrower than the program wave: one console wave spans several hardware waves. Every lane mask, branch
  condition and cross-lane read would have to go through groupshared memory with a barrier, but a barrier needs the
  whole thread group at the same point, and the two console waves of a 128-thread group branch independently. It would
  take either supersteps (run every thread to its next wave operation, barrier, exchange, repeat) or invocations that
  each run W/H lanes; neither is built.
- Mesh lane numbers assume `SV_GroupIndex` fills hardware lanes in order. Drivers do this; D3D12 does not promise it.

## Runtime headers

`rdna_prelude.hlsli` (user SGPRs from root constants `g_user4`, memory and buffer access, formatted loads/stores,
cross-lane helpers, cube math, exact conversions) and `rdna_bvh.hlsli` are embedded in the assembly and written next
to the generated HLSL.

dxc inlines every helper at every call, so a helper that one instruction calls has no loops and few branches: `FmtLoad`
picks its fields from a table with `select`, and the BVH box node handles its four children one call each. dxc unrolls
an `[unroll]` loop by rebuilding analyses of the whole function, so a few thousand inlined loops in a large program
cost tens of minutes.

## Commands

```
fivestar shader translate <program.fssm | folder> <out> [--cache <dir>] [--retry results.json]
                          [--stages ps,vs,cs,ms,gs] [--jobs N] [--limit N] [--step K] [--no-compile] [--dispatch]
                          [--native-waves] [--mesh-fallbacks] [--llvm-disassembler] [--llvm <folder>] [--dxc <path>]
```

Translates every `*.fssm` (in name order; `--step K` takes every K-th, `--limit N` the first N), writes
`<out>/<name>.hlsl`, compiles it to `<name>.dxil` and writes `<out>/results.json`. `--cache` keeps translations and
DXIL between runs (see Cache). `--retry` re-runs only programs whose status in that file is not final and merges the
results. `--no-compile` skips dxc. `--dispatch` emits the dispatch form instead of structured control flow (see Control
flow). `--mesh-fallbacks` builds the mesh fallback for every mesh program that compiles natively too, for GPUs without
mesh shaders; its reason in results.json is `fallback ok` or the fallback's error.
`--native-waves` turns wave emulation off (see Wave size). A compute program pinned to 64 lanes also gets
`<name>.w32.hlsl`/`.dxil` where possible; its reason in results.json says so. `--mesh-fallbacks` also compiles the
mesh fallback of every mesh program that compiles (reason `fallback ok`), for GPUs without mesh shaders. A compiling
run ends by writing `variants.json` (see Variant selection). `--llvm-disassembler` disassembles with
llvm-mc/llvm-objdump instead of `RdnaDecoder` (see Disassembly). Every 10 seconds the command prints programs done,
elapsed time, estimated time left and finish time, and the cache counts.

dxc runs as `dxc.exe -T <stage>_6_7 -E main -HV 2021 -I <out> -Fo <name>.dxil`, one process per compile, with a 180 s
limit; a timed-out compile is retried with `-Od` for 300 s (900 s with `--retry`); fallback compiles have 600 s.
Sources over 2,500,000 characters use `-Od` from the start. dxc comes from `--dxc`, then `FIVESTAR_DXC`, then the
Windows SDK default when it exists, then `PATH`. Compiling in-process through `dxcompiler.dll` was measured no faster
(a dxc.exe start costs about 10 ms against a median compile of 0.85 s), and a process can be killed at the limit
while an in-process crash or allocation failure ends the whole run.

`results.json` is an array of `{"name", "stage", "status", "reason"}`:

| Status | Meaning |
|---|---|
| `ok` | translated and compiled |
| `ok-msfb` | mesh shader compiled through the fallback (dxc rejected the native one) |
| `skip-stage` | stage unknown or not selected |
| `regs-fail` | FSSM header not decodable |
| `disasm-fail` | disassembly failed |
| `unsupported` | an instruction or shape the translator does not handle (reason: the instruction or detail) |
| `crash` | unexpected translator error |
| `dxc-fail` | dxc rejected the HLSL (reason: its first error) |
| `dxc-timeout` | dxc did not finish (reason: instruction count) |

### Cache

`--cache <dir>` keeps work between runs, so a re-run only redoes what changed. `fivestar setup` uses
`<work folder>/shader-cache` (see [setup.md](setup.md)). Every entry is Brotli-compressed at
`<dir>/<kind>/<key[0..2]>/<key><extension>` and written through a temporary file, so an interrupted run keeps every
finished entry. Keys are SHA-256 hashes:

| Kind | Key | Entry |
|---|---|---|
| `programs/*.json` | FiveStar.Shaders.dll (with `--llvm-disassembler` also the llvm-mc and llvm-objdump bytes), the translation options, the FSSM and container bytes | stage, status, reason, instruction count, HLSL key (programs that translated, were unsupported or crashed) |
| `hlsl/*.hlsl` | the HLSL text | the HLSL |
| `dxil/*.dxil` | dxc.exe, dxcompiler.dll and dxil.dll bytes, the fixed dxc arguments, the runtime headers, profile, `-Od`, the HLSL text | the DXIL |
| `dxil/*.error` | same | dxc's messages of a failed compile with a source diagnostic (`file:line[:column]: error:`) or `error: validation errors`; crashes and out-of-memory failures are not kept |
| `dxil/*.timeout` | same | the time limit the compile ran into; reused for limits up to that one, so `--retry` (900 s) tries again |

Programs with the same HLSL compile once, also within one run. Any new build of FiveStar.Shaders invalidates the
translations (all programs are translated again) but only programs whose HLSL changed are compiled again. Each run lists the entries it used in
`used/<identity>.txt`; `fivestar setup <dump> --clean-cache` removes the entries of older builds (see
[setup.md](setup.md#shader-cache)). Deleting the folder resets it.

```
fivestar shader waves <out> <fssm-folder>
```

Classifies every `ok` program of a run by wave size and dependence: wave-size agnostic, lane/EXEC bookkeeping only,
quad and first-lane operations only, or lane-indexed data movement (readlane, permlane, bpermute, DPP rows), which
breaks when a wave64 program runs on 32-lane waves or the reverse. For a run with wave emulation it also counts the
lane-indexed programs by floor (see Wave size).

```
fivestar shader pso <out> [--adapter <index | name part>] [--stages ps,vs,cs,ms,gs] [--limit N] [--step K]
                    [--jobs N] [--timeout S] [--max-memory GB] [--debug-layer] [--recheck] [--dxc <path>]
fivestar shader pso --list-adapters
```

Creates a D3D12 pipeline state for every `ok` and `ok-msfb` program of `<out>/results.json` on one adapter (default:
the first hardware adapter) and records whether the runtime and driver accept it. Each program is built in the form
the adapter runs (see Variant selection): a pinned compute program the adapter cannot run as its wave32 variant
(kind `cs_w32`), a mesh program on an adapter without mesh shaders as its fallback. This finds programs dxc compiles
but a driver refuses, such as a `[WaveSize(64)]` compute program without a variant on a GPU without 64-lane waves. `--list-adapters`
prints each adapter's shader model, `WaveLaneCountMin`-`Max` (D3D12_OPTIONS1) and mesh-shader tier.

| Kind | Pipeline |
|---|---|
| `cs`, `cs_w32`, `msfb_cs` | compute |
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

```
fivestar shader plan <out> [--adapter <index | name part>]
```

Prints, for every adapter with a D3D12 device (or the chosen one), how many compiled programs run natively (`ok`), as
a wave32 variant, through the mesh fallback or incorrectly, and the most common reasons. It reads `variants.json` and
builds it from `results.json` when that is newer.

```
fivestar shader link <out> <shaders.rpf | cache blob> [--programs <fssm folder>] [--adapter <index | name part>]
                     [--limit N] [--step K] [--jobs N] [--timeout S] [--max-memory GB] [--debug-layer] [--unlinked]
                     [--recheck]
```

Takes the distinct VS+PS and MS+PS pairs of the console pipeline cache ([pipeline-cache.md](pipeline-cache.md))
whose programs compiled in `<out>`, links each producer to its PS (see Linkage) and creates the pipeline on the
adapter: `vs+ps`, `ms+ps`, or `msfb_vs+ps` where the adapter runs the mesh fallback. A producer the link changes is
compiled once into `<out>/linked/<name>.<hash>.dxil`. `--unlinked` creates the pairs with the producers as translated,
for comparison. Results go to `<out>/link-<adapter>.jsonl` (`unlinked-<adapter>.jsonl`) with the fields, limits and
resume rules of `pso`. A cache shader that matches several programs (same code) stands for the first compiled one.

## Testing

`tests/FiveStar.Shaders.Tests` assembles handwritten gfx1013 programs with llvm-mc (the tests and `decode-check` are
the only users of LLVM), decodes them with `RdnaDecoder`, translates them (structured form), compiles them with dxc and
runs them on D3D12 WARP. Results are compared with a reference model of the ISA (`Reference/`), never with the
translator's output. Covered: pixel shaders (VALU, SALU, packed f16, cross-lane, EXEC, helper lanes, control flow,
memory, exports), compute (ids, LDS, wave32/wave64 wave operations, buffer and global memory, atomics), ray
intersection, the mesh path and its fallback (including overflow), vertex fetch, a handwritten GS and the real
VoxelizeGeneric GS programs.

Run `dotnet test tests/FiveStar.Shaders.Tests`. `FIVESTAR_LLVM` (LLVM bin folder) and `FIVESTAR_DXC` (dxc path) override
the default tool locations; the real-GS tests and the decoder comparison on real programs need `FIVESTAR_DUMP` and
`FIVESTAR_SHADER_DIR` (folder of extracted `.fssm`, containers in `../dxbc`). A test skips with the reason when a tool
or WARP is missing. WARP runs 4-lane waves only (a pipeline with any other `[WaveSize]` fails to create), so cases
that need a full 32/64-lane wave are skipped, and `v_fma_f32` is compared unfused (see FMA above). The wave emulation
is checked there with test-sized program waves of 2, 4, 16 and 64 lanes (wider, equal and narrower hardware waves), by
the mesh program, which reads lanes by console number, and by running every wave64 compute case as its group-wave
variant with full-wave expectations (including the two full-wave cases WARP otherwise skips). Pair-time linking is
checked with a VS whose two float2 outputs share a register while the PS reads one of them flat: unlinked the
pipeline fails, linked it draws the flat value of the provoking vertex. Test
classes run one at a time (`xunit.runner.json`): WARP crashes the test host when they run in parallel.

**Control-flow differential** (`ControlFlowDifferentialTests`, `Differential/`). Real programs run in both forms on WARP
with the same synthetic inputs, and every output word must be equal. The harness redirects each descriptor-heap index a
program computes to a slot of the declared resource type (two per type) and each sampler index to one of eight samplers,
and fills user SGPRs, a 64 KB memory window, buffers and textures with small integers, offsets into the window and
floats in [-2, 2]. Every loop gets an iteration budget, since synthetic data can make a search or BVH walk endless.
Compute programs run as they are (one group); a pixel shader runs behind a wrapper that writes each output's raw bits
to its own uint target (16×16, each input a constant per triangle corner); a vertex shader runs in a compute wrapper
that stores the Out struct of 64 vertices; a mesh shader runs as its compute fallback (vertex records, indices, draw
arguments). Each program runs with three input sets; a form that differs from itself on a rerun is a race, not a
mismatch. `FIVESTAR_DIFF_PROGRAMS` (per stage, default 3), `FIVESTAR_DIFF_FIRST`, `FIVESTAR_DIFF_STAGE` and
`FIVESTAR_DIFF_OUT` (keeps the shaders and writes `differential.jsonl`) scale it up.

Run on 1,390 programs (the first of each stage in a fixed hash order of the names):

| Stage | Sampled | Equal | Same text | Not compared |
|---|---|---|---|---|
| Compute | 420 | 94 | 24 | 6 races; 267 large, 27 with a group barrier, 2 over the loop budget |
| Pixel | 330 | 108 | 54 | 9 WARP stalls; 155 large, 4 over the loop budget |
| Vertex | 320 | 122 | 146 | 1 fast-math difference, 2 WARP stalls; 49 large |
| Mesh | 320 | 91 | | 229 large |

"Same text": the program has no branches, so both forms are one text. Of the 415 equal programs 362 wrote at least one
output word; the others discard every pixel or skip their work on synthetic data. No difference came from the
translator:

- Fast-math (1 vertex shader): 64 words 1 ulp apart. dxc's default float optimizations (reassociation, contraction)
  treat the two code shapes differently; compiled with `-Gis` (IEEE strict) both forms agree, on WARP and on an RTX
  4090. The dispatch form is no more exact than the structured one.
- Races (6 compute programs): lanes of one console wave share LDS without a barrier. On WARP's 4-lane waves that is a
  race between waves, and each form differs from itself between runs; on an RTX 4090 both forms agree.
- WARP stalls (9 pixel, 2 vertex shaders): WARP does not finish running the dxc-optimized structured form (one compiled
  with `-Od` runs in 14 s). On an RTX 4090 both forms run in milliseconds and agree.

Not compared: dispatch-form HLSL over 64,000 characters (WARP's compiler needs minutes and many GB: 90 KB took over
10 minutes and 13 GB), compute programs with a group barrier (WARP's compiler crashes on a barrier inside the dispatch
`switch`) and geometry shaders (no harness).

## Unknown

- The fetch-shader VGPR convention beyond what the fetch table states.
- PA_CL_VS_OUT_CNTL: a misc pos export (point size, layer, viewport) is passed as a plain `POSEXP<n>` interpolant.
- Typed views: one heap slot has one view type, but integer formats and image atomics need `uint` views.
- POPS ordering beyond what rasterizer-ordered views provide.
- Which interpolation modes a producer needs when no pipeline of the cache names its PS: without a pair it is emitted
  without modes.
