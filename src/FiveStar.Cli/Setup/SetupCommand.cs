// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using FiveStar.Cli.Shader;

namespace FiveStar.Cli.Setup;

/// <summary><c>fivestar setup &lt;dump&gt;</c>: prepares everything the port needs from the dump, skipping what is already done.</summary>
internal static class SetupCommand
{
    private const int Success = 0;
    private const int Failure = 1;
    private const int UsageError = 2;
    private const string StatusFlag = "status";
    private const string CleanCacheFlag = "clean-cache";
    private const string ForceFlag = "force";

    private static readonly string[] Usage =
    [
        "usage: fivestar setup <dump folder> [options]",
        "  --work <dir>         work folder (default %LOCALAPPDATA%\\FiveStar\\<titleid>-<version>)",
        "  --status             what is done, what is stale, and the disk use",
        "  --clean-cache        remove shader-cache entries of older builds",
        "  --only <a,b>         run only these steps; --force redoes them even when up to date",
        "  --limit N --step K   a sample: the first N programs of every K-th, N textures per page file",
        "  --jobs N             programs translated at once (default: every processor)",
        "  --llvm <bin folder> --dxc <dxc.exe> --gdk-lib <xgameruntime.lib>   tool locations",
    ];

    public static int Run(IReadOnlyList<string> args)
    {
        try
        {
            return Start(new CommandArguments(args, new HashSet<string> { StatusFlag, CleanCacheFlag, ForceFlag }));
        }
        catch (ArgumentException exception)
        {
            Console.Error.WriteLine($"fivestar setup: {exception.Message}");

            return UsageError;
        }
    }

    /// <summary>The steps in dependency order.</summary>
    public static IReadOnlyList<ISetupStep> Steps()
    {
        return [new VerifyStep(), new ShaderExtractStep(), new ShaderTranslateStep(), new PipelineCacheStep(), new TextureStep(), new GdkAbiStep()];
    }

    private static int Start(CommandArguments arguments)
    {
        if (arguments.Positional.Count != 1 || arguments.Positional[0] == "help")
        {
            Array.ForEach(Usage, Console.Error.WriteLine);

            return UsageError;
        }

        DumpIdentity identity;
        var dump = Path.GetFullPath(arguments.Positional[0]);

        try
        {
            identity = DumpIdentity.Read(dump);
        }
        catch (InvalidDataException exception)
        {
            Console.Error.WriteLine($"fivestar setup: {exception.Message}");

            return Failure;
        }

        var context = Context(dump, identity, arguments);
        var runner = new SetupRunner(Steps(), new StateStore(context.StateFolder), Console.Out, TimeProvider.System, () => FreeBytes(context.Work));
        Console.WriteLine($"Five Star setup {Fingerprints.CommandVersion()}: {identity.Name} {identity.TitleId} version {identity.Version}");
        Console.WriteLine($"dump {dump}, work folder {context.Work}");

        return Dispatch(runner, context, arguments);
    }

    private static int Dispatch(SetupRunner runner, SetupContext context, CommandArguments arguments)
    {
        if (arguments.Flag(StatusFlag))
        {
            SetupStatus.Print(runner, context, FreeBytes(context.Work), Console.Out);

            return Success;
        }

        if (arguments.Flag(CleanCacheFlag))
        {
            return Code(CacheCleanup.Run(runner, context, Console.Out));
        }

        var only = Only(arguments, runner.Steps);

        return Code(runner.Run(context, only, arguments.Flag(ForceFlag)));
    }

    private static SetupContext Context(string dump, DumpIdentity identity, CommandArguments arguments)
    {
        var work = arguments.Option("work") ?? identity.DefaultWork(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
        var every = arguments.IntegerOption("step") ?? 1;
        var jobs = arguments.IntegerOption("jobs") ?? Environment.ProcessorCount;

        if (every < 1 || jobs < 1)
        {
            throw new ArgumentException("--step and --jobs take a number from 1");
        }

        return new SetupContext(dump, Path.GetFullPath(work), arguments.IntegerOption("limit"), every, jobs, arguments);
    }

    private static HashSet<string>? Only(CommandArguments arguments, IReadOnlyList<ISetupStep> steps)
    {
        var list = arguments.Option("only");

        if (list is null)
        {
            return null;
        }

        var names = list.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet(StringComparer.Ordinal);
        var unknown = names.Where(name => steps.All(step => step.Name != name)).ToList();

        if (unknown.Count > 0)
        {
            throw new ArgumentException($"unknown step {string.Join(", ", unknown)}; steps: {string.Join(", ", steps.Select(step => step.Name))}");
        }

        return names;
    }

    private static long FreeBytes(string work)
    {
        var root = Path.GetPathRoot(Path.GetFullPath(work));

        if (string.IsNullOrEmpty(root))
        {
            return long.MaxValue;
        }

        return new DriveInfo(root).AvailableFreeSpace;
    }

    private static int Code(bool succeeded)
    {
        if (succeeded)
        {
            return Success;
        }

        return Failure;
    }
}
