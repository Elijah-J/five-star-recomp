# Loader

`fivestar-loader` (`src/FiveStar.Loader`) maps the console's x64 PE images into its own process on desktop Windows
and binds their imports to C# shims, to other images it mapped, or to System32. The Xbox code is ordinary x86-64, so
nothing is translated. Until the game executable is available it is exercised on the shipped Xbox DLLs.

## Loading an image

```mermaid
flowchart TD
  map["Map<br/>VirtualAlloc at the preferred ImageBase, else anywhere<br/>(an image without .reloc then fails); headers and sections copied"]
  reloc["Relocate<br/>.reloc entries DIR64, HIGHLOW, HIGH and LOW, when the base moved"]
  exports["Exports<br/>read before the imports are bound, so import cycles resolve"]
  imports["Imports<br/>normal and delay-load imports (delay imports bound at load),<br/>by name and by ordinal; order in Import resolution"]
  patch["Patch<br/>the code patches for this module, while the pages are still writable"]
  protect["Protect<br/>PAGE_* per section from its characteristics; headers read-only"]
  pdata[".pdata<br/>RtlAddFunctionTable, so SEH, C++ EH and stack walks work through the image"]
  tls["TLS<br/>a generated TLS host DLL"]
  entry["Entry<br/>DllMain(DLL_PROCESS_ATTACH), dependencies first; FALSE fails the load"]
  map --> reloc --> exports --> imports --> patch --> protect --> pdata --> tls --> entry
```

Unloading runs `DllMain(DLL_PROCESS_DETACH)`, frees the TLS host, deletes the function table and releases the memory,
in reverse initialization order.

**TLS.** Compiled code reads implicit TLS as `gs:[58h][_tls_index]`, an array only ntdll can grow for every thread.
For each image with a TLS directory the loader writes a DLL to the work folder whose `.tls` template is a copy of the
image's relocated template and whose TLS callbacks are thunks: each copies the host's `_tls_index` into the image's
and jumps to the image callback with `rcx` = image base. After `LoadLibrary` of that DLL, ntdll has assigned the slot
and allocates blocks for existing and future threads. ntdll ignores the TLS directory of a DLL without imports, so the
host imports `kernel32!GetLastError`.

**TLS slot 0.** The linker compiles an executable's thread_local accesses as `mov rax,gs:[58h]; mov rax,[rax]`: an
executable's slot is always 0 and `_tls_index` is not read. Slot 0 of this process belongs to the host executable, so
the TLS host of a mapped executable gets two more callbacks around the image's: the first (process and thread attach)
saves the thread's slot-0 pointer in 8 bytes added after the template and points slot 0 at the executable's block; the
last (process and thread detach) puts it back before ntdll frees the blocks. Threads that existed before the
executable was mapped, other than the loading thread, keep the host's slot 0.

**Unresolved imports.** Each gets a stub `mov rdx,[rsp]; mov rcx,id; mov rax,handler; jmp rax` in a code heap that is
never writable and executable at once; rdx carries the caller's return address, so the first-call log line names the
call site (`from game_scarlett_master.exe+0x...`). `--unresolved stub` (default) logs the first call and returns 0 or
the module's value; `trap` terminates on the call; `error` fails the load.

## Import resolution

```mermaid
flowchart TD
  unres["unresolved stub (--unresolved)"]
  imp["import module!symbol"] --> shim{"shim for (module, symbol),<br/>a module alias or (*, symbol)?"}
  shim -->|"yes"| bshim["the shim"]
  shim -->|"no"| xbox{"Xbox-only module?"}
  xbox -->|"listed as not implemented"| ni["stub returning the listed value"]
  xbox -->|"any other symbol"| unres
  xbox -->|"no"| img{"image already mapped, or in the search directories<br/>and manual or absent from the PC?"}
  img -->|"yes"| exp{"exported?"}
  exp -->|"yes"| bimg["the image's export; a forwarder resolves again"]
  exp -->|"no"| unres
  img -->|"no"| sys{"LoadLibraryExW from System32<br/>and the export found?"}
  sys -->|"yes"| bsys["the System32 export"]
  sys -->|"no"| unres
```

1. **Shim.** Matched on (module, symbol), then through a module alias, then on ("*", symbol). A module declared
   Xbox-only (`xmem.dll`, `d3d12_xs.dll`, `xgameruntime.dll`, `gameinput.dll`, `acphal.dll`, `chad.dll`, `xapu.dll`,
   `xg_xs.dll`, `xcrdapi.dll`) never falls through: a function listed as not implemented binds to a stub that returns
   its listed value and logs its first call; any other symbol becomes an unresolved stub returning the module's value
   (0 for `xmem.dll`, E_NOTIMPL for the others).
