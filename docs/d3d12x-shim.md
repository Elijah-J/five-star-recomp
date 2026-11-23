# D3D12.X device shim

The game imports three functions from `d3d12_xs.dll`: `D3D12XboxCreateDevice`, `D3D12ConfigureVirtualMemoryX` and
`D3D12SerializeVersionedRootSignature`. All other rendering goes through COM vtables on the device and the objects
it creates. This page lists what the game passes and calls, read from `game_scarlett_master.exe` (addresses at image
base 0x140000000), and how the loader shims it. Field and method names marked *inferred* come from argument shapes,
not from the GDK header: the PC GDK (`Microsoft GDK\261000`) ships only `windows\include` headers, none for
D3D12.X.

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
| 80 | 0x280 | 3 | X: WaitFrameEventX (*inferred*) | (type, timeout, null, flags, UINT64* in/out) |
| 101 | 0x328 | 1 | X: create state object | state-object IID |
| 108, 119, 127, 132 | | 1-2 each | X | |
| 133 | 0x428 | 3 | X: create resource at a GPU address (*inferred*) | (address, desc, state, clear, resource IID, out) |

### Command queue

The queue's own methods sit two slots later than on the desktop: GetTimestampFrequency is slot 18 (one
`UINT64*`), Signal slot 16 (fence, value). The shift is the queue's, not ID3D12Pageable's: the descriptor heap,
also pageable, has GetCPUDescriptorHandleForHeapStart at desktop slot 9 (call at 0x141c74559).

| Slot | Offset | Sites | Method |
|---|---|---|---|
| 16 | 0x080 | 1 | Signal (*inferred*) |
| 18 | 0x090 | 1 | GetTimestampFrequency (*inferred*) |
| 23 | 0x0B8 | 1 | X: suspend, argument 0 (*inferred*: process-lifetime suspend) |
| 24 | 0x0C0 | 1 | X: resume (*inferred*) |
| 31 | 0x0F8 | 1 | X |
| 32 | 0x100 | 2 | X: present (*inferred*): (plane count, plane array, parameters); called with 0, 0, 0 at shutdown |
| 35 | 0x118 | 2 | X: execute command lists (*inferred*): (count, lists, object from a global, flags 0 or 3) |

The per-frame submit is 0x142481e90: slot 35 (up to 256 lists), then slot 32, with device slot 80 called between
frames. Device slot 80 creates no object: it takes (type 0 or 2, timeout 0xFFFFFFFF, null, flags 0 or 2, UINT64*
in/out), read as WaitFrameEventX (*inferred*); the game breaks into the debugger when it fails (0x142482452).

### Command list

The game asks slot 73 for desktop ID3D12GraphicsCommandList (`5b160d0f-...`) in one place and for
`c3827890-4024-474c-98cf-645b51dc4874`, not a desktop IID, in the others. Close is slot 9 as on the desktop.
Slots 131 and 132 (X-only) take (register offset, value, 0, mask 0xFFFFFFFFFF00): register writes.

Most commands are recorded inline: the game compares the list's write pointer (+0x18) with a limit (+0x28 before
draws, +0x30 otherwise), writes PM4 packets itself when there is room, and calls the vtable slot only when there is
not. It also writes state into the list object (+0x140, +0x544 to +0x563) and reads +0x548 and a register table
at +0x144 / +0x344. The 34 functions that load the list from the graphics context (+0x127C8) call these slots; the
method follows from the arguments and from the inline packet written instead:

