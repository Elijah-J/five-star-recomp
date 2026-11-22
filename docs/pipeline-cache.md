# Pipeline cache

Besides its SGD5 tables ([shader-containers.md](shader-containers.md)), `shaders.rpf` holds one entry written by the
console's graphics driver: a cache of compiled pipelines. It names the shaders of every graphics and compute pipeline
the game builds, the vertex input layout of each graphics pipeline and the resource bindings of each shader, and holds
the driver's final machine code. Code: `src/FiveStar.Shaders/Cache`.

The entry is an ordinary RPF9 chunk run ([archives.md](archives.md)): the first aligned zlib stream whose output starts
with `PS 00 09`, read until the size the header gives. In the reference dump it starts at archive offset 0x10579000
and is 275,540,780 bytes long (4205 chunks).

All integers are little-endian.

## Layout

| Section | Size |
|---|---|
| Header | 0x80 |
| Graphics pipeline records | 0x70 × graphics count |
| Compute pipeline records | 0x18 × compute count |
| Metadata index | 8 × metadata count: u32 offset (from the metadata area), u32 size |
| Shader index | 8 × shader count: u32 offset (from the code area, 256-byte aligned), u32 size |
| Metadata area | metadata bytes |
| Code area | code bytes |

The sections follow each other without padding.

### Header

| Offset | Size | Field | Reference dump |
|---|---|---|---|
| 0x00 | 4 | `PS 00 09` | |
| 0x04 | 32 | ASCII, NUL-padded | `0x65F41EDD` |
| 0x24 | 36 | driver version, ASCII, NUL-padded | `930.048.020` |
| 0x48 | 8 | unknown | |
| 0x50 | 4 | graphics count | 25,471 |
| 0x54 | 4 | compute count | 2,693 |
| 0x58 | 4 | metadata count | 54,576 |
| 0x5C | 4 | shader count | 21,522 |
| 0x60 | 4 | metadata bytes | 31,120,020 |
| 0x64 | 4 | code bytes | 240,894,464 |
| 0x68 | 24 | zero | |

### Graphics pipeline record (0x70)

| Offset | Size | Field |
|---|---|---|
| 0x00 | 8 | hash, unique per record |
| 0x08 | 4 | metadata entry: fixed-function state |
| 0x0C | 4 | metadata entry: vertex input layout |
| 0x10 | 16 | unique per record |
| 0x20 | 16 | 110 distinct values |
| 0x30 | 8 | 93 distinct values |
| 0x38 | 7 × 8 | shader slots `{i32 shader index, i32 metadata entry}`, `-1, -1` when empty |

| Slot | Stage of its programs |
|---|---|
| 0 | pixel (FSSM stage 5) |
| 1 | vertex (stage 4) |
| 2 | mesh / NGG (stage 7) |
| 3, 4, 5 | never used |
| 6 | two pipelines; their shaders are not in the SGD5 tables |

Slot use in the reference dump: pixel + vertex 13,383, pixel + mesh 10,726, mesh only 1,336, vertex only 24, pixel +
vertex + slot 6 2.

### Compute pipeline record (0x18)

| Offset | Size | Field |
|---|---|---|
| 0x00 | 8 | hash |
| 0x08 | 4 | metadata entry: state (28 bytes, see below) |
| 0x0C | 4 | metadata entry: 20 bytes, a u32 hash then 3, 0, 0, 0 |
| 0x10 | 4 | shader index |
| 0x14 | 4 | metadata entry of the shader |

Each shader has exactly one metadata entry, whichever pipelines use it.

| Compute state | Size | Field |
|---|---|---|
| 0x00 | 4 | `0x4005CF00` |
| 0x0C | 2 + 2 | numthreads x, y |
| 0x10 | 2 | numthreads z |

The numthreads equal the PSV0 values of the shader's container for all 2,682 compute pipelines with an extracted
program.

### Fixed-function state

The graphics record's 0x08 entry is a fixed run of hardware context register values (names from AMD's RDNA2 register
reference). It starts with the u32 version (0x4005D800, 0x4005D808, 0x4005D810), then a layout kind: 4 for vertex
pipelines (380 bytes before the tail), 6 for mesh pipelines (464). The mesh layout holds 0x54 more bytes of NGG
registers ahead of the pixel-shader block, so from 0x58 on its offsets are 0x54 higher. The low byte of 0x0C is the
number of dwords in a tail after the fixed part (not decoded). Code: `GraphicsStateParser`, `FixedFunctionDecoder`.

