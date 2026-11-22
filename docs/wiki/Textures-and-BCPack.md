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
`DSTORAGE_BCPACK_MODE` (1 = BC1 ... 7 = BC7). No decoder exists yet.

### Verified

From the 108,605 BCPack pages in the game (`fivestar texture bcpack <dir> --stats` and `--models`):

| Item | Finding |
|---|---|
| Pages | 4 KiB to 4 MiB of BC data each; most are BC1 (102,725) and BC7 (5,167) |
| Header | 48 bytes, 6 rows of 8 lanes: model byte, budget byte, 32-bit start state (big-endian) |
| Coder | byte-wise rANS, 16-bit probabilities, lower bound 2^24 |
| Model byte | splits into a family (bits 5-7) and a parameter (bits 0-4); written `family.parameter` below |
| Near-constant models | 6.27 to 6.30 give one symbol 65536 - E (E = 2, 4, 8, 32) at cumulative start 1. BC1 lane 1 is always 6.30 and its state is that of N such symbols for 101,833 of 102,725 pages |
| First bytes | right after the header, every lane whose budget is not 3 reads one byte, in lane order. Budgets 0-2 count, so the budget wraps at 256 |
| Twin lanes | two lanes with identical headers read equal first bytes in 6,606 of 6,878 pairs (96%). Counting only budgets above 3 gives 92.5%, so the wrap is real |
| Interleaving | twin lanes keep reading identical bytes side by side (`x x`, or `a b a b` when each reads two at once): lanes read the shared stream in lane order, a renormalisation at a time |

Budgets are one byte, but a 4 MiB page stores about 3.6 MB. So a lane does not stop after one budget's worth of bytes.
Lane headers also turn up later in the stream, spread one byte per round like the first header (for example four lanes
with model 1.16, budget 3 and states `0x0201....` in a BC4 page). The likely reading is that a lane runs through
several segments, each opening with its own 6-byte header. That is not proven yet.

### Lane roles

The most common models per lane. Lanes with families 0-7 and parameters 24-30 behave like endpoints; family 1 with
parameters 16-22 like indices or block modes.

| Codec | Lane 0 | Lane 1 | Lanes 2-7 |
|---|---|---|---|
| BC1 | 1.17-1.20 | 6.30 always | 1.25-1.28 or 6.28/6.29; lanes 2/5, 3/6 and 4/7 share a model in 50-63% of pages, other pairs in 7-16% |
| BC4 | 1.16-1.21 | 6.30 always | 2-3: mixed families, 1.29 most; 4-7: 1.16-1.19 |
| BC5 | 1.16-1.22 | 6.27 always | 2-3: mostly 1.25-1.29; 4-7: 1.16-1.19 |
| BC3 | 1.16-1.20 | 6.27 always | 2 and 7 endpoint-like, 3-6 1.17-1.19 |
| BC6H | 1.16-1.18 | 1.16-1.20 | 2-4: mostly 5.27/5.28; 5-7: 1.25-1.27 |
| BC7 | 1.16-1.17 | 1.16-1.17 | all families, parameters 22-30 |

For the 16-byte formats the near-constant lane codes two symbols per block. The BC1 pairing fits lanes 2-4 and 5-7
being the same three channels of the two colour endpoints; that is a guess.

### Known plaintext

There is no copy of a BCPack page stored plainly elsewhere:

- 48 name hashes appear in more than one page file; none is BCPack in one and plain in another.
- 20 groups of textures share their first 2 KiB of mips with a plain copy, but the rest of the data differs.
- Matching whole zlib pages across textures gave 5 candidates, all different slices of texture arrays.

The codec seems to be chosen from the content, so identical data gets the same codec.

Mip tails give partial plaintext instead. The first page of a mipped texture starts with zero padding. For BC1
1024x1024 with 6 mips and layout mode 2 (7,174 BCPack pages of 12 KiB), plain siblings show zero blocks 0-95, 97-127,
132-159 and 176-191. In the BCPack copies the bytes for these zero runs repeat exactly across textures and across page
sizes (512-block pages show the same runs), for example `4f d0 54 ec 0e 8f 9c 4c ec`, read by two to four lanes in
step.

### Ruled out

- The second half of a page is not raw BC indices: its bytes have 7.999 bits of entropy and its 2-bit fields are uniform.
- The zero-run bytes are not one static model coding one symbol from the lower bound. All frequencies 1-6000 and all
  start positions were searched.

### Open questions

- What the families and parameters mean, beyond the near-constant 6.27-6.30.
- How a lane continues past its first budget: segments, and when a new header is read.
- Why zero runs code to the same bytes in every texture. rANS bytes normally depend on everything coded after them,
  so the coder state must restart near these runs.
- The block order inside a page (tiled memory order is the natural guess) and how many symbols each lane codes per
  block.

Microsoft's encoder (`XBTC`, part of the GDK) would answer most of these: compressing known BCn data and comparing.