| Slot | Desktop | Method | Evidence |
|---|---|---|---|
| 9 | 9 | Close | creation code |
| 12, 13 | 12, 13 | DrawInstanced, DrawIndexedInstanced | 4 and 5 counts; limit +0x28 |
| 20 to 24 | 20 to 24 | IASetPrimitiveTopology, RSSetViewports, RSSetScissorRects, OMSetBlendFactor, OMSetStencilRef | (1, rects), float*, one dword |
| 25 | 25 | SetPipelineState | PSO; caches PSO state at +0x544 |
| 29, 30 | | X | (pointer, dword, dword); 30 has 7 arguments |
| 33, 34 | 29, 30 | SetCompute/GraphicsRootSignature | root signature from the PSO (+0x80) |
| 35, 36 | 31, 32 | SetCompute/GraphicsRootDescriptorTable | (index, GPU handle); inline dword + low dword |
| 37, 38 | 33, 34 | SetCompute/GraphicsRoot32BitConstant (*inferred*: between 36 and 39) | |
| 39, 40 | 35, 36 | SetCompute/GraphicsRoot32BitConstants | (index, count, data, 0) |
| 43 to 46 | 39 to 42 | SetCompute/Graphics root SRV and UAV | (index, GPU address); inline dword + qword |
| 47, 48 | 43, 44 | IASetIndexBuffer, IASetVertexBuffers | view* or null; (0, count, views) |
| 50 | 46 | OMSetRenderTargets | (count, handles, FALSE, DSV*) |
| 51, 52 | 47, 48 | ClearDepthStencilView (*inferred*), ClearRenderTargetView | (handle, color*, 0, null) |
| 61 | 59 | ExecuteIndirect | (signature, 1, argument buffer, offset, null, 0); 8 sites |
| 62 | | X | (pointer, pointer, dword, 0) |
| 70 | 62 | OMSetDepthBounds | floats in xmm1, xmm2 |
| 72, 75, 126 to 128, 142 | | X | one site each |

Slots 26 to 28 are presumably ResourceBarrier, ExecuteBundle and SetDescriptorHeaps, and 31, 32 two X slots
(*inferred*); between 52 and 61 two desktop methods are missing.

### Descriptor heaps

The game allocates CPU descriptor memory the console way: D3D12.X takes it from the title's `XMemAlloc` hook (the
exe exports `__XMemAllocationHooks__`). Around the creation of the main shader-visible heap (0x141cf3280, 1245184
descriptors) the game pushes a hook that answers allocator id 128, object type 4 with memory it reserved, and breaks
into the debugger unless slot 9 then returns that memory.

## Shim

`XboxDeviceFactory` (`src/FiveStar.Loader/Shims/D3D12X`) implements `D3D12XboxCreateDevice` on a desktop
ID3D12Device (default adapter, feature level 12_1). `D3D12ConfigureVirtualMemoryX` logs its argument and returns
S_OK.

### Wrappers

Every object the game gets is a wrapper (`XboxObjects`): a pointer to the D3D12.X vtable, then the desktop object at
+8. One native block holds a vtable per interface (`XboxInterfaceKind`), so a pointer whose vtable lies in it is a
wrapper.

- Slots listed in `XboxInterfaces` are generated tail-call thunks: `mov rcx,[rcx+8]; mov rax,[rcx]; jmp
  [rax+desktop*8]`, with the slot remapped; struct-returning slots load the object from rdx and swap rcx and rdx.
  Arguments in rdx, r8 or r9 that may carry a wrapper are unwrapped first (`test; jz; mov r10,[reg]; range check;
  mov reg,[reg+8]`), any other pointer passes through. No managed code runs on these calls.
- Every other slot is a stub that logs its first call as `d3d12_xs.dll!<interface>.X::SlotN` with the caller and
  returns E_NOTIMPL.
- QueryInterface returns the wrapper when the desktop answer is the object itself, Release frees the wrapper with
  the desktop object, GetDevice returns the device wrapper.
- `XboxObjects.SetHandler(kind, slot, handler)` binds any slot to a native function (an `[UnmanagedCallersOnly]`
  method taking the wrapper first), before or after the vtables exist. `Wrap`, `Unwrap` and `WrapResult` are the
  helpers for handlers that create or receive objects.
- Pipeline states (slots 55, 57, 62, 67, 86, [d3d12x-pipelines.md](d3d12x-pipelines.md)) are returned unwrapped.

| Interface | Forwarded slots | Slot map |
|---|---|---|
| Device | 1 to 43 | desktop order; 25, 26, 43 swap; views (18 to 21), 31, 42 unwrap |
| Command queue | 13 to 20 | desktop slot + 2; Signal and Wait unwrap the fence; 20 (GetDesc) swaps |
| Command allocator | 8 | desktop (*inferred*) |
| Command list | 8 to 25, 33 to 40, 50 to 52, 70 | as in the table above; Reset, ClearState, copies and SetPipelineState unwrap |
| Resource | 8 to 14 | desktop (*inferred*); 10 (GetDesc) swaps |
| Heap, descriptor heap | 8 | GetDesc, swaps |
| Fence | 8 to 10 | desktop (*inferred*) |
| Query heap, command signature | none past the base slots | |

