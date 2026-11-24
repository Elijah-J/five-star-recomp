// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using FiveStar.Shaders.Dxil;
using FiveStar.Shaders.Translation;

namespace FiveStar.Shaders.Compilation;

/// <summary>
/// Compiles translated HLSL with the DXIL backend (no dxc; docs/dxil-backend.md). A program the backend does not
/// handle (another stage, or HLSL it cannot lower) goes to <c>fallback</c> when there is one, else fails with the
/// backend's reason. The runtime headers come from the source's folder when it has them, else from this assembly.
/// </summary>
public sealed class DxilBackendCompiler : IShaderCompiler
{
    private const string Prefix = "dxil backend: ";

    private readonly IShaderCompiler? _fallback;
    private readonly Lazy<string> _identity;

    public DxilBackendCompiler(DxcOptions options, IShaderCompiler? fallback)
    {
        Options = options;
        _fallback = fallback;
        _identity = new Lazy<string>(ComputeIdentity);
    }

    public DxcOptions Options { get; }

    public string Identity => _identity.Value;

    public CompileOutcome Compile(CompileRequest request, TimeSpan timeout)
    {
        if (!DxilBackend.Supports(request.Profile))
        {
            return Fallback(request, timeout, $"profile {request.Profile} is not supported");
        }

        var folder = Path.GetDirectoryName(Path.GetFullPath(request.HlslPath)) ?? ".";

        try
        {
            var container = DxilBackend.Compile(request.Hlsl, request.Profile, name => Include(folder, name));
            File.WriteAllBytes(request.DxilPath, container);

            return CompileOutcome.Success;
        }
        catch (DxilBackendException exception)
        {
            return Fallback(request, timeout, exception.Message);
        }
    }

    private CompileOutcome Fallback(CompileRequest request, TimeSpan timeout, string reason)
    {
        if (_fallback is null)
        {
            return CompileOutcome.Failure(Prefix + reason);
        }

        return _fallback.Compile(request, timeout);
    }

    private static string? Include(string folder, string name)
    {
        var path = Path.Combine(folder, name);

        if (File.Exists(path))
        {
            return File.ReadAllText(path);
        }

        return RuntimeIncludes.Read(name);
    }

    /// <summary>This build of the backend (its module version id, a hash of the deterministic build) and the fallback's identity.</summary>
    private string ComputeIdentity()
    {
        var assembly = typeof(DxilBackend).Assembly.ManifestModule.ModuleVersionId.ToString();

        return ContentHash.OfText(["dxil-backend", assembly, _fallback?.Identity ?? string.Empty]);
    }
}