2. **Image mapped by the loader.** An image already mapped, or a module found in the search directories (the
   directory of every loaded image, plus `--search`) that is either manual or not on the PC (no System32 file, no
   implemented API set). By default the manual modules are the shipped MSVC runtime: `msvcp140*`, `vcruntime140*`,
   `concrt140`, `vccorlib140`, `vcomp140`. Forwarders resolve through the same order.
3. **System32.** `LoadLibraryExW(name, LOAD_LIBRARY_SEARCH_SYSTEM32)`, so API sets resolve and a same-named DLL in
   the game folder is never picked up.

The shipped `vcruntime140.dll` is mapped by default because `_CxxThrowException` calls `RtlPcToFileHeader`, which
ntdll answers only for OS-loaded modules; mapping it lets the loader bind that import to its own shim. With
`--system-crt`, SEH still works, C++ exceptions inside a mapped image do not.

## Shims

| Shim | Behaviour |
|---|---|
| `xmem.dll` | `XMemAlloc`: requests of 64 KiB or more, or with a PageSize attribute, from `VirtualAlloc`; others from the aligned native heap, alignment from the attribute bits 24..28 (code n is 2^(n-1), at least 16). `XMemFree` releases what `XMemAlloc` returned and ignores other pointers. `XMemVirtualQuery` fills `MEMORY_BASIC_INFORMATION` and zeroes the 16-byte tail. `XMemGetWorkingSetStatistics` writes total (resident set plus free physical memory) and used (resident set) bytes. `XMemAllocDefault`/`XMemFreeDefault` as `XMemAlloc`/`XMemFree` |
| `xmem.dll` pages | Physical pages are 64 KiB pieces of pagefile-backed sections; a MEM_64K_PAGES reservation is a placeholder range and mapping replaces a 64 KiB placeholder with a view of the page, so pages alias and keep their contents across remaps ([physical-memory.md](physical-memory.md)). Other `XMemVirtualAlloc` calls go to `VirtualAlloc` with MEM_RESERVE/MEM_COMMIT and the low protection byte. `XMemIsToolsMemAvailable` and `XMemTransferMemory` return 0 |
| `d3d12_xs.dll` | `D3D12SerializeRootSignature`, `D3D12SerializeVersionedRootSignature`, `D3D12Create[Versioned]RootSignatureDeserializer` forwarded to the PC `d3d12.dll`; `D3D12XboxCreateDevice` on a desktop device and `D3D12ConfigureVirtualMemoryX` as a no-op ([d3d12x-shim.md](d3d12x-shim.md)); everything else E_NOTIMPL |
| `acphal.dll` | APU heap: `ApuCreateHeap` commits one region for both parts; `ApuAlloc` hands out zeroed, aligned blocks of it (first fit); an APU address is the offset plus 0x10000; `ApuFree`, `ApuIsVirtualAddressValid`, `ApuMapVirtualAddress`, `ApuMapApuAddress` follow. `AcpHalAllocateShapeContexts` hands out the XMA contexts from the heap and `AcpHalCreate` an IAcpHal object that accepts every message; nothing decodes XMA yet ([audio-runtime.md](audio-runtime.md)) |
| `xapu.dll` | the Opus decode queue on the CPU: commands run when enqueued (Concentus), results wait for `XApuGetResultCount` and `XApuDequeueResult` ([audio-runtime.md](audio-runtime.md)) |
| `chad.dll` | XDsp streams on the CPU: a submitted command completes at once through the status record the game polls; no effect runs, the output stays silent ([audio-runtime.md](audio-runtime.md)) |
| `xg_xs.dll` | `XGCreateTextureComputer` returns a texture address computer over the console tiling: layout, tiling, copies in and out ([xg.md](xg.md)); `XGSuggestSwizzleMode` writes 0x100 + a 4 KB or 64 KB mode |
| `xcrdapi.dll` `DStorageGetFactory` | a CPU DirectStorage factory: files through the overlay, a worker thread per queue, overlapped unbuffered reads, zlib chunks inflated into system memory, status arrays; slots the game does not call return E_NOTIMPL logged once ([directstorage.md](directstorage.md)) |
| `XboxBvhBuilder.dll` | the acceleration-structure builder class: prebuild sizes from the desktop device; builds, copies and rehydration logged once and skipped ([d3d12x-shim.md](d3d12x-shim.md)) |
| `amd_fidelityfx_loader_xbox.dll` | the five FidelityFX API functions: forwarded to the PC DLL of `--ffx` with the D3D12.X objects unwrapped, else creation reports no provider and the jitter queries are answered ([middleware.md](middleware.md)) |
| `RtlPcToFileHeader` (any module) | the mapped image containing the address, else the real ntdll function |
| `AddVectoredExceptionHandler` (any module) | registered as asked; the fault reporter of `--run` then moves back in front |
| `terminate` (any module) | logs the native stack, then calls `ucrtbase!terminate` |
| `GetModuleHandle{A,W}`, `GetModuleHandleEx{A,W}`, `GetModuleFileName{A,W}` (any module) | NULL means the main image (`--main`; else the first mapped executable, else the first `--load`); names, handles and addresses of mapped images answer with the mapping; truncation follows Windows (size-1 characters, result size, `ERROR_INSUFFICIENT_BUFFER`); everything else goes to kernel32 |
| `LoadLibrary{A,W,ExA,ExW}` (any module) | a mapped image: its base, reference counted; an Xbox-only module: a read-only page with a minimal PE header (a virtual module); a manual module or a DLL in the search directories, by name or by a path inside one: mapped now; data-file loads and the rest: the OS |
| `GetProcAddress` (any module) | mapped image: its exports; virtual module: the shim table (unknown name: NULL, error 127); OS module: the "*" shim of that name if there is one, else the real export |
| `FreeLibrary` (any module) | mapped image: reference count down; an image first mapped by `LoadLibrary` and imported by no other image is unloaded at 0; virtual module: TRUE |
| `CreateFile{W,A}`, `CreateFile2`, `GetFileAttributes{W,ExW}`, `fopen`, `_wfopen`, `_fsopen`, `_wfsopen`, `_open`, `_wopen` (any module) | the console package drive and the file overlay (below), then the real function; an ANSI call that is redirected opens the mod file through the wide function |
| `XGameRuntimeInitialize*`, `XGameRuntimeIsFeatureAvailable` | succeed; every feature available except XAppCapture, XSpeechSynthesizer and XGameStreaming |
| `XTaskQueue*` | Manual, ThreadPool, SerializedThreadPool and Immediate ports; composite queues; delayed callbacks; waiters (re-armed after each signal); monitors; terminate (pending callbacks run with `canceled = true`); duplicate and close reference counted; a process queue (ThreadPool/ThreadPool) created on first use |
| `XAsync*` | Begin, Schedule, Complete, GetStatus (with wait), GetResultSize, GetResult (identity and buffer checked), Cancel, Run; DoWork on the work port, the completion routine on the completion port, Cleanup once |
| `XUser*` | one signed-in offline user "Player" (fixed XUID); add, duplicate, close, find (by id, local id or any device), compare, gamertag, state, adult age group, every privilege granted (so `XUserResolvePrivilegeWithUi*` completes S_OK without UI); the controller picker answers the first XInput gamepad's GameInput device id; the audio endpoint is the PC's default communications device (MMDevice id); change, device-association and endpoint events register and never fire; never signing out; `XUserGetTokenAndSignature*` completes with HRESULT_FROM_WIN32(ERROR_NETWORK_UNREACHABLE) (no Xbox services token) |
| `XGameSave*`, `XGameSaveFiles*` | folders under `--save-dir`: `saves\<xuid>\<configId>\<container>\<blob>`, names %-escaped; updates staged and written per blob through a temporary file; sync and async reads, enumeration, deletes; 256 MB quota, also for the save-files folder |
| `XPackage*` | one installed game package at the package root with the identity from `MicrosoftGame.config`; mount returns the package root; chunks ready; installation monitors at 100 %; `XPackageInstallChunks[Async]` completes at once with such a monitor; `XPackageChangeChunkInstallOrder` S_OK; `XPackageUninstallChunks` HRESULT_FROM_WIN32(ERROR_NOT_SUPPORTED), `XPackageUninstallPackage` false; no DLC, no features, nothing to download, no install writes |
| `XStoreCreateContext`, `XStoreQueryGameLicense*` | an active, full, never-expiring license for the game itself |
| other `XStore*` the game calls | an unreachable store; no license or entitlement is ever reported. `XStoreAcquireLicenseFor{Package,Durables}*`, consumable balance and fulfillment, user collections/purchase ids and package downloads complete with E_GAMESTORE_NETWORK_ERROR (0x89245301) and no handle; `XStoreIsLicenseValid` false; entitled/associated product queries return an empty one-page query handle; the update query finds no updates; purchase, product page and redeem UI end with E_ABORT; `XStoreQueryPackageIdentifier` maps only the game's store id; license-changed and license-lost events register and never fire |
| `XSystem*`, `XThread*`, `XGameGetXboxTitleId`, `XGameEventWrite`, `XError*`, `XPersistentLocalStorage*` | device type 7 (Series X) unless `--device-type`; fixed console and sandbox ids; app-specific device id: Base64 SHA-256 of title id and PC name; any non-null handle valid; time sensitivity per thread; title id from the config; events dropped; local storage in `--save-dir\localstorage`, a space prompt succeeds when the space is free, else E_ABORT |
| `XGameUi*` | no system UI: dialogs complete through XAsync as if closed at once; S_OK for achievements, profile card, invites, error and state share; the message dialog answers its cancel button; the player picker picks nobody; text entry and web sign-in end with E_ABORT |
| `XNetworking*` | connectivity hint: network initialized, level None, cost unrestricted; preferred UDP port 3074 (sync and async); change events register and never fire; `XNetworkingQuerySecurityInformationForUrl*` completes and `XNetworkingVerifyServerCertificate` fails (closed) with HRESULT_FROM_WIN32(ERROR_NETWORK_UNREACHABLE): the request handle belongs to the game's HTTP stack and no request is live offline |
| `XDisplay*` | HDR: Enabled with the display's luminance when the primary display's desktop runs HDR10 (DXGI `GetDesc1`), else Disabled; a timeout deferral holds a "display required" power request |
| `XClosedCaption*`, `XHighContrastGetMode`, `XSpeechSynthesizer*` | captions white on black at scale 1, enabled as the game last set; high contrast from the Windows setting; no voices; `XSpeechSynthesizerCreate` HRESULT_FROM_WIN32(ERROR_NOT_SUPPORTED), so no synthesizer or stream handle exists and the calls taking one return E_INVALIDARG |
| `XLaunchUri`, `XLaunchRestartOnCrash`, `XLaunchNewGame`, `XGameInvite*`, `XGameActivation*`, `XGameProtocol*` | an http(s) URI opens in the browser, others are logged; restart arguments recorded; a new-game launch is logged and the game keeps running; invite, activation and protocol events register and never fire |
| `XAppCaptureTakeScreenshot` | HRESULT_FROM_WIN32(ERROR_NOT_SUPPORTED) |
| `gameinput.dll` `GameInputCreate` (also ordinal 1, how the executable imports it) | an IGameInput over XInput, the keyboard, the mouse and a keyboard-and-mouse virtual gamepad (below); also registered as `xgameruntime.dll!GameInputCreate` for the thunk ABI table |
| other `xgameruntime.dll` functions of the public headers | not implemented: E_NOTIMPL, E_GAMESTREAMING_NOT_INITIALIZED for game streaming, or 0 for bool, count and void functions; first call logged |

