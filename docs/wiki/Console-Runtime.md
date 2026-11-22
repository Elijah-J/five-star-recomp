# Console runtime

The game and its libraries are normal x64 PE images. The loader maps them on Windows itself, resolves their imports
and runs their initialisation. Imports resolve in this order: a shim in the loader, an image the loader mapped, then
the Windows system DLL.

## Console-only libraries

| Library | Replacement |
|---|---|
| `xmem.dll` | memory allocation on `VirtualAlloc` |
| `d3d12_xs.dll` | root-signature helpers forwarded to `d3d12.dll`; the rest of D3D12.X is not started |
| `xgameruntime.dll` | offline GDK runtime: real task queues and async; one local user; folder-backed saves; one installed package; store license always active |

## How GDK calls arrive

The game does not import GDK functions by name. A static library inside each module loads `xgameruntime.dll`, calls
`QueryApiImpl(clsid, iid, &object)` and then calls methods on the returned COM-style object by vtable slot. The
loader answers `QueryApiImpl` with objects whose slots call its shims. The table that maps each (clsid, iid, slot) to
a function is not filled yet: it needs the game's call sites or the PC GDK's import library.

## Process identity

`GetModuleHandle(NULL)` and `GetModuleFileName(NULL)` return the game image rather than the loader, and
`LoadLibrary` calls from loaded code are routed through the loader when the module is one it maps. Patching the
process environment block is optional because it also affects the .NET runtime hosting the loader.
