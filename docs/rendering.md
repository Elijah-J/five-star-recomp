# Rendering

`src/FiveStar.Graphics/Rendering` holds the small D3D12 pieces that draw with translated programs:
`CommandContext` (queue, list, fence), `ResourceArena` (upload buffers, textures), `BindlessHeaps` (the two
shader-visible heaps), `RenderTarget` (offscreen target with readback) and `HardwareSampler` (RDNA S# to D3D12
sampler). `PipelineBuilder.Graphics(program, state)` takes blend, rasterizer and depth-stencil state.

## First frame

```
fivestar graphics draw-sample <out.png> [--dump <folder>] [--programs <fssm folder>] [--texture <file.ttpf>]
                              [--index N] [--adapter <index|name|warp>] [--work <dir>] [--debug-layer]
```

Draws a game texture with the game's own immediate-mode unlit pass, offscreen, and writes a PNG:

1. Loads the pipeline cache from `shaders.rpf` ([pipeline-cache.md](pipeline-cache.md)) and takes the first pipeline
   running `VS_TransformUnlit_IM` + `PS_TexturedUnlit_IM` (pipeline 0 in the reference dump).
2. Translates both programs to DXIL (as `shader translate`, into `--work`, default `<out>-shaders`).
3. Builds the pipeline with the bindless root signature and the cache's blend, rasterizer and depth state, input
   layout and render-target format (R8G8B8A8_UNORM).
4. Reads mip 0 of a texture (default `data/ttdb/ui/platform.ttpf` #0, BC7) and draws it twice: once at its own size,
   once as a strip with a red-to-blue vertex colour at half alpha, alpha-blended over dark grey.

`--debug-layer` reports D3D12 validation errors (exit code 1 when there are any). The test
`DrawSampleTests` renders an uncompressed texture on WARP and checks every pixel of the quad against texel over
clear colour.

Pipeline 0's state: blend SRC_ALPHA / INV_SRC_ALPHA on colour and alpha, back faces culled with
FrontCounterClockwise (the cache's low-confidence guess; the sample's quads are wound to suit it), depth off.

## Binding contract

What the runtime must provide for a translated program, as found for this pass:

| Item | Content |
|---|---|
| Root parameter 0 | CBV `b0` with the 128 user-data dwords (`g_user4`); user SGPR n is dword n |
| Heap slot 0 | `ByteAddressBuffer` over the GPU-memory window; a 32-bit pointer in user data is a byte address in it |
| Heap slot 1 | writable view of the window (only programs that store through pointers use it) |
| Any other slot | whatever a descriptor's dword 0 names |
| Sampler slot 2032 + k | the k-th literal S# of the program (`// static sampler k` comment in the HLSL) |
| Vertex input | translated vertex shaders read `ATTR0..n`; element n of the cache's input layout becomes `ATTR` n with the cache's format, slot and offset |

Both programs of the pass read only user SGPR s8: the low half of a pointer whose high half the code sets to 0. It
points at a table of descriptors:

| Table offset | Read by | Descriptor | Contents of the resource |
|---|---|---|---|
| 0x00 | PS | V# (4 dwords) | dword 0 scales alpha (squared by the code), dword 3 scales colour |
| 0x20 | VS | V# | 4x4 object matrix, row vectors: rows 0-2 multiply x, y, z, row 3 is added |
| 0x40 | PS | T# (8 dwords) | the texture |
| 0x60 | VS | V# | view constants; the 4x4 view-projection matrix (row vectors) at byte 0x160 |

The pixel shader samples with a literal S# `00000400 00c00000 18f00000 00000000`: wrap on all axes, 4x anisotropic,
linear mips, LOD 0 to 12. Output: `colour = texture.rgb * vertex.rgb * c[3]`, `alpha = texture.a * vertex.a * c[0]^2`.

The draw uses one table for both stages, since one root CBV serves every stage; their offsets do not overlap here.
On the console each stage has its own user-data registers, so another pass may need a table (or user data) per
stage. The values the sample puts in the tables are its own: constants that make the texture appear texel for texel.

The cache's shader metadata (tag 0x41 entries) lists the same registers (PS: s14 sampler, t13 buffer, t0 texture;
VS: t7 buffer, b0), but its byte offsets belong to the cache's second compile, not to the FSSM programs.

## Shader fixes this needed

- Compressed colour exports (`exp mrt0 v0, v0, v1, v1 compr`): LLVM prints the two packed registers as
  `src0, src0, src1, src1`; the translator read the first register twice, so blue and alpha were wrong in every
  pixel shader that exports 16-bit colour.
- VS / PS linkage: D3D12 links stages by semantic and register. Vertex shaders now declare their parameters in
  number order before the unpacked f16 copies, and pixel shaders declare every input register of their signature,
  read or not, with the f16 halves last.

## Models

```
fivestar graphics draw-model <out.png> --archive <file.rpf> --from 0xOFFSET [--name TEXT] [--lod N] [--yaw DEG]
                             [--pitch DEG] [--size N] [--plain] [--dump <folder>] [--programs <fssm folder>]
                             [--adapter <index|name|warp>] [--work <dir>] [--debug-layer]
```

Draws a drawable ([models.md](models.md)) with the same unlit pass:

1. Carves the archive from `--from` and takes the first drawable whose name contains `--name`.
2. Takes the geometries of level of detail `--lod` that use the shader with the most triangles, and finds that
   shader's `DiffuseTex` by its hash in the dump's `.ttpf` files. sRGB formats are read as their UNORM twins, so the
   stored values reach the UNORM target unchanged. Without a texture (or with `--plain`) every geometry is drawn on a
   1x1 white texture.
3. Turns the model by `--yaw` (about z, default 35) and `--pitch` (default 20) and fits it into a square target
   (`--size`, default 768) with an orthographic view.
4. The pass has no depth test, so the CPU drops back faces, sorts the rest far to near and winds them to suit the
   cache's front face. The vertex colour is a Lambert term from the decoded normals.
## Unknown

- What s0..s7 hold for these programs (they are not read).
- The meaning of the PS buffer's dwords 1 and 2 and of the view buffer before byte 0x160.
- Whether the console binds one table for both stages.
