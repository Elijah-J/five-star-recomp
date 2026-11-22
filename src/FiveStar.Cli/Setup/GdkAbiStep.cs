// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using FiveStar.Cli.Platform;
using FiveStar.Formats.Platform;

namespace FiveStar.Cli.Setup;

/// <summary>
/// Builds the loader's thunk ABI table (<c>&lt;work&gt;/loader/gdk-abi.txt</c>, for <c>fivestar-loader --gdk-abi</c>) from
/// the PC GDK's xgameruntime.lib, checked against the dump's modules (runs <c>fivestar platform gdk-abi</c>). Without a
/// GDK the step finishes with a note; installing one later redoes it.
/// </summary>
internal sealed class GdkAbiStep : ISetupStep
{
    public const string StepName = "gdk-abi";

    private const string LibraryName = "xgameruntime.lib";
    private const string GdkFolder = "Microsoft GDK";
    private const string TableName = "gdk-abi.txt";
    private const string ModulePattern = "*.dll";
    private const string NoLibrary = "no-gdk";

    // The 64-bit library sits in an amd64 or x64 folder of the GDK.
    private static readonly string[] SixtyFourBitFolders = ["amd64", "x64"];
    private static readonly TimeSpan TypicalTime = TimeSpan.FromSeconds(5);

    public string Name => StepName;

    public string Title => "Build the loader's GDK table";

    public IReadOnlyList<string> DependsOn => [VerifyStep.StepName];

    public TimeSpan Estimate(SetupContext context) => TypicalTime;

    public StepFingerprint Fingerprint(SetupContext context)
    {
        var library = Library(context);
        var inputs = NoLibrary;

        if (library is not null)
        {
            inputs = Fingerprints.OfFiles(Path.GetDirectoryName(library)!, [library, .. Modules(context)]);
        }

        return new StepFingerprint(inputs, Fingerprints.OfAssemblies(typeof(GdkThunkLibrary)));
    }

    public bool HasOutputs(SetupContext context) => Library(context) is null || File.Exists(Path.Combine(context.LoaderFolder, TableName));

    public long RequiredBytes(SetupContext context) => 0;

    public string? MissingRequirement(SetupContext context) => null;

    public StepResult Run(SetupContext context)
    {
        var library = Library(context);

        if (library is null)
        {
            return StepResult.Done("skipped: no PC GDK found; install the Microsoft GDK or pass --gdk-lib <xgameruntime.lib>");
        }

        Directory.CreateDirectory(context.LoaderFolder);
        var table = Path.Combine(context.LoaderFolder, TableName);
        GdkAbiCommand.Run(new Archive.CommandArguments([library, .. Modules(context), "--out", table], []));

        return StepResult.Done($"{table} from {library}");
    }

    private static IReadOnlyList<string> Modules(SetupContext context)
    {
        var modules = Directory.GetFiles(context.Dump, ModulePattern);
        Array.Sort(modules, StringComparer.Ordinal);

        return modules;
    }

    /// <summary>The library from <c>--gdk-lib</c>, else the newest 64-bit one of an installed GDK, or null.</summary>
    private static string? Library(SetupContext context)
    {
        var given = context.Arguments.Option("gdk-lib");

        if (given is not null)
        {
            return given;
        }

        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), GdkFolder);

        if (!Directory.Exists(root))
        {
            return null;
        }

        return Directory.EnumerateFiles(root, LibraryName, SearchOption.AllDirectories)
            .Where(path => SixtyFourBitFolders.Contains(Path.GetFileName(Path.GetDirectoryName(path)), StringComparer.OrdinalIgnoreCase))
            .Order(StringComparer.Ordinal)
            .LastOrDefault();
    }
}
