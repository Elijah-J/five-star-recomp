# Roadmap

## Needs the executable

| Item | Why |
|---|---|
| Archive file table | names and exact entry boundaries are encrypted; the key and algorithm are in the executable |
| Import list | the exact set of console APIs the game calls |
| D3D12.X usage | how the game drives the GPU: device calls, descriptors, command lists |
| Pipeline state | root signatures, blend and depth state, vertex formats, shader constants layout |
| GDK call table | GDK functions are reached through COM-style objects; mapping each object and slot to a function needs the game's call sites (or the PC GDK) |

## Research

| Item | State |
|---|---|
| BCPack | header and entropy-coder parameters known; the data models are not ([details](Textures-and-BCPack.md)) |
| `audio.rpf` | the payload is raw audio banks, not zlib; banks not decoded yet |
| Type names | PSO structures are identified by hashes; naming them needs lists of engine class names |
| Geometry | the older paged resources with vertex and index data are mapped but not decoded |
| BVH data | ray tracing needs acceleration structures in AMD's layout ([details](Shader-Translation.md#ray-tracing)) |

## Engineering

| Item | State |
|---|---|
| D3D12.X runtime | only root-signature helpers exist |
| Shader linking | a vertex, geometry or mesh shader must use the pixel shader's interpolation modes when a pipeline is built |
| Wave size | compute programs that read lanes by number are pinned to their wave size; 1,982 pixel, vertex and mesh programs with the same dependence need emulation ([details](Shader-Translation.md#wave-size)) |
| Structured shader control flow | optional and working (DXIL 39% smaller); needs a check on real game data before it becomes the default, and the 3 largest programs still take 25 to 80 minutes in dxc |
| Pipeline sweep | build a pipeline state for every translated shader with real drivers (NVIDIA, AMD) to find what dxc accepts but drivers reject |
| Call overhead benchmarks | measure game-to-runtime calls and command translation |
| First frame | swap chain and real draws |
| Proton | test the Windows build on Linux and Steam Deck through Proton |

Platform and performance strategy: [Platforms and performance](Platforms-and-Performance.md).
