// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using FiveStar.Loader.Calls;
using FiveStar.Loader.Diagnostics;
using FiveStar.Loader.Host;

namespace FiveStar.Loader;

/// <summary>Entry point of fivestar-loader: maps the console's x64 images and runs inspected exports.</summary>
public static class Program
{
    public static int Main(string[] args)
    {
        var options = Parse(args);

        if (options is null)
        {
            return LoaderHost.UsageError;
        }

        if (options.Help)
        {
            Console.WriteLine(HostUsage.Text);

            return LoaderHost.Success;
        }

        if (options.ListSafeCalls)
        {
            ExportCaller.ListKnown(new LoaderLog(Console.Out, null, false));

            return LoaderHost.Success;
        }

        if (!Environment.Is64BitProcess)
        {
            Console.Error.WriteLine("x64 only");

            return LoaderHost.UsageError;
        }

        if (options.ClassifyInput is not null)
        {
            return ClassifyCommand.Run(options);
        }

        if (options.Loads.Count == 0)
        {
            Console.WriteLine(HostUsage.Text);

            return LoaderHost.UsageError;
        }

        return Run(options);
    }

    private static HostOptions? Parse(string[] args)
    {
        try
        {
            return new CommandLineParser(args).Parse();
        }
        catch (CommandLineException e)
        {
            Console.Error.WriteLine(e.Message);
            Console.WriteLine(HostUsage.Text);

            return null;
        }
    }

    private static int Run(HostOptions options)
    {
        using var file = OpenLogFile(options.LogFile);
        var log = new LoaderLog(Console.Out, file, options.Verbose);
        using var runtime = new LoaderRuntime(HostComposition.CreateLoaderOptions(options), HostComposition.CreateGdkOptions(options), HostComposition.CreatePipelineFolders(options), HostComposition.CreateEnvironment(), log);

        return new LoaderHost(options, runtime, log, Environment.ProcessId).Run();
    }

    private static StreamWriter? OpenLogFile(string? path)
    {
        if (path is null)
        {
            return null;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);

        return new StreamWriter(path, false) { AutoFlush = true };
    }
}
