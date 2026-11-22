<p align="center"><img src="docs/assets/banner.svg" alt="Five Star Recomp" width="640"></p>

Five Star Recomp is an effort to run the Xbox Series version of **Grand Theft Auto VI** natively on a Windows PC,
from a copy of the game you own. It targets the Xbox Series release, version 1.0.2.1 (title ID `7952987A`).

The Xbox Series uses a regular x86-64 processor, so GTA VI's own code can run on a PC as it is. What does not
carry over is everything around it: the console's operating system services, its graphics API, its GPU shaders and
its data formats. This project rebuilds those pieces:

- **Shaders.** The game ships its GPU programs only as native AMD RDNA machine code. A translator turns each one
  into HLSL, which the standard DirectX compiler builds for any PC graphics card.
- **Platform.** A loader maps the game's modules on Windows and answers their calls to console-only system
  libraries with PC implementations.
- **Assets.** Readers for the game's archives, textures and metadata, so the data can be inspected and used.

No game files, keys or extracted data are part of this repository. Every tool runs on your own console dump.

## Status

GTA VI does not run yet: its main executable is not available to the project, so the work so far is everything
that can be built and verified without it.

| Area | State |
|---|---|
| Shader translation | Every shader stage the game uses is translated (vertex, pixel, compute, mesh, geometry, ray intersection), and all 25,884 programs compile. An optional structured control-flow form makes the output 39% smaller. Instruction behaviour is checked on D3D12 WARP. |
| Archives | Contents can be extracted without the encrypted file table (no names yet). Metadata files parse completely. |
| Textures | Most textures export; pages using the console's BCPack compression cannot be decoded yet. |
| Loader | The game's support libraries load with all imports resolved; the console runtime is implemented for offline use. |
| Graphics API | Needs the executable to see how the game drives the GPU. A first frame works: a game texture drawn with the game's translated unlit pass and its cached pipeline state on our own D3D12 code ([docs/rendering.md](docs/rendering.md)). |

The [wiki](../../wiki) has the details, research notes and the current progress report.

## Getting started

You need the .NET 10 SDK. Shader work also needs LLVM 18 or newer with the AMDGPU target (`llvm-mc`) and the
DirectX shader compiler (`dxc`, part of the Windows SDK).

```
dotnet build FiveStar.slnx
dotnet test
```

### Preparing your dump

```
fivestar setup <your dump folder>
```

One command prepares everything the port needs: it checks the dump, extracts and translates the shaders, reads the
console pipeline cache and exports the textures into `%LOCALAPPDATA%\FiveStar\<titleid>-<version>\` (`--work` picks
another folder). The first run takes one to several hours, depending on the processor, and needs over 100 GB free; a
re-run skips finished steps, and a run that was stopped continues where it left off. `fivestar setup <dump> --status` shows what is done. See
[docs/setup.md](docs/setup.md).

### Single tools

The `fivestar` command holds all tools; `fivestar <group> help` lists a group's commands.

```
fivestar archive carve <archive.rpf> <out-folder>     extract an archive's contents
fivestar texture extract <file.ttpf> <out-folder>     export textures as DDS
fivestar shader extract <shaders.rpf> <out-folder>    extract the GPU programs
fivestar shader translate <programs> <out-folder>     translate them to HLSL and compile
```

The loader is a separate program, `fivestar-loader`; see [docs/loader.md](docs/loader.md).

## Repository layout

| Folder | Contents |
|---|---|
| `src/FiveStar.Formats` | archive, texture, metadata and platform file readers |
| `src/FiveStar.Shaders` | shader containers and the RDNA to HLSL translator |
| `src/FiveStar.Loader` | the module loader and the console runtime replacements |
| `src/FiveStar.Graphics` | D3D12 devices, the root signature of translated programs, pipelines and rendering |
| `src/FiveStar.Cli` | the `fivestar` command |
| `tests/` | tests per project, plus the code-rule checks |
| `docs/` | one page per format and tool |
| `docs/wiki/` | the source of the project wiki: status, roadmap and research notes |

## Contributing

Contributions are welcome. [CONTRIBUTING.md](CONTRIBUTING.md) explains the ground rules, the code style and how
to submit a change.

## License

GNU General Public License v3.0 or later. See [LICENSE](LICENSE).

Five Star Recomp is a fan project. It is not affiliated with or endorsed by Rockstar Games, Take-Two Interactive or
Microsoft. Grand Theft Auto and Grand Theft Auto VI are trademarks of Take-Two Interactive. You need your own copy of
the game; this repository contains none of its content.
