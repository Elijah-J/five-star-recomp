# Third-party notices

Five Star Recomp is licensed under the GNU General Public License, version 3 or later (`LICENSE`). Parts of it are
based on the projects below, used under their MIT licenses.

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
