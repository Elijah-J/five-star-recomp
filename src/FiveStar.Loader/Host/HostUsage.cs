// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

namespace FiveStar.Loader.Host;

/// <summary>The usage text of fivestar-loader.</summary>
internal static class HostUsage
{
    public const string Text = """
        fivestar-loader - manual PE loader and Xbox API shims

          fivestar-loader --load <dll> [--load <dll> ...] [--call <export>|all] [options]

        options:
          --call <name>          call an inspected export (see --list-safe-calls); 'all' = every applicable one
          --run                  run the main executable's entry point; reports faults in mapped code with the
                                 last calls through shims and stubs
          --run-timeout <s>      with --run: after s seconds report the main thread's stack and exit (code 3)
          --trace-system         with --run: the call trace also records System32 imports
          --main <name>          image that plays the process exe for GetModuleHandle(NULL)/GetModuleFileName(NULL)
                                 (default: first mapped exe, else the first --load)
          --patch-peb            also point PEB.ImageBaseAddress at the main image (off by default)
          --save-dir <dir>       XGameSave / local storage root (default: %LOCALAPPDATA%\FiveStar\GdkData\<TitleId>)
          --shader-dir <dir>     extracted FSSM programs; with --translated-dir, D3D12.X pipeline creation is enabled
          --translated-dir <dir> translated DXIL programs (fivestar shader translate)
          --gdk-abi <file>       XGameRuntime thunk ABI table: "clsid iid slot FunctionName" per line
          --input <file>         GameInput keyboard-and-mouse gamepad mapping ("setting value" per line)
          --package-root <dir>   package root for XPackage / MicrosoftGame.config (default: main image directory)
          --device-type <n>      XSystemGetDeviceType result (default 7 = XboxScarlettAnaconda)
          --patches <path>       code patch file, or folder of *.fspatch files (repeatable, applied in order)
          --mods <dir>           mod folder laid over the package root (repeatable, first has priority)
          --manual <a.dll,b.dll> map these modules with this loader instead of LoadLibrary (repeatable)
          --system-crt           do not map the shipped MSVC runtime (default: msvcp140*/vcruntime140*/concrt140/
                                 vccorlib140/vcomp140 are mapped from the target's directory if present)
          --search <dir>         extra directory for --manual modules and for modules the PC does not have
          --unresolved <mode>    stub (log + return 0, default) | trap (log + terminate on call) | error (fail load)
          --no-entry             do not call DllMain
          --no-shim <mod!func>   disable one shim (exercise the unresolved-import path)
          --no-exports           do not list the target's exports
          --unload               call DllMain(PROCESS_DETACH) and free images at the end
          --work-dir <dir>       where generated TLS host DLLs go (default: <exe dir>\work)
          --log <file>           full log (every import and its resolution)
          -v                     verbose console (every import, every shim call)
          --list-safe-calls      list the export call recipes
          --classify <file>      classify "module!symbol" lines (shim, Xbox stub, package, host or missing; the
                                 --search directories are the package folder; for fivestar exe analyze)
          --classify-out <file>  where the classification goes (default: standard output)
        """;
}