| Vertex layout | Register | Evidence |
|---|---|---|
| 0x2C | VGT_GS_OUT_PRIM_TYPE | 2 (triangles) everywhere except the two slot-6 pipelines (1, lines): slot 6 is a geometry shader |
| 0x58 | SPI_PS_INPUT_ENA | equals the pixel shader's FSSM value in most pipelines |
| 0x68 | DB_SHADER_CONTROL | equals the FSSM value in 88% to 99.7% of each layout's pipelines |
| 0x70 | SPI_SHADER_COL_FORMAT | export formats, compacted over holes; 0 when no target is bound |
| 0x78 | CB_SHADER_MASK | the FSSM mask restricted to the bound targets |
| 0x9C | PA_CL_CLIP_CNTL | ZCLIP_NEAR/FAR_DISABLE on 960 pipelines, 939 of the 945 with programs are shadow-cascade passes |
| 0xA0 | PA_SU_SC_MODE_CNTL | front-face culling on 62 pipelines, 43 of them `*_BackFace` pixel shaders |
| 0xAC, 0xB0, 0xB4 | PA_SU_POLY_OFFSET_CLAMP, FRONT_SCALE, FRONT_OFFSET (floats) | non-zero on shadow and entity-mask passes |
| 0xB8 | DB_DEPTH_CONTROL | reverse-Z GEQUAL/GREATER in the main passes, LESS/LEQUAL in shadow and linear-depth passes |
| 0xBC | DB_STENCIL_CONTROL | stencil ops set when stencil writes are on (6 exceptions in 25,471) |
| 0xC0, 0xC4 | DB_STENCILREFMASK, DB_STENCILREFMASK_BF | |
| 0xCC | CB_TARGET_MASK | D3D write masks (0x88888888 alpha-only passes, 0 for colour-less passes) |
| 0xD0 | CB_BLEND0..7_CONTROL | 1,937 use SRC1 factors; all of those pixel shaders write SV_Target0 and SV_Target1 |
| 0xF4 | SX_PS_DOWNCONVERT | format class per written target |
| 0xF8, 0xFC | SX_BLEND_OPT_EPSILON, SX_BLEND_OPT_CONTROL | |
| 0x100 | SX_MRT0..7_BLEND_OPT | `06000600` (blend disabled) as often as CB_BLEND is disabled, per target |
| 0x120 | DB_EQAA | `00100000` except 94 UAV-only and conservative passes (16 samples, forced) |

Further checks on the reference dump: the colour exports equal the OSG1 SV_Target registers in 22,021 of 24,088
pipelines with a pixel shader (most others write targets the pipeline does not bind, and the driver drops those
exports); `uint` outputs always use 32-bit or UINT16 exports; pipelines without a pixel shader export nothing; no
pipeline whose pixel shader is named `*Opaque*` blends, while all `ptfx` and `Transparent` ones do.

`psocache --out` writes each graphics pipeline's `state` in D3D12 enum values: blend per target, render targets with
a DXGI format guess, depth-stencil, rasterizer, topology type, sample counts, and the raw registers.

| Field | Confidence |
|---|---|
| Blend factors, ops, enable, write mask; dual source | high |
| Depth enable/write/func, stencil enable, ops, funcs, masks | high |
| Cull mode, depth clip, fill mode (always solid) | high |
| Depth bias | medium: the registers are certain; slope = scale / 16 and bias = offset follow AMD's units |
| Render-target count | medium: one past the highest written, bound target; bound but unwritten targets are invisible |
| Render-target format | low: a family guess; UNORM vs SRGB, RGBA vs BGRA and float vs integer 32-bit are not stored |
| Topology type | medium: the rasterised primitive (triangles); a GS pipeline's input type is not stored |
| Sample count | medium: 1 everywhere; DB_EQAA shows MSAA only as forced counts |
| Front counter-clockwise | low: taken as the hardware's FACE bit (0 = CCW front), so almost every pipeline reads `true` |

## Shaders

A shader index entry is the shader's code block. It starts with the same 24 bytes as the code block of the FSSM
program of the same shader ([fssm.md](fssm.md)); the code after them is a second compile and differs (other user-data
loads, an extra 12-byte instruction after the prefix in most shaders). The 24-byte prefix links a cache shader to its
extracted programs: in the reference dump 21,502 of 21,522 shaders match, 25,448 of 25,471 graphics pipelines have a
program in every slot (13,362 vertex + pixel, 10,726 mesh + pixel) and 2,684 of 2,693 compute pipelines do.