Of the 365 named thunks in `libHttpClient.GDK.dll`, 154 have a shim and 211 a not-implemented entry, so each one returns
a deliberate value (before this table grew: 88 shims, 186 entries, 91 left to the module default). Of the 416 thunks of
the PC GDK 261000 library, 247 have a shim; the game calls 161 of them, all shimmed. The library has no XAchievements, XUserStats, XThread affinity, XMemory or clock
functions; achievements and stats are Xbox services that titles reach over HTTP.

The GDK shims also answer for any `api-ms-win-*gaming*` or `ext-ms-win-*gaming*` module name, for symbols starting
with `X`; other functions of those API sets go to System32.

A shim that reports a last error is entered through a generated wrapper that writes `TEB.LastErrorValue` after the
managed method returns, because the runtime's return path resets the value a managed `SetLastError` set. The wrapper
copies up to 8 stack arguments (the fifth and later) into its own call.

## Code patches

`--patches <file or folder>` (repeatable) reads patch files; a folder contributes its `*.fspatch` files in name
order. A `[name]` line starts a patch, `key = value` lines describe it, `#` starts a comment.

| Key | Meaning |
|---|---|
| `module` | image file name (required) |
| `sha256` | SHA-256 of the image file; another build skips the patch |
| `enabled` | `true` (default) or `false` |
| `description` | free text |
| `pattern` | hex bytes separated by spaces, `??` for any byte (required) |
| `matches` | how many matches the pattern must have, default 1; each one is patched |
| `offset` | from the start of a match to the first written byte; decimal, `0x` hex, may be negative; default 0 |
| `bytes` | the replacement bytes |
| `jump` | `module!symbol` of a shim; writes `mov rax,<shim>; jmp rax` (12 bytes) |

