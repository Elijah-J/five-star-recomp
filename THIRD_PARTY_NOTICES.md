# Third-party notices

Five Star Recomp is licensed under the GNU General Public License, version 3 or later (`LICENSE`). Parts of it are
based on the projects below, used under the licenses quoted with them.

## Leonida

https://github.com/ItsNotRexed/Leonida, GPL-3.0. Its notes on the console D3D12.X list (slots 146 and 148, the
device byte at +0x18), the create-device and present parameter names it collected from public sources, the
WaitFrameEventX return codes and the BVH header and instance-node fields were leads; each was checked against the
game's executable or shaders before use (docs/d3d12x-shim.md, docs/raytracing.md), and the instance node's 0x00 and
0x70 fields are taken from it unverified. Its note that an Intel host needs the four Zen 2 instruction families
emulated was the lead for docs/cpu.md; the census and the emulation are our own. No code was copied.

## GTA6-Asset-Info

https://github.com/TrinitronClient/GTA6-Asset-Info, revision `0f649eba78cdd6e053d7aefd333fb6e3abe9739a`, GPL-3.0.
Its notes were leads, each checked on the dump before use; no text or code was copied.

- Textures (docs/textures.md): the resource-kind and array-size fields of the descriptor, the copies of a cube map
  and their padding, arrays stored level by level, no mip tail on a single-mip texture, the 3D brick order and the y
  bit order of the 1-byte 64 KB block. The stored mip count we use (mip field plus `d1` bits 0-2) is our own finding.
- Models (docs/models.md): the meshlet start wrap, the skin, tangent and extra texture-coordinate streams, the inverse
  bind matrices and the ped bone palette. Its order of the packed position low bytes was checked and not adopted.

## bcpack-rs

https://github.com/Ahmet-Dedeler/bcpack-rs, revision `0a887de1edc9c22a425605091f1a8365fdf74dbb`, licensed MIT OR
Apache-2.0 and used here under MIT.

- The BCPack BC1 decoder in `src/FiveStar.Formats/Ttpf/BcPack` is a C# implementation of its decoder (`src/rans.rs`,
  `src/bc1.rs`): the clocked lane model, the feeder and the BC1 block assembly.
- `src/FiveStar.Formats/Ttpf/BcPack/Tables/bc1_window.json` and `bc1_quad_table.json` are its
  `data/bc1_window.json` and `data/bc1_quad_table.json` with our corrections; each changed entry keeps the upstream
  value.
- `vendor/bcpack-rs` holds its synthetic test streams unchanged (`vendor/bcpack-rs/PROVENANCE.md`).

```
MIT License

Copyright (c) 2026 Ahmet Dedeler

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

## Microsoft silicon-rans

https://github.com/microsoft/silicon-rans, commit `2f6827d8a4e6a78029c372f20d8b61f7cd408fc5`.

- `src/FiveStar.Formats/Ttpf/BcPack/Tables/mdu_models.json` holds the rANS model tables (`base`, `freq`, `rf`, `rs`,
  `rn`) of its `mdu_minirans.v`, as tabulated by bcpack-rs.
- Each lane of the BC1 decoder follows that file's `decoder` module.

```
MIT License

Copyright (c) Microsoft Corporation.

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE
```

## Concentus

https://github.com/lostromb/concentus, NuGet package `Concentus` 2.2.2, a C# port of the Opus reference library,
licensed under the BSD 3-Clause license below.

- The loader's `xapu.dll` queue (`src/FiveStar.Loader/Shims/Apu`) decodes the game's Opus packets with it; it is a
  package dependency, not copied code.

```
Copyright (c) by various holding parties, including (but not limited to):
Skype Limited, Xiph.Org Foundation, CSIRO, Microsoft Corporation,
Jean-Marc Valin, Gregory Maxwell, Mark Borgerding, Timothy B. Terriberry,
Logan Stromberg. All rights are reserved by their respective holders.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

* Redistributions of source code must retain the above copyright notice, this
  list of conditions and the following disclaimer.

* Redistributions in binary form must reproduce the above copyright notice,
  this list of conditions and the following disclaimer in the documentation
  and/or other materials provided with the distribution.

* Neither the name of Internet Society, IETF or IETF Trust, nor the
   names of specific contributors, may be used to endorse or promote
   products derived from this software without specific prior written
   permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE
FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR
SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER
CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY,
OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.


This repository and its redistributable packages contain independently compiled
versions of the Opus C reference library, which is maintained by Xiph.org and the
Opus open-source contributors. The source code for these libraries is freely available
at https://gitlab.xiph.org/xiph/opus/-/tags/v1.5.2, and all binaries are being
redistributed to you under the same terms of the general Opus license dictated above.
```

## FFmpeg

https://github.com/FFmpeg/FFmpeg, file `libavcodec/wmaprodata.h` as of commit
`232e88928eee71afa55233d021eeda25eb3ed5b4` (its last change), licensed under the GNU Lesser General Public License,
version 2.1 or later. Section 3 of that license lets the tables be used under the GNU General Public License, which
this project does (`LICENSE`).

- `src/FiveStar.Formats/Audio/Xma/WmaProTables.cs` holds its WMA Pro tables unchanged as data: `critical_freq`,
  `scale_table`, `scale_rl_table`, `scale_rl_run`, `scale_rl_level`, `coef0_lens`, `coef0_syms`, `coef0_run`,
  `coef0_level`, `coef1_table`, `coef1_run`, `coef1_level`, `vec4_lens`, `vec4_syms`, `vec2_table` and `vec1_table`.
  The XMA2 decoder around them is our own code.

```
WMA 9/3/PRO compatible decoder
Copyright (c) 2007 Baptiste Coudurier, Benjamin Larsson, Ulion
Copyright (c) 2008 - 2009 Sascha Sommer

This file is part of FFmpeg.

FFmpeg is free software; you can redistribute it and/or
modify it under the terms of the GNU Lesser General Public
License as published by the Free Software Foundation; either
version 2.1 of the License, or (at your option) any later version.

FFmpeg is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
Lesser General Public License for more details.
```