The command-list wrapper is 4 KiB and zeroed: write pointer and limits stay 0, so every inline command calls its
slot. Slots taking GPU addresses (41 to 48) stay stubs until console addresses are translated.

### Handlers

| Interface | Slot | Handler |
|---|---|---|
| Device | 8, 9, 12, 14, 27 to 30, 36, 39, 41 | desktop creation, result wrapped; wrapped heap, allocator and PSO arguments unwrapped |
| Device | 17 to 24 | view creation and descriptor copies with CPU handles translated (below) |
| Device | 34, 35 | MakeResident, Evict: S_OK (desktop objects are resident from creation) |
| Device | 48, 73 | compute queues (flags reduced to DISABLE_GPU_TIMEOUT) and command lists, wrapped |
| Device | 78, 79 | SetFrameIntervalX, ScheduleFrameEventX (*inferred*): logged, S_OK |
| Device | 80 | logged stub returning S_OK |
| Device | 133 | buffer at a game address (below) |
| Command queue | 12, 35 | unwrap up to 256 lists per call and run desktop ExecuteCommandLists; 35 ignores its frame object and flags |
| Command queue | 32 | logged stub returning S_OK (no swap chain yet) |
| Resource | 11 | the console address for a slot-133 buffer, else the desktop GPU address |
| Descriptor heap | 9, 10 | heap start handles returned in rax; 9 returns the title memory when the heap has some |

### Game memory on the GPU

Slot 133 (GPU address, buffer desc, 0xFFFFFFFF, null clear value, IID, out) asks for a buffer over XMem pages the
game mapped itself (`XboxPlacedResources`). The store's whole-section view of each page section
([physical-memory.md](physical-memory.md)) is opened with `ID3D12Device3::OpenExistingHeapFromAddress`
(`OpenExistingHeapFromFileMapping` rejects the pagefile sections with E_INVALIDARG). The range is split into runs of
pages contiguous in one section; each run becomes a buffer placed at its section offset, with
ALLOW_CROSS_ADAPTER (required on such heaps) and the game's flags reduced to UNORDERED_ACCESS (it passes 0x20004).
The game's resource is the first run's buffer; GetGPUVirtualAddress returns the console address; every run's
console range and desktop GPU address go into `XboxPlacedResources.Addresses`, the translation the command-list
and descriptor work will use (on the console GPU address == CPU address). A reserved buffer with tile mappings onto
these heaps removes the device, so one resource cannot span sections. A range with no mapped page gets a committed
buffer that is not shared.

Descriptor heaps (`XboxDescriptorHeaps`): a shader-visible heap takes title memory from the game's hook (allocator
id 128, object type 4); slot 9 returns it and `Handles` maps it onto the desktop heap's CPU handles, which slots 17
to 24 translate. The GPU start stays the desktop one, so the GPU handles the game derives are desktop handles. A
heap above 1,000,000 descriptors (the tier-2 limit) is created at the limit, with a warning.

### Boot

With these, boot passes device set-up, slot 133 (four page runs of 0xAE00000 bytes) and the descriptor heaps, then
calls device slot 70 (stub) at 0x144ecc81b; the next CreateCommandAllocator fails because the desktop device was
removed with DXGI_ERROR_INVALID_CALL, and the game stops at the `int3` at 0x1419f4f58.

## Unknown

- What removes the desktop device after the descriptor heaps; device slot 70 (ray-tracing related, 6 sites).
- List slots 26 to 32, 53 to 60, 62 and above 70; the list slots that take wrapped resources inside structures
  (barriers, texture copies) and GPU addresses.
- Device slots 44 to 133 apart from those named above; the meaning of the desc dwords of slots 48 and 73.
- The slot maps marked *inferred*: allocator, resource and fence have no X call site in the census.
- What queue slot 35's third argument is and how frame pacing (device 78 to 80, queue 32) should map to a swap chain.
- Descriptors the game writes into title memory itself instead of through slots 17 to 24, and descriptors past the
  first 1,000,000 of the main heap.
- Parameter-block fields at 0x20, 0x24, 0x28 and 0x3C.
