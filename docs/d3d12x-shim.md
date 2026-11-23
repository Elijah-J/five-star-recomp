# D3D12.X device shim

The game imports three functions from `d3d12_xs.dll`: `D3D12XboxCreateDevice`, `D3D12ConfigureVirtualMemoryX` and
`D3D12SerializeVersionedRootSignature`. All other rendering goes through COM vtables on the device and the objects
it creates. This page lists what the game passes and calls, read from `game_scarlett_master.exe` (addresses at image
base 0x140000000), and how the loader shims it. Field and method names marked *inferred* come from argument shapes,
not from the GDK header.

## Startup order

The device is created in one function (0x142478440), after the game registers and shows its own Win32 window
(`RegisterClassExA`, `CreateWindowExA`, `ShowWindow`):

1. `D3D12ConfigureVirtualMemoryX` (call at 0x141e1cfe0), then `XMemVirtualAlloc` reserves the GPU virtual range.
2. `D3D12XboxCreateDevice` (call at 0x1424785b7). The device pointer goes to 0x1475df048 and is copied to eleven
   more globals (0x1475deff8 to 0x1475df040, 0x148759d00).
3. On an OS build of 10.0.26100.9628 or later: device slot 49 with (0x11F, 1).
4. Slot 26 (GetCustomHeapProperties) for heap types 1, 2, 3; slot 8 (CreateCommandQueue) for one direct queue
   (stored at 0x148759ff8); slot 48 four times for four more queues (0x1475e7038 to 0x1475e7050).
5. Slot 9 and slot 73 create command allocators and lists; the lists record X-only slots 132 and 131 and are closed
   (slot 9).

## D3D12XboxCreateDevice

`HRESULT D3D12XboxCreateDevice(IGraphicsUnknown* adapter, const D3D12XBOX_CREATE_DEVICE_PARAMETERS* parameters,
REFIID riid, void** device)`. The game passes a null adapter and `riid` = `5405c344-d457-444e-b4dd-2366e45aee39`,
the desktop IID of ID3D12Device11 (`XboxBvhBuilder.dll` also takes an `ID3D12Device11*`).

The parameter block is 0x50 bytes, cleared before it is filled:

| Offset | Value | Meaning |
|---|---|---|
| 0x00 | 0x00020004 | Version (*inferred*) |
| 0x04 | 0 | Process debug flags (*inferred*) |
| 0x08 | 0x00100000 (1 MiB) | Graphics command-queue ring size (*inferred*) |
| 0x0C, 0x10 | 0 | Graphics and compute scratch sizes, 0 = default (*inferred*) |
| 0x20 | byte at 0x14a943957 | Boolean set at run time; meaning unknown |
| 0x24 | byte at 0x14a943958 | Boolean set at run time; meaning unknown |
| 0x28 | 1 | Unknown |
| 0x3C | 0x20 | Unknown |
| other | 0 | |

## D3D12ConfigureVirtualMemoryX

One pointer to a 32-byte block, zero except the dword at +4: `ceil(size / 16 GiB) + 2`, where `size` is 16 GiB when
the console-type query (0x1455deb88) returns 6 and 24 GiB otherwise. The same `size` is then reserved with
`XMemVirtualAlloc`. A failing HRESULT hits an `int3`.

## Calling convention

- Methods that return a struct take the return buffer in rcx and the object in rdx (slot 25 at 0x14034413f, slot 26
  at 0x14247870d). The desktop ABI is the other way round.
- AddRef and Release are ordinary vtable calls (slots 1 and 2); the game does not touch reference counts directly.

## Vtable census

A static scan of every function in `.pdata` (scratch tooling, not in the repository) follows the device from its
globals, through function parameters and return values, and types each created object by the IID passed next to
its out-pointer. The counts are call sites, not run-time frequency; objects reached only through engine structures
are under-counted.

### Device (85 sites, 42 slots)

Slots 0 to 43 follow desktop ID3D12Device order: every creation slot below passes the IID of the desktop object it
should create. From slot 44 on the order is not desktop's.

