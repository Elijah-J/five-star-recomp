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
          --input-script <file>  timed key presses ("<s> <key> [hold <s>] [repeat <n> every <s>]" per line)
                                 added to the desktop keys, so the keyboard and the virtual gamepad see them
          --profile-threads      with --sample-threads: also log the main image's busiest functions on running threads
          --trace-module <dll>   with --run: the call trace also records calls into this mapped image
          --sample-threads <s>   with --run: every s seconds log the calls per shim since the last sample,
                                 DirectStorage totals and every thread's stack
          --main <name>          image that plays the process exe for GetModuleHandle(NULL)/GetModuleFileName(NULL)
                                 (default: first mapped exe, else the first --load)
          --patch-peb            also point PEB.ImageBaseAddress at the main image (off by default)
          --save-dir <dir>       XGameSave / local storage root (default: %LOCALAPPDATA%\FiveStar\GdkData\<TitleId>)
          --shader-dir <dir>     extracted FSSM programs; with --translated-dir, D3D12.X pipeline creation is enabled
          --translated-dir <dir> translated DXIL programs (fivestar shader translate)
          --gdk-abi <file>       XGameRuntime thunk ABI table: "clsid iid slot FunctionName" per line;
                                 default: read from an installed PC GDK's xgameruntime.lib
          --input <file>         GameInput keyboard-and-mouse gamepad mapping ("setting value" per line)
          --ffx <dll>            PC FidelityFX API DLL (AMD FidelityFX SDK, DX12) the game's FidelityFX calls go to;
                                 without it FidelityFX reports no provider
          --d3d12-debug          D3D12 debug layer and DRED; their messages and the removal report go to the log
                                 (needs the Graphics Tools optional feature)
          --d3d12-gpu-validation --d3d12-debug plus GPU-based validation (slow)
          --d3d12-skip <i:n,...> D3D12.X slots that do nothing (list:51, queue:35, device:n), to bisect GPU faults
          --d3d12-trace-queues   log the first queue submissions, signals and waits (cross-queue protocol)
          --d3d12-trace-draws    log what the first draws bind (pipeline, targets, vertex buffers, tables) and read
                                 their render targets back
          --capture-dir <dir>    write read-back planes and render targets there as PNG
          --capture-presents <n,...> present numbers whose plane is read back (default 60,300,900,1500)
          --present-test         present moving color bars instead of the game's frame (checks the swap chain)
          --raytracing          XboxBvhBuilder.dll's CPU paths: console acceleration-structure sizes and BLAS
                                 rehydration into game memory (off: desktop sizes, nothing written)
          --xdsp-convolution     run the XDsp convolution effects on the CPU (default: silence; response format inferred)
          --package-root <dir>  package root for XPackage / MicrosoftGame.config (default: main image directory)
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