Exactly one of `bytes` and `jump` is given. A jump target is looked up like an import (module, alias, then `*`), so
`jump` sends a game function to any shim, for example one written for raw mouse input.

```
# Bink2Scarlett.dll: BinkUtilCPUs is mov eax,7; ret
[twelve-cpus]
module = Bink2Scarlett.dll
description = report 12 CPUs
pattern = B8 07 00 00 00 C3 CC ?? ?? ?? ?? ?? ?? ?? ?? ?? F7 DA
offset = 1
bytes = 0C
```

Patches run in the order given, after relocation and import binding and before section protection, TLS and
`DllMain`. The pattern is searched over the whole mapped image. Each patch is logged as `applied` (with the written
offsets), `skipped` (disabled, or a different SHA-256) or `failed` (not found, a different match count, a write
outside the image, an unknown shim). A skipped or failed patch writes nothing.

## File overlay

`--mods <dir>` (repeatable) adds a mod folder; the first given has priority. When mapped code opens a path inside the
package root (`--package-root`, else the main image's directory) through one of the file shims, the loader takes the
path relative to the root and opens the same relative path in the first mod folder that contains that file. Paths
outside the root, and files no mod folder has, open unchanged. `\\?\` prefixes and relative paths are resolved first.
Each redirect is a detail line in the log (`[overlay] CreateFileW("...") -> "..."`), and with a log file every path
the file shims see is one (`[file] GetFileAttributesW("...")`).

The console mounts the game package as drive `G:`; a path starting with `G:\` is mapped to the package root before the mod
folders are consulted.

Directory listings (`FindFirstFile*`) are not merged: a file only a mod folder has opens, but is not listed.

## The GDK thunk ABI

GDK titles link `xgameruntime.lib`, which loads `xgameruntime.dll`, looks up `InitializeApiImpl`,
`InitializeApiImplEx`, `InitializeApiImplEx2`, `QueryApiImpl`, `UninitializeApiImpl` and `XErrorReport`, and implements
every `X*` call as `QueryApiImpl(&clsid, &iid, &object)` followed by a COM-style call through the object's vtable.
`libHttpClient.GDK.dll` contains this library.

```mermaid
sequenceDiagram
  participant T as GDK thunk in the image
  participant Q as loader QueryApiImpl
  participant O as object for (clsid, iid)
  participant S as shim
  T->>Q: QueryApiImpl(&clsid, &iid, &object)
  alt pair in the --gdk-abi table
    Q-->>T: the object of that pair
  else no table entry
    Q-->>T: REGDB_E_CLASSNOTREG
  end
  T->>O: call through vtable slot n
  alt slot mapped to a function
    O->>S: generated thunk drops this, shifts the arguments
    S-->>T: result
  else unmapped slot
    O-->>T: E_NOTIMPL
  end
  T->>O: Release (slot 2)
  O-->>T: no-op reference count
```

The loader answers `LoadLibraryExW("xgameruntime.dll")` with a virtual module, makes the `InitializeApiImpl*`
functions succeed and logs `XErrorReport`. `QueryApiImpl` builds one object per (clsid, iid) from a table of
`(clsid, iid, slot) -> function name`: slots 0..2 are IUnknown (no-op reference counting), and each mapped slot is a
generated thunk that drops `this` and shifts the arguments one position (integer registers, the stack, `xmm1..3`) into
the named shim. Unmapped slots return E_NOTIMPL. A pair without a table entry returns `REGDB_E_CLASSNOTREG`, and each
requested pair is logged once.

The table is loaded with `--gdk-abi <file>`, one entry per line, `#` starting a comment:

```
# clsid                                  iid                                    slot  function
11111111-2222-3333-4444-555555555555   66666666-7777-8888-9999-aaaaaaaaaaaa   3     XTaskQueueCreate
```

The repository ships no table; `fivestar platform gdk-abi` builds one from the public PC GDK (see
[platform.md](platform.md)). Without `--gdk-abi` the loader reads the newest 64-bit `xgameruntime.lib` under
`Program Files (x86)\Microsoft GDK` the same way; with no GDK it warns and the table stays empty. With an empty table
every thunk fails: `HCInitialize` of `libHttpClient.GDK.dll` returns `0x89240107`, and the executable, whose
`XSystemGetXboxLiveSandboxId` buffer then stays empty, matches none of `RETAIL`, `CERT`, `RKTR.99`, `CERT.DEBUG` or
the `RKTR` prefix and hits an `int3` (RVA `0x3dda76b`).

## GameInput

`gameinput.dll` is declared Xbox-only, so the PC GameInput.dll is never loaded; API-set names matching
`*-ms-win-*gameinput*` answer for symbols starting with `GameInput`. The layouts follow GameInput.h of the PC GDK
251005 (API v0) and live in one file, `Shims/Input/GameInputLayout.cs`.

| Interface | Implemented | Behaviour |
|---|---|---|
| IGameInput | QueryInterface, AddRef, Release, GetCurrentTimestamp, GetCurrentReading, GetNextReading, GetPreviousReading, RegisterDeviceCallback, StopCallback, UnregisterCallback, FindDeviceFromId | timestamps in microseconds; readings filtered by kind (any bit in common, 0 matches all) and device; no match: `GAMEINPUT_E_READING_NOT_FOUND`; FindDeviceFromId finds connected devices only, else `GAMEINPUT_E_DEVICE_NOT_FOUND` |
| IGameInputReading | QueryInterface, AddRef, Release, GetInputKind, GetSequenceNumber, GetTimestamp, GetDevice, GetRawReport (false), GetKeyCount, GetKeyState, GetMouseState, GetGamepadState | the state is copied out of the reading object |
| IGameInputDevice | QueryInterface, AddRef, Release, GetDeviceInfo, GetDeviceStatus, GetBatteryState (unknown), SetRumbleState (low and high motors) | |

Every other slot logs its first call and returns E_NOTIMPL, or 0 for a method that does not return an HRESULT.

The executable uses the v0 layout: `GameInputCreate(IGameInput**)` through one cached getter, IGameInput slots 4
(GetCurrentReading), 5 (GetNextReading), 6 (GetPreviousReading), 9 (RegisterDeviceCallback with blocking enumeration),
13 (UnregisterCallback with a timeout) and 16 (FindDeviceFromId); reading slots 14, 15 (keys) and 22 (gamepad);
device slot 3, from whose info it copies the id at +0x20. It asks only for the Gamepad (0x40000) and Keyboard (0x10)
kinds, never Mouse, so the mouse reaches it through the keyboard-and-mouse gamepad.

QueryInterface answers IUnknown and the object's own interface id; any other id gets E_NOINTERFACE, logged once.

| Device | Source | Kind |
|---|---|---|
| XInput gamepad 1..4 | `xinput1_4.dll`; sticks -1..1, triggers 0..1, no dead zone; an empty slot is probed once a second | Gamepad, family XboxOne |
| Keyboard | `GetAsyncKeyState`; up to 16 keys per reading, scan code from `MapVirtualKeyW`, code point 0 | Keyboard |
| Mouse | `GetCursorPos` deltas accumulated into the position; buttons from `GetAsyncKeyState`; no wheel | Mouse |
| Keyboard and mouse gamepad | the mapping below | Gamepad, family Virtual |

The sources are polled on the game's own calls, at most once per millisecond and by one thread at a time; a reading
is added only when a device's state changes. The last 256 readings are kept; a reading the game holds is never
reused. The keyboard and mouse sources need no window and read the whole desktop whether or not the game has focus;
mouse motion stops at the screen edges. `RegisterDeviceCallback` reports connected devices on the calling thread
before it returns (for asynchronous and blocking enumeration alike); later connects and disconnects are reported on
the thread whose call polled them. Releasing IGameInput or a device to 0 keeps the object for the process.

**Keyboard-and-mouse gamepad.** `--input <file>` replaces settings of the default mapping, one per line, `#` starting
a comment:

```
virtual-gamepad    on                # on | off at start
toggle             ScrollLock        # key that connects and disconnects it; none for no key
left-stick         W A S D           # up left down right; diagonals on the unit circle
mouse-sensitivity  1.0               # right-stick deflection per 1000 px/s of mouse motion
mouse-curve        1.0               # exponent after clamping to the unit circle
left-trigger       MouseRight
right-trigger      MouseLeft
a Space
b C
x R
y F
left-shoulder Q
right-shoulder E
left-thumb LeftShift
right-thumb V
menu Escape
view Tab
dpad-up Up
dpad-down Down
dpad-left Left
dpad-right Right
```

These are the defaults. Keys are letters, digits, `F1`..`F24`, `Space`, `Tab`, `Enter`, `Escape`, `Backspace`,
`CapsLock`, `ScrollLock`, `Insert`, `Delete`, `Home`, `End`, `PageUp`, `PageDown`, the arrows `Up` `Down` `Left`
`Right`, `LeftShift` `RightShift` `LeftCtrl` `RightCtrl` `LeftAlt` `RightAlt`, `MouseLeft` `MouseRight` `MouseMiddle`
`Mouse4` `Mouse5`, a virtual-key code as `0x..`, or `none`. The right stick follows mouse speed: deflection =
sensitivity × speed / 1000 px/s, clamped to 1, then raised to the curve exponent; screen down is stick down.

## Running it

```powershell
dotnet build FiveStar.slnx
$dump = "<your dump folder>"
fivestar-loader --load $dump\XboxBvhBuilder.dll --load $dump\Bink2Scarlett.dll --call all --unload --log run.log
```

| Option | Meaning |
|---|---|
| `--load <dll>` | map an image (repeatable) |
| `--run`, `--run-timeout <s>` | run the main executable's entry point (below) |
| `--call <export>` / `--call all` | call an export that was disassembled first (`--list-safe-calls` lists them); others are refused |
| `--main <name>` | the image that plays the process executable |
| `--patch-peb` | also point `PEB.ImageBaseAddress` at the main image (restored at exit) |
| `--save-dir <dir>` | root of saves and local storage; default `%LOCALAPPDATA%\FiveStar\GdkData\<TitleId>` |
| `--gdk-abi <file>` | the thunk ABI table; default: from an installed PC GDK |
| `--package-root <dir>` | where `MicrosoftGame.config` is read; default the main image's directory |
| `--device-type <n>` | `XSystemGetDeviceType` result |
| `--patches <file or folder>` | code patches (repeatable) |
| `--mods <dir>` | mod folder over the package root (repeatable, first has priority) |
| `--input <file>` | the keyboard-and-mouse gamepad mapping |
| `--shader-dir <dir>`, `--translated-dir <dir>` | extracted FSSM programs and their translated DXIL; with both, the D3D12.X pipeline slots create pipelines |
| `--ffx <dll>` | the PC FidelityFX API DLL the game's FidelityFX calls go to ([middleware.md](middleware.md)) |
| `--d3d12-debug`, `--d3d12-gpu-validation` | the D3D12 debug layer (and GPU-based validation) and DRED, with their messages and the removal report in the log ([d3d12x-shim.md](d3d12x-shim.md)); needs the Graphics Tools optional feature |
| `--d3d12-skip <i:n,...>` | D3D12.X slots that do nothing and return 0 (`list:20`, `queue:35`, `device:127`), to bisect GPU faults ([d3d12x-shim.md](d3d12x-shim.md#debug-layer)) |
| `--d3d12-trace-queues` | the first queue submissions, queue signals and waits and CPU fence signals in the log |
| `--present-test` | present moving color bars instead of the game's frame, to check the swap chain on its own ([d3d12x-shim.md](d3d12x-shim.md#present)) |
| `--manual <a.dll,b.dll>`, `--system-crt`, `--search <dir>` | which modules the loader maps itself |
| `--unresolved stub\|trap\|error`, `--no-shim <module!symbol>` | unresolved-import handling; disable one shim |
| `--no-entry`, `--no-exports`, `--unload`, `--work-dir <dir>` | skip DllMain; skip the export listing; unload at the end; folder of the TLS host DLLs |
| `--log <file>`, `-v` | full log of every import and shim call; the same on the console |

The output lists the images, an import table per image and module, the totals and the exports. Tests:
`dotnet test tests/FiveStar.Loader.Tests`; with `FIVESTAR_DUMP` set to the dump folder they also load the six Xbox
DLLs (825 imports, none unresolved), run the inspected exports and read the package identity.

## Running the executable

`--run` runs the main executable after loading: its TLS callbacks already ran when its TLS host loaded, then the
entry point (the CRT startup, the static initializers, then `main`) runs on a thread with the image's
`SizeOfStackReserve`. Before anything is loaded it turns on two diagnostics:

- **Call trace.** Imports bound to a shim or an unresolved stub go through a thunk that adds the import's id to a
  256-entry ring (`lock xadd`, only r10 and r11 touched) and jumps on.
- **Fault reporter.** A first vectored exception handler. For a hardware exception (NTSTATUS severity error, not a
  customer code) or a breakpoint whose first 12 stack frames include a mapped image, it logs the code (and the access
  and address of an access violation), the image RVA, the `.pdata` function around it, 16 code bytes, the registers,
  the stack and the last 24 traced calls; after 3 such records one line per fault, at most 64. It always continues the
  search, so the game's own handlers still run.

`--run-timeout <s>` suspends the main thread if the entry point has not returned after s seconds, logs the same
record for it and exits with code 3. `--trace-system` also traces imports bound to System32 (the ring then covers far\nless time). Run boots in a child process: the CRT ends the process through `exit`.

The loader is built with `<CETCompat>false</CETCompat>`: with CET shadow stacks, SEH resuming inside mapped code
fail-fasts (0x30).

## Call cost

`dotnet run -c Release --project benchmarks/FiveStar.Benchmarks -- --filter * --job short` (BenchmarkDotNet short job,
3 iterations; Ryzen 9 7950X3D, .NET 10.0.7):

| Path | Mean | Allocated |
|---|---:|---:|
| native to native (`mov eax,7; ret`), the baseline | 3.0 ns | 0 |
| native to shim (`XSystemGetDeviceType` through its IAT pointer) | 6.5 ns | 0 |
| a GDK call as the game makes it: `QueryApiImpl`, the slot through the drop-this thunk, `Release` | 58 ns | 0 |
| XTaskQueue submit and dispatch of a no-op on a manual queue | 58 ns | 0 |
| XAsync round trip: `XAsyncRun`, work and completion dispatched on a manual queue, `XAsyncGetStatus` | 308 ns | 576 B |
| a callback submitted to a thread-pool queue, waited for until a pool thread ran it | 841 ns | 56 B |
| `XMemAlloc` and `XMemFree` of 64 bytes | 74 ns | 56 B |

Most of the thunk path is `QueryApiImpl`'s lookup of the (clsid, iid) object under a lock; the shim behind it is the
6.5 ns path.

## Unknown

- The signatures of `XThreadVerifyNotTimeSensitive` and `XPackageGetIdentifier`, which the PC library exports but no
  public header declares (the first is answered with S_OK, the second is not implemented).
- Whether the console reports connectivity level None or LocalAccess when Xbox services are unreachable.
- The names of console-only thunks the PC GDK lacks (43 in `libHttpClient.GDK.dll`, most of them XUser through a
  console-only interface id), and the API-set names the game imports GDK functions from (the alias is a pattern).
- Signatures inferred from call sites: `InitializeApiImpl*`, `QueryApiImpl`, `XErrorReport`, `XMemVirtualQuery`,
  every `acphal.dll`, `chad.dll` and `xapu.dll` function, both `xg_xs.dll` functions and the texture computer's
  slots ([xg.md](xg.md)); the meaning of the `XMemVirtualQuery` tail (+0x30); the XALLOC attribute layout; the second
  argument of `ApuIsVirtualAddressValid`. The xmem page functions are in [physical-memory.md](physical-memory.md).
- Audio unknowns are in [audio-runtime.md](audio-runtime.md), DirectStorage ones in [directstorage.md](directstorage.md).
- XUser and XGameSave error HRESULT values; the members of `XPackageDetails` after `installing`; the
  array sizes of `XSystemAnalyticsInfo`; the member order of
  `XPersistentLocalStorageSpaceInfo`; what the console returns as package identifier.
- `XGameRuntimeInitializeWithOptions` options; waiters re-arming after each signal; `XGameSaveGetContainerInfo` on a
  missing container (S_OK, no callback).
- D3D12.X beyond the device ([d3d12x-shim.md](d3d12x-shim.md)), and whether its root-signature versions and flags match the PC ones.
- Overrides of files inside `.rpf` archives: the archive file table is encrypted, so the overlay works on whole files
  only. Which functions the game executable opens its files with.
- GameInput on the console: the reading semantics (kind filter, the history depth, when readings are added) and
  whether FindDeviceFromId also finds disconnected devices. The slots, kinds, key and gamepad state sizes and the
  device id offset the executable uses match v0.
