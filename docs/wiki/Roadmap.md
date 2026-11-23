# Roadmap

## Needs the executable

When the executable arrives, run `fivestar exe analyze` first: it reports the items below from the real imports and
call sites. See [exe-intake.md](https://github.com/ThatRecompDev/five-star-recomp/blob/main/docs/exe-intake.md).

| Item | Why |
|---|---|
| Archive file table | names and exact entry boundaries are encrypted; the key and algorithm are in the executable |
| Import list | the exact set of console APIs the game calls |
| D3D12.X usage | how the game drives the GPU: device calls, descriptors, command lists |
| Pipeline state | whole root signatures (the cache lists only the bindings each shader uses, with their user SGPRs) and the shader constants layout; blend, depth, raster state, render-target and depth formats and vertex formats already come from the driver's pipeline cache |
| GDK call table | the PC GDK names 365 of the 408 GDK calls in the package's libraries; which ones the game calls needs its call sites |

## Research

| Item | State |
|---|---|
| BCPack | BC1 decodes (all 102,725 pages); BC7 (5,167 pages) and BC3, BC4, BC5, BC6H (713 pages) are open ([details](Textures-and-BCPack.md)) |
| Type names | PSO structures are identified by hashes; naming them needs lists of engine class names |
| Geometry | drawables decode (meshlets, packed positions, normals, texture coordinates); skinning, the other vertex streams and the meshlet culling data are not ([details](https://github.com/ThatRecompDev/five-star-recomp/blob/main/docs/models.md)) |
| BVH data | ray tracing needs acceleration structures in AMD's layout ([details](Shader-Translation.md#ray-tracing)) |

## Engineering

| Item | State |
|---|---|
| D3D12.X runtime | adapters, device, the bindless root signature and pipeline creation exist; no command translation yet |
| Shader linking | real VS/MS+PS pairs link and build pipelines; values filled for inputs a producer never writes are not yet checked against real shading |
| Wave size | emulated or pinned; still open: 19 compute programs with several waves per group need 64-lane waves, 445 wave64 pixel and 1 vertex program read lanes a 32-lane GPU does not have, and 37 mesh programs that compile only through the mesh fallback need 64-lane waves ([details](Shader-Translation.md#wave-size)) |
| Structured shader control flow | the default since a differential check on real programs (see [Status](Status.md)) |
| Pipeline sweep | sampled runs done; a full run, and the AMD driver crashes on some pixel shaders, are open |
| Call overhead benchmarks | GDK calls measured (58 ns per call); command translation needs the executable |
| First frame | textured models draw with a real shader pair; depth and the game's material shaders are next |
| Proton | test the Windows build on Linux and Steam Deck through Proton |

Platform and performance strategy: [Platforms and performance](Platforms-and-Performance.md).
