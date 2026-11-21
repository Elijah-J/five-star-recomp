# Loader

`fivestar-loader` (`src/FiveStar.Loader`) maps the console's x64 PE images into its own process on desktop Windows
and binds their imports to C# shims, to other images it mapped, or to System32. The Xbox code is ordinary x86-64, so
nothing is translated. Until the game executable is available it is exercised on the shipped Xbox DLLs.

## Loading an image

| Step | What happens |
|---|---|
| Map | `VirtualAlloc` at the preferred `ImageBase`, else anywhere (an image without `.reloc` then fails); headers and sections copied |
| Relocate | `.reloc` entries DIR64, HIGHLOW, HIGH and LOW, when the base moved |
| Exports | read before the imports are bound, so import cycles resolve |
| Imports | normal and delay-load imports (delay imports bound at load), by name and by ordinal; order below |
| Protect | `PAGE_*` per section from its characteristics; headers read-only |
| `.pdata` | `RtlAddFunctionTable`, so SEH, C++ EH and stack walks work through the image |
| TLS | a generated "TLS host" DLL (below) |
| Entry | `DllMain(DLL_PROCESS_ATTACH)`, dependencies first; FALSE fails the load |

Unloading runs `DllMain(DLL_PROCESS_DETACH)`, frees the TLS host, deletes the function table and releases the memory,
in reverse initialization order.

**TLS.** Compiled code reads implicit TLS as `gs:[58h][_tls_index]`, an array only ntdll can grow for every thread.
For each image with a TLS directory the loader writes a DLL to the work folder whose `.tls` template is a copy of the
image's relocated template and whose TLS callbacks are thunks: each copies the host's `_tls_index` into the image's
and jumps to the image callback with `rcx` = image base. After `LoadLibrary` of that DLL, ntdll has assigned the slot
and allocates blocks for existing and future threads. ntdll ignores the TLS directory of a DLL without imports, so the
host imports `kernel32!GetLastError`.

**Unresolved imports.** Each gets a stub `mov rcx,id; mov rax,handler; jmp rax` in a code heap that is never writable
and executable at once. `--unresolved stub` (default) logs the first call and returns 0 or the module's value; `trap`
terminates on the call; `error` fails the load.

## Import resolution

1. **Shim.** Matched on (module, symbol), then through a module alias, then on ("*", symbol). A module declared
   Xbox-only (`xmem.dll`, `d3d12_xs.dll`, `xgameruntime.dll`) never falls through: a function listed as not
   implemented binds to a stub that returns its listed value and logs its first call; any other symbol becomes an
   unresolved stub returning the module's value (E_NOTIMPL for `xgameruntime.dll`, 0 otherwise).
2. **Image mapped by the loader.** An image already mapped, or a manual module found in the search directories (the
   directory of every loaded image, plus `--search`). By default the manual modules are the shipped MSVC runtime:
   `msvcp140*`, `vcruntime140*`, `concrt140`, `vccorlib140`, `vcomp140`. Forwarders resolve through the same order.
3. **System32.** `LoadLibraryExW(name, LOAD_LIBRARY_SEARCH_SYSTEM32)`, so API sets resolve and a same-named DLL in
   the game folder is never picked up.

The shipped `vcruntime140.dll` is mapped by default because `_CxxThrowException` calls `RtlPcToFileHeader`, which
ntdll answers only for OS-loaded modules; mapping it lets the loader bind that import to its own shim. With
`--system-crt`, SEH still works, C++ exceptions inside a mapped image do not.

## Shims

| Shim | Behaviour |
|---|---|
| `xmem.dll` | `XMemAlloc`: requests of 64 KiB or more, or with a PageSize attribute, from `VirtualAlloc`; others from the aligned native heap, alignment from the attribute bits 24..28 (code n is 2^(n-1), at least 16). `XMemFree` releases what `XMemAlloc` returned and ignores other pointers. `XMemVirtualQuery` fills `MEMORY_BASIC_INFORMATION` and zeroes the 16-byte tail. `XMemGetWorkingSetStatistics` writes peak and current working set |
| `d3d12_xs.dll` | `D3D12SerializeRootSignature`, `D3D12SerializeVersionedRootSignature`, `D3D12Create[Versioned]RootSignatureDeserializer` forwarded to the PC `d3d12.dll`; everything else unresolved |
| `RtlPcToFileHeader` (any module) | the mapped image containing the address, else the real ntdll function |
| `terminate` (any module) | logs the native stack, then calls `ucrtbase!terminate` |
| `GetModuleHandle{A,W}`, `GetModuleHandleEx{A,W}`, `GetModuleFileName{A,W}` (any module) | NULL means the main image (`--main`; else the first mapped executable, else the first `--load`); names, handles and addresses of mapped images answer with the mapping; truncation follows Windows (size-1 characters, result size, `ERROR_INSUFFICIENT_BUFFER`); everything else goes to kernel32 |
| `LoadLibrary{A,W,ExA,ExW}` (any module) | a mapped image: its base, reference counted; an Xbox-only module: a read-only page with a minimal PE header (a virtual module); a manual module or a DLL in the search directories, by name or by a path inside one: mapped now; data-file loads and the rest: the OS |
| `GetProcAddress` (any module) | mapped image: its exports; virtual module: the shim table (unknown name: NULL, error 127); OS module: the "*" shim of that name if there is one, else the real export |
| `FreeLibrary` (any module) | mapped image: reference count down; an image first mapped by `LoadLibrary` and imported by no other image is unloaded at 0; virtual module: TRUE |
| `XGameRuntimeInitialize*`, `XGameRuntimeIsFeatureAvailable` | succeed; every feature available |
| `XTaskQueue*` | Manual, ThreadPool, SerializedThreadPool and Immediate ports; composite queues; delayed callbacks; waiters (re-armed after each signal); monitors; terminate (pending callbacks run with `canceled = true`); duplicate and close reference counted; a process queue (ThreadPool/ThreadPool) created on first use |
| `XAsync*` | Begin, Schedule, Complete, GetStatus (with wait), GetResultSize, GetResult (identity and buffer checked), Cancel, Run; DoWork on the work port, the completion routine on the completion port, Cleanup once |
| `XUser*` | one signed-in offline user "Player" (fixed XUID); add, duplicate, close, find, compare, gamertag, state, adult age group, every privilege granted; change events register and never fire |
| `XGameSave*`, `XGameSaveFilesGetFolderWithUi*` | folders under `--save-dir`: `saves\<xuid>\<configId>\<container>\<blob>`, names %-escaped; updates staged and written per blob through a temporary file; sync and async reads, enumeration, deletes; 256 MB quota |
| `XPackage*` | one installed game package at the package root with the identity from `MicrosoftGame.config`; mount returns the package root; chunks ready; installation monitors at 100 %; no DLC |
| `XStoreCreateContext`, `XStoreQueryGameLicense*` | an active, full, never-expiring license |
| `XSystem*`, `XThread*`, `XGameGetXboxTitleId`, `XGameEventWrite`, `XError*`, `XPersistentLocalStorage*` | device type 7 (Series X) unless `--device-type`; fixed console and sandbox ids; time sensitivity per thread; title id from the config; events dropped; local storage in `--save-dir\localstorage` |
| other `xgameruntime.dll` functions of the public headers | not implemented: E_NOTIMPL, or 0 for bool and void functions; first call logged |