The code is RDNA (gfx10) machine code. On a sample of 25 shaders `llvm-mc --mcpu=gfx1013` and `gfx1030` decode the
same instructions; they differ only inside the 24-byte prefix, which is data.

## Metadata

### Vertex input layout

| Offset | Size | Field |
|---|---|---|
| 0x00 | 4 | hash |
| 0x38 | 4 | bytes from 0x4C to the end of the elements |
| 0x4C | 4 | element count (0 for mesh pipelines) |
| 0x50 | | elements |

An element is `u32 name length (with the NUL), name, u32 semantic index, u32 DXGI format, u32 input slot, u32 byte
offset, u32 classification, u32 step rate` (the order of `D3D12_INPUT_ELEMENT_DESC`), packed without alignment; the
entry is padded to 4 bytes. For every vertex pipeline whose vertex shader is extracted (13,388), the elements equal the
non-system inputs of the shader's ISG1 signature.

### Shader metadata

| Offset | Size | Field |
|---|---|---|
| 0x00 | 4 | version: 2 graphics, 0x403 compute |
| 0x04 | 4 | size up to the end of the hash entry |
| 0x08 | | rest of the header (0x10 bytes in all for version 2, 0x18 for 0x403) |
| header end | 32 × n | entries tagged 0x41 or 0x42 |
| | 4 + 16 | tag 0x43, 16-byte hash |

| Tag 0x41 entry | Size | Field |
|---|---|---|
| +0x00 | 4 | 0x41 |
| +0x04 | 4 | kind: 0x300 or 0x100 SRV, 0x302 or 0x002 CBV, 0x303 UAV, 0x401 sampler |
| +0x08 | 4 | byte offset, a multiple of 8 |
| +0x0C | 4 | shader register |
| +0x10, +0x14 | 4 + 4 | unknown |
| +0x18 | 8 | `FFFFFFFF 00000000` |

| Tag 0x42 entry | Size | Field |
|---|---|---|
| +0x00 | 4 | 0x42 |
| +0x08 | 4 | low byte 1 or 2; byte 1 class (0 SRV, 1 UAV, 2 CBV, 3 sampler); high 16 bits the PSV0 resource kind |
| +0x0C | 4 | shader register |
| +0x10 | 4 | register space |
| +0x18 | 8 | `FFFFFFFF 00000000` |

The kinds and registers of the 0x41 entries agree with the PSV0 resource bindings of the shader's container.

## Command

```
fivestar shader psocache <shaders.rpf | cache blob> [--programs <fssm folder>] [--out <dir>]
```

Prints the header, the slot patterns, input-layout and binding statistics and, with a program folder (default
`FIVESTAR_SHADER_DIR`), how many shaders and pipelines resolve to extracted programs. `--out` writes
`code/<shader index>.bin` (the raw code blocks) and `pipelines.json`: every graphics pipeline (fixed-function state,
input layout, slots with shader index and program names), every compute pipeline (numthreads, shader) and every shader (program names, 0x41 and 0x42 entries).
A blob cut short (such as the `bin` entry `archive carve` writes) reads up to its end; shaders past it are skipped.

## Unknown

- Header 0x04 and 0x48, graphics record 0x10..0x37, compute record 0x0C.
- Compute state words 0x04, 0x14 and 0x18 (0x14 looks like COMPUTE_PGM_RSRC2 but differs from the FSSM header's).
- In the fixed-function state: the header words 0x08 to 0x28, the tail, the NGG block of the mesh layout and the
  words not in the table above; the depth-stencil format (no PA_SU_POLY_OFFSET_DB_FMT_CNTL or DXGI value is stored);
  the exact render-target formats; alpha-to-coverage, logic op, conservative rasterisation and the sample mask.
- Graphics record 0x20..0x37: hashes; neither half of 0x20 determines the render-target format class.
- Shader metadata header words 0x08 and 0x0C, 0x41 entry fields +0x10 and +0x14, 0x42 entries with low byte 2.
- Where the 0x41 byte offsets point (a user-data table of the second compile is likely).
- How a cache code block maps to FSSM register configuration; it has none of its own, so it is not a translator input.
- The programs of slot 6 and of the 20 shaders without an SGD5 match.
