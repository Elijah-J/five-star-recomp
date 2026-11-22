# Textures and BCPack

Textures live in `.ttpf` page files (magic `RDS5`) outside the archives: a page table, then an `ETPF` section of
texture descriptors and name hashes. Pages are stored raw, zlib-compressed, or BCPack-compressed. Texture memory uses
the console's 4 KiB and 64 KiB tiled layouts; the exporter undoes the tiling and writes DDS. Layout details:
`docs/textures.md` in the repository.

| Result | Textures |
|---|---|
| Export cleanly | 315,483 of 357,115 (88.3%), over all 57 `.ttpf` files |
| Blocked, mostly by BCPack pages | 41,632 |

## BCPack

BCPack is the Xbox Series hardware texture codec. Microsoft describes it as an entropy coder for BCn data that
separates colour endpoints from palette indices and codes them with rANS. The page codec number equals
`DSTORAGE_BCPACK_MODE` (1 = BC1 ... 7 = BC7).

Known from the 108,605 BCPack pages in the game:

| Item | Finding |
|---|---|
| Header | 48 bytes: 8 lanes, each with a mode byte, a refill budget and a 32-bit rANS start state |
| Coder | byte-wise rANS, 16-bit probabilities, lower bound 2^24 |
| Verified | lanes that code a single constant symbol decode exactly |
| Stream | after the header, all lanes read from one shared byte stream in decode order |

## Unknown

- The probability models selected by the lane mode byte.
- How block indices are coded.

A software decoder exists nowhere public. The most direct route is Microsoft's encoder (`XBTC`, part of the GDK) as a
reference to compare against.
