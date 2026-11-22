# Shader translation

The game ships its GPU programs only as native AMD RDNA (gfx10) machine code. The DXIL inside each shader record has
its bitcode removed, so the PC driver cannot compile it. The translator turns the machine code into HLSL, which
`dxc` compiles to DXIL for any PC GPU.

## Model

| Console concept | HLSL translation |
|---|---|
| A wave of 32 or 64 lanes | one HLSL invocation per lane |
| EXEC mask | a per-lane `active` flag |
| Branches | a wave-uniform block dispatch loop (`switch` on the program counter) |
| Lane masks | per-lane booleans; wave ballots where a mask is read as an integer |
| Resource descriptors | bindless heap indices written by the runtime into the descriptors |
| Hardware input registers | decoded from the program header (stage, wave size, input layout) |
| Semantics | taken from the input and output signatures that survive in each record |

## Stages

| Stage | Programs | Notes |
|---|---|---|
| Pixel | 12,049 | helper lanes included in wave operations (Shader Model 6.7) |
| Vertex | 2,191 | the driver's fetch shader becomes vertex inputs |
| Compute | 2,684 | |
| Mesh (NGG) | 8,765 | two phases so output counts are set before any output write; 46 exceed PC's 32 KB output limit and run as compute plus a vertex-pulling vertex shader |
| Geometry | 195 | legacy geometry shader with on-chip output; the embedded copy shader runs as a function per emitted vertex |

## Ray tracing

64 programs walk AMD's own bounding-volume hierarchy with `image_bvh_intersect_ray`. The instruction is emulated in
HLSL over AMD's node layouts (box16, box32, triangle), following AMD's GPURT reference, and matches it bit-exactly in
tests. A PC port therefore has to supply BVHs in AMD's layout: the shipped `XboxBvhBuilder.dll` produces them for
static data; moving geometry needs its own builder.

## Wave size

23,226 programs are written for 64-lane waves. On 32-lane GPUs (NVIDIA) per-lane work, quad operations and
first-lane reads stay correct. What breaks is reading another lane by index:

| Programs | Count |
|---|---|
| wave64, index other lanes (break on 32-lane GPUs) | 1,226 |
| wave32, index other lanes (break if a driver runs them as wave64) | 1,877 |

Compute and mesh shaders can request a wave size; pixel shaders cannot, so they need an emulated path.

## Limits

| Limit | Handling |
|---|---|
| Mesh output over 32 KB | compute plus vertex pulling |
| Group shared memory 28 KB | mesh outputs written directly instead of staged |
| Ordered pixel access (POPS) | rasterizer-ordered views |
| Very large programs | 3 programs exceed `dxc`'s time budget; needs structured control flow |
