// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using FiveStar.Loader.Host;
using FiveStar.Loader.Tests.Support;
using Xunit;

namespace FiveStar.Loader.Tests;

/// <summary>The classify mode behind <c>fivestar exe analyze</c>: the same shim table verdicts, without mapping an image.</summary>
public sealed class ImportClassifyTests
{
    [Fact]
    public void Imports_classify_as_shim_xbox_stub_or_host()
    {
        using var test = TestRuntime.Create();
        var classifier = new ImportClassifier(test.Runtime.Shims);

        var shim = classifier.Classify("kernel32.dll", "GetProcAddress", 0);
        var forward = classifier.Classify("d3d12_xs.dll", "D3D12SerializeRootSignature", 0);
        var xboxOnly = classifier.Classify("d3d12_xs.dll", "SomeConsoleOnlyEntry", 0);
        var host = classifier.Classify("kernel32.dll", "NoSuchFunctionXyz", 0);

        Assert.Equal(ImportClassKind.Shim, shim.Kind);
        Assert.Equal(ImportClassKind.Shim, forward.Kind);
        Assert.Equal(ImportClassKind.XboxStub, xboxOnly.Kind);
        Assert.Equal(ImportClassKind.Host, host.Kind);
    }
}
