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
`code/<shader index>.bin` (the raw code blocks) and `pipelines.json`: every graphics pipeline (input layout, slots with
shader index and program names), every compute pipeline (numthreads, shader) and every shader (program names, 0x41 and 0x42 entries).
A blob cut short (such as the `bin` entry `archive carve` writes) reads up to its end; shaders past it are skipped.

## Unknown

- Header 0x04 and 0x48, graphics record 0x10..0x37, compute record 0x0C.
- Compute state words 0x04, 0x14 and 0x18 (0x14 looks like COMPUTE_PGM_RSRC2 but differs from the FSSM header's).
- The fixed-function state entry (versions 0x4005D800, 0x4005D808, 0x4005D810, 380 to 520 bytes); it looks like
  hardware context registers (eight blend-control words), not decoded.
- Shader metadata header words 0x08 and 0x0C, 0x41 entry fields +0x10 and +0x14, 0x42 entries with low byte 2.
- Where the 0x41 byte offsets point (a user-data table of the second compile is likely).
- How a cache code block maps to FSSM register configuration; it has none of its own, so it is not a translator input.
- The programs of slot 6 and of the 20 shaders without an SGD5 match.
