# Platform files

The package's non-game files: the shipped DLLs, the OS volume `gameos.xvd` and `microsoft_gameconfig.bin`, the
compiled `MicrosoftGame.Config`. `fivestar platform` reads their headers.

## Package contents

| File | What it is |
|---|---|
| `layout_*.xml` | Install chunks: 1000-1014 base game (1001 exe, 1002 DLLs, 1003 all base `.ttpf`, then one RPF per chunk), 3000-3008 `pack\*`, 3009 `Marker=Launch` |
| `microsoft_gameconfig.bin` | Registry hive (`regf`) compiled from `MicrosoftGame.Config` |
| `gameos.xvd` | OS volume (XVD, encrypted); not game data |
| `Catalog000.bin` | Data-only PE with an encrypted payload; not game data |
| `*.dll` | Runtime and middleware modules |

## PE modules

Read per the PE/COFF specification: COFF header, optional header (PE32 or PE32+), data directories 0 (exports),
1 (imports), 2 (resources), 6 (debug), 13 (delay imports), section table.

| Table | Entry | Fields |
|---|---|---|
| Imports | 20 bytes | lookup table RVA, time stamp, forwarder chain, name RVA, address table RVA; ends at an all-zero entry |
| Delay imports | 32 bytes | attributes (bit 0: RVAs, else virtual addresses), name, module handle, address table, name table, ... |
| Thunk | 4 or 8 bytes | Top bit: import by ordinal (low 16 bits); else RVA of `{u16 hint, name}` |
| Debug | 28 bytes | type at +12 (2 = CodeView), size at +16, file offset at +24; CodeView `RSDS` has the PDB path at +24 |

Ordinal-only imports of `oleaut32.dll` are shown by name (SysAllocString = 2 ... VariantCopy = 10).

## XVD header

At file offset 0x200, field layout of the public xvdtool `XvdHeader`:

| Offset | Size | Meaning |
|---|---|---|
| 0x00 | 8 | `msft-xvd` |
| 0x08 | 4 | Volume flags |
| 0x0C | 4 | Format version |
| 0x10 | 8 | Creation time (FILETIME) |
| 0x18 | 8 | Drive size |
| 0x20 | 16 | VDUID |
| 0x30 | 16 | UDUID |
| 0x40 | 32 | Top hash block hash |
| 0x60 | 32 | Original XVC data hash |
| 0x80 | 4 | Type (0 fixed, 1 dynamic) |
| 0x84 | 4 | Content type |
| 0x88 | 4 | Embedded XVD length |
| 0x8C | 4 | User data length |
| 0x90 | 4 | XVC data length |
| 0x94 | 4 | Dynamic header length |
| 0x98 | 4 | Block size |

## Registry hive

`regf` at 0x00, root key cell offset at 0x24. Cell offsets are relative to 0x1000; each cell starts with an i32 size.

| Cell | Fields |
|---|---|
| `nk` key | flags at +2 (0x20 = Latin-1 name), subkey count +0x14, subkey list +0x1C, value count +0x24, value list +0x28, name length +0x48, name +0x4C |
| `vk` value | name length +2, data length +4 (bit 31 = data inline in the next field), data offset +8, type +0xC, flags +0x10 (1 = Latin-1 name), name +0x14 |
| `lf`, `lh` | u16 count at +2, 8-byte entries from +4 |
| `li`, `ri` | u16 count at +2, 4-byte entries from +4 (`ri` entries are lists) |

## GDK thunk ABI

Every GDK call compiles to a thunk from `xgameruntime.lib`: `QueryApiImpl(&clsid, &iid, &object)`, then a call through
vtable slot n of the object, then `Release` (slot 2). The console and PC libraries use the same ids and slots for the
same functions, so the public PC GDK names the console thunks.

| Source | How the thunks are found |
|---|---|
| `xgameruntime.lib` (PC GDK, `windows\lib\x64`) | each COFF function with a REL32 relocation to `QueryApiImpl`; the GUIDs from the `_GUID_xxxxxxxx_xxxx_...` symbols its `lea rcx/rdx, [rip+...]` refer to (`mov rdx, rcx` when both are equal); the slot from the first `mov rax, [rax+disp]` after the query with disp / 8 ≥ 3 |
| a console module | the same instruction pattern around a `call`, accepted only for class ids that the library uses |

The October 2025 PC GDK (2510) has 418 named thunks. `libHttpClient.GDK.dll` (built with the October 2025 GRDK) has 408
distinct thunks; 365 match by (clsid, iid, slot). The other 43 are console-only, most of them XUser through interface
id `26f3c674-a2fe-44fa-b6c4-a323bc94ff53`.

## Commands

```
fivestar platform imports <dir>         which functions of which DLLs the modules import, system DLLs last
fivestar platform pe      <module> ...  header, sections with entropy, imports, exports, version, PDB, resources
fivestar platform xvd     <file.xvd>
fivestar platform regf    <hive>        every key and value
fivestar platform gdk-abi <xgameruntime.lib> [module ...] [--out file]
```

`gdk-abi` writes the table for `fivestar-loader --gdk-abi` (one `clsid iid slot function` line per library thunk) and,
for each module, its thunks without a name as comments. The GDK is a free download from Microsoft
(github.com/microsoft/GDK, releases); the repository contains no table.

## Unknown

- The XVD fields from 0x2A0 on (printed as hex).
- The `Catalog000.bin` payload.
