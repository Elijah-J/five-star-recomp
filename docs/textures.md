# Streamed textures (`.ttpf`)

The textures live in page files under `data/ttdb/` and `pack/*/data/ttdb/`. A page file maps stored pages into a
virtual address space and lists the textures in that space. `fivestar texture` extracts mip 0 of every readable
texture as DDS and reports what cannot be read yet.

## RDS5 header and page table

All values little-endian.

| Offset | Size | Meaning |
|---|---|---|
| 0x00 | 4 | `RDS5` |
| 0x04 | 4 | n = page entries + 1 (a trailing sentinel slot, normally zero) |
| 0x08 | 4 | Virtual address of the last page (mod 2^32) |
| 0x0C | 12 | Unknown |
| 0x18 | 8 per entry | Page entries `{u32 lo, u32 hi}` |

| Field | Bits | Meaning |
|---|---|---|
| `lo` | 0-23 | File offset / 0x100 |
| `lo` | 24-31 | Flag byte |
| flag | 2-4 | Codec: 0 stored, 1/3/4/5/6/7 BCPack for BC1/BC3/BC4/BC5/BC6H/BC7 |
| flag | 5 | zlib stream |
| flag | 6-7 | Adds 0x1000 steps to the virtual address |
| `hi` | 0-19 | Virtual address / 0x4000 |
| `hi` | 20-31 | Unknown tag |

A page's stored bytes run to the next higher file offset of any entry (or the end of the file); entries with no bytes
are dropped. A page's virtual size is the distance to the next entry's address (0x4000 for the last).

## ETPF section

At `align(0x18 + 8 * (n - 1), 0x100)`; in some files it overlaps the sentinel slot.

| Offset | Size | Meaning |
|---|---|---|
| 0x00 | 4 | `ETPF` |
| 0x04 | 4 | 634 in every file |
| 0x08 | 4 | Descriptor count |
| 0x0C | 4 | Section size |
| 0x10 | 16 per texture | Descriptors |
| then | 8 per texture | 64-bit name hashes, sorted |

| Descriptor word | Bits | Meaning |
|---|---|---|
| `d0` | 0-13 | Width - 1 |
| `d0` | 14-27 | Height - 1 |
| `d0` | 28-31 | Mip count (clamped; often 0 in mode-0 textures) |
| `d1` | 3-9 | DXGI format |
| `d1` | 24-31 | Layout mode: bits 0-2 base (0 = 64 KB tiles, 1-3 = 4 KB tiles, 4 unknown), bit 3 cube map, bit 4 no layout effect |
| `o0` | 0-20 | Virtual address / 0x1000 |
| `o1` | | Unknown |

## Memory layout

A texture's span runs from its address to the next texture's address (the last one to the end of the last page).
Mips are stored smallest first, so mip 0 is the last tile-padded block of the span.

Swizzle patterns give each bit of an element's index inside a tile, least significant first, as the next x or y bit
of its position. An element is a 4x4 block for BC formats and a pixel otherwise. Tiles are laid out row-major.

| Element size | 4 KB tile (bases 1-3) | Tile | 64 KB tile (base 0) | Tile |
|---|---|---|---|---|
| 1 byte | `xxxxyyyyyxyx` | 64x64 | `xxxyyyxyyxyxyxyx` | 256x256 |
| 2 bytes | `xxxyyyxyxyx` | 64x32 | `xxxyyyxyxyxyxyx` | 256x128 |
| 4 bytes | `xxyyyxyxyx` | 32x32 | `xxyyxyyxyxyxyx` | 128x128 |
| 8 bytes | `xyyxxyxyx` | 32x16 | `xyxxyyxyxyxyx` | 128x64 |
| 16 bytes | `yyxxyxyx` | 16x16 | `xyxyyxyxyxyx` | 64x64 |

| Case | Where mip 0 is |
|---|---|
| Mip 0 larger than one tile | The last `padded size` bytes of the span |
| Mip 0 fits in one tile (mip tail) | Swizzled with the shortest pattern prefix that covers it, starting half a tile before the span's end |
| Cube map with one mip | Six faces of `span / 6` bytes, mip 0 at the end of each face (as above) |
| Cube map with mips | Not decoded |

## Page codecs

zlib pages decode with any zlib inflater. Codecs 1 and 3 to 7 are the console's BCPack format, one per BC format; no
decoder exists. A BCPack page starts with a 48-byte header of 6 rows by 8 lanes (byte `8 * row + lane`):

| Row | Meaning |
|---|---|
| 0 | Lane model byte |
| 1 | Refill bytes the lane reads + 3 |
| 2-5 | 32-bit rANS start state, big-endian |

The coder renormalises byte-wise with 16-bit probabilities and lower bound 2^24. Model bytes 0xDB, 0xDC, 0xDD, 0xDE
give one symbol the frequency 65536 - E (E = 2, 4, 8, 32) at cumulative start 1. A lane that codes only that symbol N
times (N = blocks per page, or twice that for 16-byte blocks) has a start state that depends only on N;
`texture bcpack` checks these lanes. After the header, the first refill byte of each lane with a budget above 3
follows in lane order.

## DDS output

One mip, `DDS_HEADER` with `DDSD_CAPS | HEIGHT | WIDTH | PIXELFORMAT | LINEARSIZE`, pitch field = bytes of one face,
FourCC `DX10`, then `DDS_HEADER_DXT10` {format, 2D, cube flag for 6 faces, array size 1}. Files are named
`<index>_<hash>_<width>x<height>_<format>.dds`.

## Commands

```
fivestar texture extract  <file.ttpf> <out dir> [--limit N] [--only i,j] [--no-dds]
fivestar texture coverage <dir>                  textures readable without BCPack, per file and in total
fivestar texture codecs   <dir>                  pages and stored bytes per storage kind
fivestar texture pages    <file.ttpf>            three sample entries per flag value, then counts per flag and kind
fivestar texture bcpack   <file.ttpf> [--limit N] [--codec C]
fivestar texture bcpack   <dir> --stats
```

Statuses: `ok`, `skip_fmt_N` (format not supported), `skip_modeN` (no swizzle known), `skip_cube_span`,
`skip_size`, `skip_cube_mips`, `skip_unsupported_codec` (mip 0 on a BCPack page), and in `coverage`
`bcpack_codecN`.

## Unknown

- BCPack data lanes and index coding.
- Layout base 4, and what separates bases 1, 2 and 3.
- Mipped cube map layout.
- `o1`, `o0` bits 21-31 and the page tag.
