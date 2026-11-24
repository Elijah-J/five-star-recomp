# Debugging

Three tools find faults. Each one sees a different set of them.

| Tool | Sees | Use it for |
|---|---|---|
| The loader's fault reporter (`fivestar-loader --run`, [loader.md](loader.md#running-the-executable)) | hardware exceptions whose first 12 frames include a mapped image, from inside the process: image RVA, `.pdata` function, code bytes, registers, the last traced shim calls | faults in game code. It is the first thing to read. |
| `fivestar-dbg` (`src/FiveStar.Debugger`) | every exception in the process, from outside it, at first and second chance: module loads, debug output, symbolised stacks, minidumps, the page heap | faults inside coreclr, ntdll's heap manager, a driver or the fault reporter itself, heap corruption, and crashes that leave no log |
| `--d3d12-debug`, `--d3d12-gpu-validation` ([d3d12x-shim.md](d3d12x-shim.md#debug-layer)) | D3D12 API misuse and GPU faults: debug-layer messages and the DRED report after a device removal | `DXGI_ERROR_DEVICE_REMOVED`, hangs on the GPU, wrong descriptors or barriers |

## fivestar-dbg

```powershell
fivestar-dbg [options] [--] <program> [args...]
fivestar-dbg --debug-heap --break-on AccessViolation --dump-on-crash crash.dmp `
  --symbols "srv*C:\symbols*https://msdl.microsoft.com/download/symbols" `
  -- fivestar-loader --load $dump\game.exe --run --log run.log
```

| Option | Meaning |
|---|---|
| `--debug-heap` | ntdll's debug heap and the full page heap, for this process only (below) |
| `--break-on <code,...>` | a full record (registers, stack) already at first chance; hex codes or names (`AccessViolation`, `HeapCorruption`, `StackOverflow`, ...) |
| `--symbols <path>` | folders and `srv*<cache>*<url>` entries; default `_NT_SYMBOL_PATH`, else exports only |
| `--dump-on-crash <file>`, `--full-dump` | a minidump at the first second-chance or `--break-on` exception; with all process memory |
| `--log <file>` | a copy of the output |
| `--loader-log <file>` | the fivestar-loader log; default the program's own `--log` value |
| `--children` | also debug the processes the program starts (`DEBUG_PROCESS`) |
| `--timeout <s>` | end the program after s seconds (exit code 124) |

The exit code is the program's. The tool builds for NativeAOT (`IsAotCompatible`); it calls kernel32, ntdll and
System32's dbghelp through `LibraryImport`.

### Behaviour

- **Exceptions.** Each first-chance exception is one line: chance, code and name, address, `module+offset`, symbol, and
  for an access violation the access and address. After 20 of one code they are only counted; the totals come at the
  end. First-chance exceptions go back to the program (`DBG_EXCEPTION_NOT_HANDLED`). A second-chance exception gets a
  full record with every thread's stack.
- **Breakpoints.** ntdll's startup breakpoint passes silently. Any later one (a heap check, a verifier stop, an
  `__debugbreak`) gets a full record and is stepped over, as a debugger's `g` does.
- **.NET hosts.** An unhandled exception in a .NET process (the loader is one) ends it through the runtime's fail-fast
  without a second chance, so `--break-on` is what gets the stack and the dump.
- **Stacks.** `StackWalk64` unwinds through the `.pdata` of every image dbghelp knows. JIT code has no image; the walk
  stops at the first frame outside an image.
- **Images the loader maps.** The OS never reports them. At each report the tool reads the new lines of the loader log
  (`== loading <path>`, `mapped <name> @ 0x<base> (size 0x<size>`, `unloaded <name>`), registers those images with
  dbghelp and names addresses in them as `game.exe RVA 0x1234 (mapped)`.
- **Symbols.** A name dbghelp takes from the export table is marked `(export)`: it is the nearest export, possibly a
  different function. System32 has no `symsrv.dll`, so for `srv*` entries the tool reads the image's CodeView record
  and downloads `<pdb>/<GUID><age>/<pdb>` into the cache (symstore layout) the first time a module appears in a report.

### Debug heap

Nothing system-wide changes, and no registry key is written:

| Mode | How | Catches |
|---|---|---|
| default | `_NO_DEBUG_HEAP=1` in the program's environment, which turns off the debug heap ntdll enables for every process started under a debugger | nothing extra: the heap behaves as in a normal run |
| `--debug-heap` | no `_NO_DEBUG_HEAP`, so tail checking, free checking and parameter validation stay on; and at the create-process event, before any user-mode code runs, `FLG_HEAP_PAGE_ALLOCS` (0x02000000) is set in the debuggee's `PEB.NtGlobalFlag` (offset 0xBC). ntdll then loads verifier.dll and logs `page heap enabled with flags 0x2` (full page heap) | an overrun of a block whose size is a multiple of 16 faults on the writing instruction (guard page); other overruns and writes after free are reported as a verifier stop at the free |

The full page heap costs at least two pages per allocation; a long game run under it needs memory to match.

### Output

```text
[   0.025] 40756 page heap on
[   0.027] 40756/54844 debug: Page heap: pid 0x9F34: page heap enabled with flags 0x2.
[  12.435] 40756/54844 first-chance c0000005 AccessViolation at 0x7ff975a9ca06 ntdll.dll+0x16ca06 ntdll!_memset_spec_avx_ermsb+0xc6 (write at 0x2f9a4767000)
[  12.437] 40756 exited with 0xc0000005 (c0000005 AccessViolation)
```

Tests (`tests/FiveStar.Debugger.Tests`, Windows): the test assembly is also the debuggee. An access violation in ntdll
is reported with module, symbolised frame and a minidump; a one-byte heap overrun passes unnoticed on the normal heap
and faults on the write under `--debug-heap`.

## Transient pool watch

`fivestar-loader --watch-transient` follows the game's transient-memory pool, the per-frame memory its command-list
contexts take constants and small objects from (set up at 0x14247ac20; globals pool base 0x1475DF1B8, block records
0x1475DF1C0, count 0x1475DF1C8, block size 0x1475DF1CC). Each 0x58-byte block record holds the block's address
(+0x10), its retire token (+0x18) and a list (+0x48) of 0x18-byte nodes {next, pooled allocator or list, token}
stored inside the blocks: 0x140188430 links one when a context takes a pooled allocator, and 0x144e9b480 (or
0x1450b8970) walks the list when the block is taken again, then starts a new one in the block's last 0x18 bytes. The
story-load faults at 0x144e9b4b2 read such a list with garbage nodes (0x2, 0x200000001).

Every 5 ms a thread walks every list; a next pointer outside the pool or an object pointer outside user space, seen
on three polls in a row, is logged once with the block, the node and the 0x60 bytes around it, followed by every
queue as last seen ([queue progress](d3d12x-shim.md#queue-progress)). Root CBV, SRV and UAV addresses (list slots 41
to 46) that fall in the pool are kept in a ring of 4,096 with the list's pipeline; the report lists the newest 24 in
the corrupted block, which names the GPU work that used it. The game's code is read, never changed.

## Memory map

`fivestar-loader --memory-map <s>` walks the address space (VirtualQueryEx) every s seconds and logs `[memory]` lines:

| Line | Holds |
|---|---|
| `#n` | private commit by owner: `XMemVirtualAlloc` (plain allocations of that shim), `XMemAlloc` blocks, heaps (process heaps and blocks with a heap segment signature), game images, everything else as `anonymous`; the managed heap's commit (part of the anonymous); mapped and image commit |
| `private by protection` | 0x4 read-write, 0x404 read-write write-combined (upload and custom heaps), ... |
| `anonymous by size` | counts and bytes per size class, and the committed sizes that hold the most |
| `heaps` | HeapSummary of every process heap |
| `since the last map` | new and gone allocations, by owner and size |
| `largest` | the 20 largest: base, owner, committed of reserved, protection; an anonymous one with the modules its first page points into (`[nvgpucomp64.dll x3]`) |
| `gpu memory` | QueryVideoMemoryInfo per adapter: local and non-local usage and budget |

A walk of 200,000 to 300,000 regions takes 0.4 to 1 s. `--min-free-memory <GB>` and `--max-commit <GB>` poll the machine
every 0.25 s and end the process with a last map (exit code 5).

**Story load, before the compile budget** (RTX 4090, 64 GB, programs on demand): private 17 GB at 47 s, 26 GB at 96 s,
54 GB at 147 s, 60 GB at 165 s; the growth is anonymous memory in power-of-two regions, almost fully committed (8 GB,
4 GB x4, 2 GB x5, 1 GB x4). They are the NVIDIA compiler's arenas: six driver compiles of 1 to 3 MB mesh-shader
pipelines ran on the build threads (stacks in nvgpucomp64.dll under CreatePipelineState) from 100 s on, each growing
its arena 1, 2, 4, 8 GB. Everything else stayed flat: heaps 1.5 to 2 GB, `XMemVirtualAlloc` 4.5 GB, game images
0.2 GB, managed heap 1 to 1.5 GB, write-combined 5.2 GB. [d3d12x-pipelines.md](d3d12x-pipelines.md#stand-ins) has
the compile budget.
## Unknown

- Whether ntdll keeps honouring `PEB.NtGlobalFlag` set at the create-process event in later Windows releases (checked
  on Windows 11 build 26200).
