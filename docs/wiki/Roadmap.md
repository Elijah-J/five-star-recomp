# Roadmap

## Running the game

The executable is in hand; `fivestar exe analyze` reports its imports and GDK calls
([exe-intake.md](https://github.com/ThatRecompDev/five-star-recomp/blob/main/docs/exe-intake.md)) and
`fivestar-loader --run` boots it. In the order boot will need them:

| Item | State |
|---|---|
| GPU hang | next blocker: right after the first present the GPU hangs and every later command list stops before its first command; it happens even with all draws and dispatches skipped, so the cause is outside binding (a clear on a render target still in the unordered-access state is the first suspect) |
| Descriptor binding | done for the first frames: descriptor heaps bind on command lists, the game's own GPU handles stay valid, and its root arguments reach the translated shaders as user data through a descriptor window; the last 245,446 of the main heap's 1,245,184 descriptors do not fit the desktop limit, and pointers into game memory from shaders are not yet translated |
| Pipeline state | pipeline creation is clean on the debug layer (about 71,000 errors fixed), and each program's wave32, superstep or native form is picked per GPU. Render-target and depth formats come from the cache itself (the console-only D16 depth-stencil format maps to a desktop one). Open: derive override kind 5, the mesh fallback on GPUs without mesh shaders, and 39 cached pipelines that enable depth without a depth format |
| Resources at game addresses | first version done: textures and views at the game's GPU addresses get desktop textures filled from game memory through the console tiling, refreshed when the game creates a view and the bytes changed; console samplers translate. Open: CPU writes after a texture's last view, synchronising uploads with the game's queues, releasing replaced textures, buffer views at game addresses, and barriers (the barrier method is not identified yet) ([gpu-resources.md](https://github.com/ThatRecompDev/five-star-recomp/blob/main/docs/gpu-resources.md)) |
| GPU memory | buffers at game-chosen addresses live in heaps opened over the game's own pages, as one buffer per pool, and a table maps game GPU addresses to desktop ones, including memory the game committed itself. Open: the main descriptor heap's 1,245,184 descriptors (the desktop limit is 1,000,000) ([d3d12x-shim.md](https://github.com/ThatRecompDev/five-star-recomp/blob/main/docs/d3d12x-shim.md)) |
| Command lists | the game writes most commands itself into the list's own buffer and calls a method only when it is full; about 50 of the list's methods are mapped to desktop ones; barriers, texture copies and the console-only methods are open |
| Pipeline creation | the game's five pipeline-creation methods (from its pipeline cache, from a state stream, compute, and derived variants) build desktop pipelines from the translated shaders; picking per-GPU program variants and slot 101 (ray-tracing state objects) are open ([d3d12x-pipelines.md](https://github.com/ThatRecompDev/five-star-recomp/blob/main/docs/d3d12x-pipelines.md)) |
| Pipeline state | whole root signatures (the cache lists only the bindings each shader uses, with their user SGPRs) and the shader constants layout; blend, depth, raster state, render-target and depth formats and vertex formats already come from the driver's pipeline cache |
| Present | done for one plane: the game's frames go to a flip-model swap chain on its own window (with HDR when the display supports it), and its frame pacing (30 fps at 60 Hz by default) runs on a timer; a second plane (likely a UI overlay) is not composited yet |
| Ray tracing | `XboxBvhBuilder.dll` needs console compute shaders, so its builds are skipped for now |
| Audio | the game mixes on the CPU and plays through WASAPI but leaves decoding to the console hardware: Opus (99.9% of sounds) now decodes through a CPU copy of the console's queue; XMA2 now decodes behind the console's SHAPE XMA contexts, whose layout and messages are traced (checked against our decoder on a real sound, not yet heard in a boot); the audio DSP effects return silence ([audio-runtime.md](https://github.com/ThatRecompDev/five-star-recomp/blob/main/docs/audio-runtime.md)) |
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
