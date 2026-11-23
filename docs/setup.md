# Setup

`fivestar setup <dump folder>` prepares everything the port needs from your own dump. It runs a fixed list of steps
in dependency order and records each finished step, so a second run skips what is done and redoes only what changed.
An interrupted run continues where it stopped.

```
fivestar setup <dump folder> [--work <dir>] [--only <step,...>] [--force] [--limit N] [--step K] [--jobs N]
                             [--dxc <dxc.exe>] [--gdk-lib <xgameruntime.lib>]
fivestar setup <dump folder> --status
fivestar setup <dump folder> --clean-cache
```

## Steps

| Step | Does | Output in the work folder | Depends on |
|---|---|---|---|
| `verify` | reads the title id and version from `MicrosoftGame.Config`; checks `shaders.rpf` and the texture page files exist. Another title fails; another version gives a warning | none | |
| `shader-extract` | `fivestar shader extract` of `shaders.rpf` | `shaders/dxbc`, `shaders/fssm` | `verify` |
| `shader-translate` | `fivestar shader translate --mesh-fallbacks` of every program, compiled with dxc through the shader cache | `shaders/hlsl` (HLSL, DXIL, `results.json`, `variants.json`), `shader-cache` | `shader-extract` |
| `pipeline-cache` | `fivestar shader psocache` of the console pipeline cache, matched to the programs | `pipelines` (`pipelines.json`, `code/`) | `shader-extract` |
| `textures` | `fivestar texture extract` of every page file | `textures/<page file path>/*.dds` | `verify` |
| `gdk-abi` | `fivestar platform gdk-abi` from the PC GDK's `xgameruntime.lib`, for `fivestar-loader --gdk-abi`. Without a GDK it finishes with a note | `loader/gdk-abi.txt` | `verify` |

`variants.json` lists every compiled program with its DXIL forms (native, wave32 variant or mesh superstep form, mesh fallback) and wave
floor, so the runtime picks the form for the GPU without reading HLSL (see
[shader-translation.md](shader-translation.md#wave-size), Variant selection). Mesh fallbacks are compiled for every
mesh program, for GPUs without mesh shaders.

A failed step stops the steps that depend on it; the others still run. `--only` runs the named steps (their
dependencies must be done); `--force` redoes them even when they are up to date.

## Work folder

The default is `%LOCALAPPDATA%\FiveStar\<titleid>-<version>\`, for example `%LOCALAPPDATA%\FiveStar\7952987A-1.0.2.1\`;
`--work <dir>` picks another. A full run needs about 16 GB for the shaders (about 610 KiB per program), 0.5 GB for the
pipeline cache and 0.9 to 2.2 times each page file for its textures (58 GB of page files). Before each step setup
compares its estimate (twice the page files for textures) with the free space of the drive and stops the step with a
message when it does not fit; on a re-run it counts only what is not done yet.

## State and resume

`state/<step>.json` records a finished step: a hash of its inputs, a hash of the tool build, when it finished, how
long it took and a summary. `state/<step>.running` exists while a step runs; it stays when the process ends early
or the step fails, and the step then shows as interrupted.

| Step | Inputs | Tool |
|---|---|---|
| `verify` | path, size and write time of the config, `shaders.rpf` and the page files | a version number in the code |
| `shader-extract` | `shaders.rpf` (path, size, write time) | FiveStar.Shaders |
| `shader-translate` | `shaders/dxbc/_manifest.tsv` (it lists every container hash), the sample options | the shader-cache identity (below) |
| `pipeline-cache` | `shaders.rpf` | FiveStar.Shaders, FiveStar.Formats |
| `textures` | every page file, the sample options | FiveStar.Formats |
| `gdk-abi` | the GDK library and the dump's modules, or none | FiveStar.Formats |

A step's inputs also include the records of the steps it depends on, so a redone step redoes what follows it. A
step runs when it has no record, its record differs, its outputs are gone or it was interrupted. Within a step:

- `shader-translate` resumes through the shader cache: every finished translation and compile is kept, so a re-run
  only does the programs that were not reached.
- `textures` writes `state/textures/<page file>.done` with the file's fingerprint after each page file and skips files
  whose mark is current.
- `shader-extract` and `pipeline-cache` take about a minute and start over.

Every step prints `[n/6] <title>`, the reason it runs, its expected time and the overall time left. The expected time is
the last measured duration of the step, or before the first run an estimate: 3.2 s of one job per shader program
(about an hour for all programs on 24 jobs, 5.8 hours on 4), 100 MiB of page files per second for textures. The shader and texture steps print
progress, time left and finish time every 10 seconds.

`--limit N` and `--step K` make a sample: the first N of every K-th program, N textures per page file. A different
sample counts as different inputs.

## Requirements

`shader-translate` needs dxc, found as for `fivestar shader translate`: `--dxc`/`FIVESTAR_DXC`, then the Windows SDK,
then `PATH`. The RDNA code is decoded in-process, so LLVM is not needed. When dxc is missing setup says so and how to
provide it before any step runs.

## Shader cache

The cache (`shader-cache/`, layout in [shader-translation.md](shader-translation.md#cache)) keys every entry by a
hash, so the hash does not tell which build made it. Each run of `fivestar shader translate --cache` (which setup
uses) therefore records the entries it reads or writes in `shader-cache/used/<identity>.txt`, one entry path per
line, appended as it goes. The identity hashes the translator identity (FiveStar.Shaders and the
translation options) and the dxc identity.

`--clean-cache` removes every entry the current identity's record does not list, the records of other identities and
left-over temporary files, then prints what it removed and kept. Entries a new build reuses (the DXIL of unchanged
HLSL) are in its record, so it runs only after `shader-translate` finished with the current build.

## Status

`--status` prints each step's condition (done, not run, interrupted, stale with the reason, or waiting for an
earlier step) with its last run, the size of each output folder, the free space and the shader cache split into the
current build and older builds. Pass the same `--limit`/`--step` as the run, or the sampled steps show as stale.