| Slot | Offset | Sites | Method | Evidence |
|---|---|---|---|---|
| 0 | 0x000 | 1 | QueryInterface | asks for IDXGIDevice |
| 2 | 0x010 | 2 | Release | |
| 8 | 0x040 | 2 | CreateCommandQueue | queue IID |
| 9 | 0x048 | 5 | CreateCommandAllocator | allocator IID |
| 14 | 0x070 | 3 | CreateDescriptorHeap | descriptor-heap IID |
| 15 | 0x078 | 10 | GetDescriptorHandleIncrementSize | |
| 16 to 21 | 0x080-0x0A8 | 1 each | CreateRootSignature, CreateConstantBufferView ... CreateDepthStencilView | |
| 24 | 0x0C0 | 1 | CopyDescriptorsSimple | (1, dest, src, type) |
| 25 | 0x0C8 | 3 | GetResourceAllocationInfo | struct return, swapped registers |
| 26 | 0x0D0 | 3 | GetCustomHeapProperties | struct return, swapped registers |
| 27 | 0x0D8 | 1 | CreateCommittedResource | resource IID |
| 36 | 0x120 | 1 | CreateFence | fence IID |
| 39 | 0x138 | 1 | CreateQueryHeap | query-heap IID |
| 41 | 0x148 | 5 | CreateCommandSignature | command-signature IID |
| 48 | 0x180 | 4 | X: create command queue | queue IID, desc, IID, out |
| 49, 50 | 0x188, 0x190 | 1 each | X | |
| 55, 57, 62, 67, 86 | | 1 each | X: create pipeline state (five variants, [d3d12x-pipelines.md](d3d12x-pipelines.md)) | pipeline-state IID |
| 61 | 0x1E8 | 1 | X | |
| 70, 71 | 0x230, 0x238 | 6, 2 | X: ray-tracing related (*inferred*) | desc carries 0x20004 at +0x30 |
| 73 | 0x248 | 3 | X: create command list | (16-byte desc, allocator, PSO, IID, out) |
| 74, 78, 81 | | 1 each | X | |
| 79 | 0x278 | 3 | X | |
| 80 | 0x280 | 3 | X: creates an object without an IID | handed to queue slot 35 |
| 101 | 0x328 | 1 | X: create state object | state-object IID |
| 108, 119, 127, 132 | | 1-2 each | X | |
| 133 | 0x428 | 3 | X: create resource at a GPU address (*inferred*) | (address, desc, state, clear, resource IID, out) |

### Command queue

The base methods sit two slots later than on the desktop (ID3D12Pageable appears to carry two extra methods;
*inferred*): GetTimestampFrequency is slot 18 (one `UINT64*`), Signal slot 16 (fence, value).

| Slot | Offset | Sites | Method |
|---|---|---|---|
| 16 | 0x080 | 1 | Signal (*inferred*) |
| 18 | 0x090 | 1 | GetTimestampFrequency (*inferred*) |
| 23 | 0x0B8 | 1 | X: suspend, argument 0 (*inferred*: process-lifetime suspend) |
| 24 | 0x0C0 | 1 | X: resume (*inferred*) |
| 31 | 0x0F8 | 1 | X |
| 32 | 0x100 | 2 | X: present (*inferred*): (plane count, plane array, parameters); called with 0, 0, 0 at shutdown |
| 35 | 0x118 | 2 | X: execute command lists (*inferred*): (count, lists, slot-80 object, flags 0 or 3) |

The per-frame submit is 0x142481e90: slot 35, then slot 32, with device slot 80 called between frames.

### Command list

The game asks slot 73 for desktop ID3D12GraphicsCommandList (`5b160d0f-...`) in one place and for
`c3827890-4024-474c-98cf-645b51dc4874`, not a desktop IID, in the others. Close is slot 9 as on the desktop.
Slots 131 and 132 (X-only) take (register offset, value, 0, mask 0xFFFFFFFFFF00): register writes. The game's
graphics context keeps its list at +0x127C8; the scan reaches 26 sites in 18 slots, too few to map the recording
methods.

## Shim

`XboxDeviceFactory` (`src/FiveStar.Loader/Shims/D3D12X`) implements `D3D12XboxCreateDevice`:

- Creates a desktop ID3D12Device (default adapter, feature level 12_1) and hands the game a two-pointer object:
  the D3D12.X vtable, then the desktop device.
- Slots 0 to 43 are generated tail-call thunks: `mov rcx,[rcx+8]; mov rax,[rcx]; jmp [rax+slot*8]`. Slots 25, 26
  and 43 load the object from rdx and swap rcx and rdx first. No managed code runs on these calls, so they meet the
  `[HotPath]` rule by construction.
- Slots 44 to 159 are logged stubs: the first call of each slot is logged as `d3d12_xs.dll!ID3D12Device.X::SlotN`,
  every call returns E_NOTIMPL.
- `D3D12ConfigureVirtualMemoryX` logs its argument and returns S_OK.

The other interfaces take the same shape: one thunk vtable per interface (queue, list, allocator, resource), whose
desktop-compatible slots forward with the slot remapped (+2 for pageable objects) and whose X slots go to
`[UnmanagedCallersOnly]` handlers that translate. The forwarded creation slots currently return unwrapped desktop
objects, so a queue or list call past the desktop layout does not yet reach a stub.

## Unknown

- The command-list vtable: which slots record draws, dispatches, barriers and root arguments. A run-time trace of
  the slots the game calls (all stubs logging) is the fastest way to get it.
- Device slots 44 to 133 apart from those named above; the desc layout of slots 48 and 73.
- Whether the queue's +2 shift holds for every pageable object (resource, heap, fence, PSO, allocator).
- The object device slot 80 returns and how queue slot 35 uses it (frame pacing is the likely role).
- Parameter-block fields at 0x20, 0x24, 0x28 and 0x3C.
- Whether the struct-return register swap applies to GetAdapterLuid (slot 43) and to struct-returning methods of
  other interfaces (descriptor-heap handle getters, resource GetDesc).
