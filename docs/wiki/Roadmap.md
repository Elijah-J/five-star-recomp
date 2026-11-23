# Roadmap

## Running the game

The executable is in hand; `fivestar exe analyze` reports its imports and GDK calls
([exe-intake.md](https://github.com/ThatRecompDev/five-star-recomp/blob/main/docs/exe-intake.md)) and
`fivestar-loader --run` boots it. In the order boot will need them:

| Item | State |
|---|---|
| Device removal | next blocker: after the descriptor heaps an invalid call removes the desktop device (device slot 70 is the first unknown call); the D3D12 debug layer should name it |
| GPU memory | buffers at game-chosen addresses are placed in heaps opened over the game's own pages, and a table maps game GPU addresses to desktop ones. Open: translating those addresses where they reach commands and descriptors, ranges that span several page sections, and the main descriptor heap's 1,245,184 descriptors (the desktop limit is 1,000,000) ([d3d12x-shim.md](https://github.com/ThatRecompDev/five-star-recomp/blob/main/docs/d3d12x-shim.md)) |
| Command lists | the game writes most commands itself into the list's own buffer and calls a method only when it is full; about 50 of the list's methods are mapped to desktop ones; barriers, texture copies and the console-only methods are open |
| Pipeline creation | the game's five pipeline-creation methods (from its pipeline cache, from a state stream, compute, and derived variants) build desktop pipelines from the translated shaders; picking per-GPU program variants and slot 101 (ray-tracing state objects) are open ([d3d12x-pipelines.md](https://github.com/ThatRecompDev/five-star-recomp/blob/main/docs/d3d12x-pipelines.md)) |
| Pipeline state | whole root signatures (the cache lists only the bindings each shader uses, with their user SGPRs) and the shader constants layout; blend, depth, raster state, render-target and depth formats and vertex formats already come from the driver's pipeline cache |
| Present | the queue's present and frame-event methods, on a swap chain for the game's own window |
| Audio | the game mixes on the CPU and plays through WASAPI but leaves decoding to the console hardware: Opus (99.9% of sounds) now decodes through a CPU copy of the console's queue; an XMA2 decoder exists (below) but is not yet behind the console's SHAPE XMA contexts, whose protocol is still being traced ([audio-runtime.md](https://github.com/ThatRecompDev/five-star-recomp/blob/main/docs/audio-runtime.md)) |
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
| D3D12.X runtime | every object the game creates is a wrapper over a desktop object, with a registration API for console-only methods; no command translation yet |
| Shader linking | real VS/MS+PS pairs link and build pipelines; values filled for inputs a producer never writes are not yet checked against real shading |
| Wave size | emulated or pinned; still open: 19 compute programs with several waves per group need 64-lane waves, 445 wave64 pixel and 1 vertex program read lanes a 32-lane GPU does not have, and 37 mesh programs that compile only through the mesh fallback need 64-lane waves ([details](Shader-Translation.md#wave-size)) |
| Structured shader control flow | the default since a differential check on real programs (see [Status](Status.md)) |
| Pipeline sweep | sampled runs done; a full run, and the AMD driver crashes on some pixel shaders, are open |
| Call overhead benchmarks | GDK calls measured (58 ns per call); command translation is not measured yet |
| First frame | textured models draw with a real shader pair; depth and the game's material shaders are next |
| Proton | test the Windows build on Linux and Steam Deck through Proton |

Platform and performance strategy: [Platforms and performance](Platforms-and-Performance.md).