The GDK shims also answer for any `api-ms-win-*gaming*` or `ext-ms-win-*gaming*` module name, for symbols starting
with `X`; other functions of those API sets go to System32.

A shim that reports a last error is entered through a generated wrapper that writes `TEB.LastErrorValue` after the
managed method returns, because the runtime's return path resets the value a managed `SetLastError` set.

## The GDK thunk ABI

GDK titles link `xgameruntime.lib`, which loads `xgameruntime.dll`, looks up `InitializeApiImpl`,
`InitializeApiImplEx`, `InitializeApiImplEx2`, `QueryApiImpl`, `UninitializeApiImpl` and `XErrorReport`, and implements
every `X*` call as `QueryApiImpl(&clsid, &iid, &object)` followed by a COM-style call through the object's vtable.
`libHttpClient.GDK.dll` contains this library.

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

The loader ships no table. It needs, for each interface the game requests, the clsid and iid and the function behind
each vtable slot (slot 3 onwards). With an empty table, `HCInitialize` of `libHttpClient.GDK.dll` fails with
`0x89240107` after its first `QueryApiImpl`.

## Running it

```powershell
dotnet build FiveStar.slnx
$dump = "<your dump folder>"
fivestar-loader --load $dump\XboxBvhBuilder.dll --load $dump\Bink2Scarlett.dll --call all --unload --log run.log
```

| Option | Meaning |
|---|---|
| `--load <dll>` | map an image (repeatable) |
| `--call <export>` / `--call all` | call an export that was disassembled first (`--list-safe-calls` lists them); others are refused |
| `--main <name>` | the image that plays the process executable |
| `--patch-peb` | also point `PEB.ImageBaseAddress` at the main image (restored at exit) |
| `--save-dir <dir>` | root of saves and local storage; default `%LOCALAPPDATA%\FiveStar\GdkData\<TitleId>` |
| `--gdk-abi <file>` | the thunk ABI table |
| `--package-root <dir>` | where `MicrosoftGame.config` is read; default the main image's directory |
| `--device-type <n>` | `XSystemGetDeviceType` result |
| `--manual <a.dll,b.dll>`, `--system-crt`, `--search <dir>` | which modules the loader maps itself |
| `--unresolved stub\|trap\|error`, `--no-shim <module!symbol>` | unresolved-import handling; disable one shim |
| `--no-entry`, `--no-exports`, `--unload`, `--work-dir <dir>` | skip DllMain; skip the export listing; unload at the end; folder of the TLS host DLLs |
| `--log <file>`, `-v` | full log of every import and shim call; the same on the console |

The output lists the images, an import table per image and module, the totals and the exports. Tests:
`dotnet test tests/FiveStar.Loader.Tests`; with `FIVESTAR_DUMP` set to the dump folder they also load the six Xbox
DLLs (825 imports, none unresolved), run the inspected exports and read the package identity.

## Unknown

- The `(clsid, iid, slot) -> function` table of the thunk ABI, and the API-set names the game imports GDK functions
  from (the alias is a pattern).
- Signatures inferred from call sites: `InitializeApiImpl*`, `QueryApiImpl`, `XErrorReport`, `XMemVirtualQuery`,
  `XMemGetWorkingSetStatistics`; the meaning of the `XMemVirtualQuery` tail (+0x30); the XALLOC attribute layout.
- XUser and XGameSave error HRESULT values; the members of `XPackageDetails` after `installing`; the
  `XStoreGameLicense` layout; the array sizes of `XSystemAnalyticsInfo`; the member order of
  `XPersistentLocalStorageSpaceInfo`; what the console returns as package identifier.
- `XGameRuntimeInitializeWithOptions` options; waiters re-arming after each signal; `XGameSaveGetContainerInfo` on a
  missing container (S_OK, no callback).
- D3D12.X beyond root signatures, and whether its root-signature versions and flags match the PC ones.
