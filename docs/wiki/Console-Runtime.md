# Console runtime

The game and its libraries are normal x64 PE images. The loader maps them on Windows itself, resolves their imports
and runs their initialisation. Imports resolve in this order: a shim in the loader, an image the loader mapped, then
the Windows system DLL.

## Console-only libraries

| Library | Replacement |
|---|---|
| `xmem.dll` | allocation on `VirtualAlloc`; physical pages are pieces of shared memory sections mapped into placeholder ranges, so one page seen at two addresses holds the same data, as on the console |
| `d3d12_xs.dll` | the device on a desktop D3D12 device: desktop-compatible methods forward through generated thunks, console-only ones are logged; compute queues, command lists and frame pacing; root-signature helpers forwarded to `d3d12.dll` |
| `xg_xs.dll` | texture layout computers over the console's tiling (sizes, mip offsets, tiling, copies in and out) |
| `xcrdapi.dll` | DirectStorage: files read on worker threads and zlib chunks inflated into memory, exactly as stored |
| `acphal.dll`, `chad.dll`, `xapu.dll` | the APU heap on ordinary memory; the audio hardware reports itself unavailable |
| `GameInput.dll` | keyboard, mouse and gamepads |
| `xgameruntime.dll` | offline GDK runtime (below); the game loads it at run time and gets the loader's implementation |

## The offline GDK

The game runs as if the console had no network: one local player, saves in folders, everything installed.

| Area | What the game sees |
|---|---|
| Task queues, XAsync | real queues and async calls with the console's ordering |
| User | one signed-in player, every privilege, the first gamepad as their controller, the PC's default headset |
| Saves, local storage | folders under the save directory, with the console's quota |
| Package, store | one installed package, an active full license, no DLC |
| System UI | dialogs close at once: the cancel button, nobody picked, text entry canceled |
| Network | up but with no connectivity, so the game takes its offline paths |
| Display | HDR on when Windows runs the display in HDR, with its real brightness range; the screen stays awake on request |
| Accessibility | high contrast from Windows; captions as the game sets them; no speech voices |
| Invites, activation | never happen |
| Capture, streaming, speech | reported unavailable |

Every other GDK function answers with a deliberate "not implemented" value and logs its first call. Achievements and
stats are Xbox web services, not part of the runtime, so they fail like any offline web request.

## How GDK calls arrive

The game does not import GDK functions by name. A static library inside each module loads `xgameruntime.dll`, calls
`QueryApiImpl(clsid, iid, &object)` and then calls methods on the returned COM-style object by vtable slot. The
loader answers `QueryApiImpl` with objects whose slots call its shims. The table that maps each (clsid, iid, slot) to
a function is built from the user's own copy of the PC GDK (`fivestar platform gdk-abi`). With the October 2026 GDK it
names all 161 GDK functions the game calls and 335 of the 404 thunks in the shipped `libHttpClient.GDK.dll`. A call through this path costs about 60 ns.

## Process identity

`GetModuleHandle(NULL)` and `GetModuleFileName(NULL)` return the game image rather than the loader, and
`LoadLibrary` calls from loaded code are routed through the loader when the module is one it maps. Patching the
process environment block is optional because it also affects the .NET runtime hosting the loader.
