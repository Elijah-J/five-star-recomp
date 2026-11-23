# Roadmap

## Running the game

The executable is in hand; `fivestar exe analyze` reports its imports and GDK calls
([exe-intake.md](https://github.com/ThatRecompDev/five-star-recomp/blob/main/docs/exe-intake.md)) and
`fivestar-loader --run` boots it. In the order boot will need them:

| Item | State |
|---|---|
| Black frames | next: the game runs its frame loop but every frame is black, and each frame draws only two blits, so the game is still in an early state (a splash or loading screen) rather than rendering a scene. The black is the game's own: the first blit fills a target with a colour from a constant buffer that is still zero, and the second composites it onto the frame, so this looks like a fade from black waiting on the game's state. What it waits for is still open: its start-up loading completes without failures (about 8,300 DirectStorage requests, 53,600 pipelines) and it adds the default user, then idles without polling input, opening a movie or touching saves; the default user is added correctly, but the game's main frame is frozen from about 10 s in: it waits for four deferred jobs, apparently pipeline creations, that never finish (the shim fails 66 of the game's pipeline creations), so the per-frame update that pairs a gamepad with the user, fills the active player and starts saves never runs; fences, frame pacing, queries and network connectivity are ruled out (reporting internet access, with all traffic sealed, changes nothing) (presents are steady at 33.3 ms once the driver has compiled the game's pipelines, and the game reads no query results in this state). `--sample-threads`, `--profile-threads` and `--input-script` help find out more. Done on the way: the console's rectangle lists are drawn in full through a generated geometry shader (`fivestar shader rect-gs`); resource states are now handled (every resource rests in a fixed state between command lists, and the game's barriers, which go through the enhanced `Barrier` call on list slot 146, are translated), so states are no longer reported as wrong |
| Frame loop stability | done: two-minute runs have no crashes and exit cleanly, at the game's own 30 fps. The random crashes were heap corruption from the loader's TLS host (every new thread wrote 8 bytes past its TLS block); command lists the game resubmits while still running are now waited for; frame events pace the game as on the console |
| Descriptor binding | descriptor heaps bind on command lists, the game's own GPU handles stay valid, and its root arguments reach the translated shaders as user data through a descriptor window. All 1,245,184 descriptors of the main heap are usable through a sparse page remap (the game touches few pages), and the per-draw tables the game builds by copying descriptor bytes itself reach the shaders. Open: pointers into game memory from shaders |
| Pipeline state | pipeline creation is clean on the debug layer (about 71,000 errors fixed), and each program's wave32, superstep or native form is picked per GPU. Render-target and depth formats come from the cache itself (the console-only D16 depth-stencil format maps to a desktop one). Open: derive override kind 5, the mesh fallback on GPUs without mesh shaders, and 39 cached pipelines that enable depth without a depth format |
| Resources at game addresses | first version done: textures and views at the game's GPU addresses get desktop textures filled from game memory through the console tiling, refreshed when the game creates a view and the bytes changed; console samplers translate. Open: CPU writes after a texture's last view, synchronising uploads with the game's queues, releasing replaced textures, buffer views at game addresses, and barriers (the barrier method is not identified yet) ([gpu-resources.md](https://github.com/ThatRecompDev/five-star-recomp/blob/main/docs/gpu-resources.md)) |
| GPU memory | buffers at game-chosen addresses live in heaps opened over the game's own pages, as one buffer per pool, and a table maps game GPU addresses to desktop ones, including memory the game committed itself. Open: the main descriptor heap's 1,245,184 descriptors (the desktop limit is 1,000,000) ([d3d12x-shim.md](https://github.com/ThatRecompDev/five-star-recomp/blob/main/docs/d3d12x-shim.md)) |
| Command lists | the game writes most commands itself into the list's own buffer and calls a method only when it is full; about 50 of the list's methods are mapped to desktop ones; barriers, texture copies and the console-only methods are open |
| Pipeline creation | the game's five pipeline-creation methods (from its pipeline cache, from a state stream, compute, and derived variants) build desktop pipelines from the translated shaders; picking per-GPU program variants and slot 101 (ray-tracing state objects) are open ([d3d12x-pipelines.md](https://github.com/ThatRecompDev/five-star-recomp/blob/main/docs/d3d12x-pipelines.md)) |
| Pipeline state | whole root signatures (the cache lists only the bindings each shader uses, with their user SGPRs) and the shader constants layout; blend, depth, raster state, render-target and depth formats and vertex formats already come from the driver's pipeline cache |
| Present | done for one plane: the game's frames go to a flip-model swap chain on its own window (with HDR when the display supports it), and its frame pacing (30 fps at 60 Hz by default) runs on a timer; a second plane (likely a UI overlay) is not composited yet |
| Ray tracing | behind `--raytracing`: the console's structure sizes, and the game's precomputed bottom-level structures from `core_bvh4.rpf` rebuilt into game memory in AMD's layout through `XboxBvhBuilder.dll`'s own CPU path; runtime bottom-level builds and compacting copies run on the CPU from the game's own vertex and index buffers. Open: the top-level structure (the game builds it through the same batched call, but its instance-node layout is only partly known), and whether the game's DXR pipeline is ever dispatched (all 70 of its ray-tracing shaders traverse inline) ([raytracing.md](https://github.com/ThatRecompDev/five-star-recomp/blob/main/docs/raytracing.md)) |
| Audio | the game mixes on the CPU and plays through WASAPI but leaves decoding to the console hardware: Opus (99.9% of sounds) now decodes through a CPU copy of the console's queue; XMA2 now decodes behind the console's SHAPE XMA contexts, whose layout and messages are traced, including voices chained across contexts (checked against our decoder on a real sound, not yet heard in a boot); the audio DSP's convolution reverb runs on the CPU behind `--xdsp-convolution` (off by default until its impulse-response format is confirmed), and its second effect returns silence ([audio-runtime.md](https://github.com/ThatRecompDev/five-star-recomp/blob/main/docs/audio-runtime.md)) |
| Archive file table | names and exact entry boundaries stay encrypted; the game opens and reads its archives itself at run time |

## Research

| Item | State |
|---|---|
| BCPack | BC1 decodes exactly (all 102,725 pages); BC4 and BC5 decode approximately with tables fitted from the data; every codec's entropy layer decodes; BC7 (1,870 textures: mode headers and index layout confirmed, endpoint and preset tables not fitted), BC6H (98) and BC3 (17) are open ([details](Textures-and-BCPack.md)) |
| Type names | PSO structures are identified by hashes; naming them needs lists of engine class names |
| Geometry | drawables decode with tangents, four texture coordinate sets, skin influences and inverse binds; open: an export that carries skinning (glTF), the ped bone palette, head skin streams and the meshlet culling data ([details](https://github.com/ThatRecompDev/five-star-recomp/blob/main/docs/models.md)) |
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
