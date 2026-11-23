# Roadmap

## Running the game

The executable is in hand; `fivestar exe analyze` reports its imports and GDK calls
([exe-intake.md](https://github.com/ThatRecompDev/five-star-recomp/blob/main/docs/exe-intake.md)) and
`fivestar-loader --run` boots it. In the order boot will need them:

| Item | State |
|---|---|
| GPU memory | next blocker. On the console a GPU address is the CPU address; the game places buffers in its own xmem pages. Plan: open the page sections as D3D12 heaps, place resources in them, and translate game addresses wherever they reach descriptors or commands ([d3d12x-shim.md](https://github.com/ThatRecompDev/five-star-recomp/blob/main/docs/d3d12x-shim.md)) |
| D3D12.X objects | queues, command lists, allocators, resources and heaps wrapped like the device, so console-only methods are reached and logged; the command-list methods are mapped from a run-time trace |
| Pipeline creation | the game's five pipeline-creation methods build desktop pipelines from the translated shaders and the console pipeline cache |
| Pipeline state | whole root signatures (the cache lists only the bindings each shader uses, with their user SGPRs) and the shader constants layout; blend, depth, raster state, render-target and depth formats and vertex formats already come from the driver's pipeline cache |
| Present | the queue's present and frame-event methods, on a swap chain for the game's own window |
| GDK | 65 of the 161 GDK functions the game calls have no shim yet: store, speech, package chunks, user tokens, certificate checks |
| Audio | the console's audio hardware reports itself unavailable; what the game falls back to is not known yet |
| Archive file table | names and exact entry boundaries stay encrypted; the game opens and reads its archives itself at run time |

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
| D3D12.X runtime | the game's device, compute queues, command lists and frame pacing run on desktop D3D12; adapters, the bindless root signature and pipeline creation exist; no command translation yet |
| Shader linking | real VS/MS+PS pairs link and build pipelines; values filled for inputs a producer never writes are not yet checked against real shading |
| Wave size | emulated or pinned; still open: 19 compute programs with several waves per group need 64-lane waves, 445 wave64 pixel and 1 vertex program read lanes a 32-lane GPU does not have, and 37 mesh programs that compile only through the mesh fallback need 64-lane waves ([details](Shader-Translation.md#wave-size)) |
| Structured shader control flow | the default since a differential check on real programs (see [Status](Status.md)) |
| Pipeline sweep | sampled runs done; a full run, and the AMD driver crashes on some pixel shaders, are open |
| Call overhead benchmarks | GDK calls measured (58 ns per call); command translation is not measured yet |
| First frame | textured models draw with a real shader pair; depth and the game's material shaders are next |
| Proton | test the Windows build on Linux and Steam Deck through Proton |

Platform and performance strategy: [Platforms and performance](Platforms-and-Performance.md).
