// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using FiveStar.Cli.Exe;
using FiveStar.Cli.Tests.Support;
using Xunit;

namespace FiveStar.Cli.Tests;

/// <summary>
/// <c>fivestar exe analyze</c> on the package's own DLLs (FIVESTAR_DUMP). The GDK thunk counts are the ones lane work
/// already found in <c>libHttpClient.GDK.dll</c>.
/// </summary>
public sealed class ExeAnalyzeTests
{
    private static readonly string[] PackageDlls =
    [
        "amd_fidelityfx_loader_xbox.dll", "amd_fidelityfx_upscaler_xbox.dll", "Bink2Scarlett.dll",
        "libHttpClient.GDK.dll", "XboxBvhBuilder.dll", "XCurl.dll",
    ];

    [GdkDumpFact]
    public void The_http_client_shows_the_known_gdk_thunks_and_each_report_section()
    {
        var analysis = Analyze("libHttpClient.GDK.dll");

        Assert.Equal(408, analysis.GdkThunkCount);
        Assert.Equal(365, analysis.GdkNamedCount);
        Assert.Contains(analysis.Sections, section => section.Console);
        Assert.NotEmpty(analysis.Imports);
        Assert.NotEmpty(analysis.DayOneGaps);
        AssertReportsRender(analysis);

        if (analysis.LoaderClassified)
        {
            Assert.Contains(analysis.GdkCalls, call => call.Shimmed == true);
        }
    }

    [DumpFact]
    public void Every_package_dll_analyzes_without_error()
    {
        foreach (var dll in PackageDlls)
        {
            var analysis = Analyze(dll);

            Assert.NotEmpty(analysis.Sections);
            AssertReportsRender(analysis);
        }
    }

    private static void AssertReportsRender(ExeAnalysis analysis)
    {
        Assert.False(string.IsNullOrWhiteSpace(ReportMarkdown.Build(analysis)));
        Assert.StartsWith("{", ReportJson.Build(analysis).TrimStart());
    }

    private static ExeAnalysis Analyze(string dll)
    {
        var exe = Path.Combine(DumpFactAttribute.Folder!, dll);
        var outDir = Path.Combine(Path.GetTempPath(), "fivestar-exe-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outDir);
        var options = new AnalyzeOptions(exe, DumpFactAttribute.Folder, GdkDumpFactAttribute.Library, outDir, null);

        return ExeAnalyzer.Analyze(options);
    }
}
