// Copyright (c) 2026 Five Star Recomp contributors.
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using FiveStar.Loader.Codegen;
using FiveStar.Loader.Loading;

namespace FiveStar.Loader.Shims;

/// <summary>
/// Every shim of this assembly, found by reflection: [Shim] methods, [ShimModule] (Xbox-only) modules with their
/// API-set aliases, and [NotImplementedTable] catalogs. Looked up by (normalized module, symbol), then through an
/// alias, then under "*".
/// </summary>
internal sealed class ShimTable
{
    private const BindingFlags StaticMembers = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    private readonly Dictionary<(string Module, string Symbol), ShimEntry> _entries = [];
    private readonly HashSet<string> _exclusiveModules = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ShimModuleAttribute> _moduleInfo = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<(string Module, string Symbol), long> _notImplemented = [];
    private readonly List<ShimAlias> _aliases = [];
    private readonly LastErrorBridge _lastError;

    public ShimTable(LastErrorBridge lastError, CodeHeap heap)
    {
        _lastError = lastError;

        foreach (var type in typeof(ShimTable).Assembly.GetTypes())
        {
            RegisterModules(type);
            RegisterNotImplemented(type);
            RegisterMethods(type);
        }

        // The last-error wrappers become executable.
        heap.Seal();
    }

    public IReadOnlyCollection<ShimEntry> All => _entries.Values;

    /// <summary>Known but unimplemented functions: (module, symbol) to the value their stub returns.</summary>
    public IReadOnlyDictionary<(string Module, string Symbol), long> NotImplementedFunctions => _notImplemented;

    public bool IsExclusiveModule(string module)
    {
        return _exclusiveModules.Contains(ModuleNames.NormalizeImport(module));
    }

    /// <summary>The module that answers for <paramref name="symbol"/> of <paramref name="module"/>: an alias target, or the module itself.</summary>
    public string EffectiveModule(string module, string? symbol)
    {
        string key = ModuleNames.NormalizeImport(module);

        if (symbol is null)
        {
            return key;
        }

        foreach (var alias in _aliases)
        {
            if (alias.Pattern.IsMatch(key) && symbol.StartsWith(alias.Prefix, StringComparison.Ordinal))
            {
                return alias.Target;
            }
        }

        return key;
    }

    public ShimEntry? TryResolve(string module, string? name, uint ordinal)
    {
        string symbol = ModuleNames.SymbolKey(name, ordinal);
        string key = ModuleNames.NormalizeImport(module);

        if (_entries.TryGetValue((key, symbol), out var direct))
        {
            return direct;
        }

        string effective = EffectiveModule(module, name);

        if (effective != key && _entries.TryGetValue((effective, symbol), out var aliased))
        {
            return aliased;
        }

        return _entries.GetValueOrDefault((ModuleNames.AnyModule, symbol));
    }

    /// <summary>
    /// The second chance after <see cref="TryResolve"/>: a known unimplemented function, or any other symbol of an
    /// Xbox-only module (which returns the module's unresolved value). Null for everything else.
    /// </summary>
    public ShimFallback? TryFallback(string module, string? name, uint ordinal)
    {
        string effective = EffectiveModule(module, name);

        if (_notImplemented.TryGetValue((effective, ModuleNames.SymbolKey(name, ordinal)), out long value))
        {
            return new ShimFallback(value, true);
        }

        if (!_exclusiveModules.Contains(effective))
        {
            return null;
        }

        long unresolved = 0;

        if (_moduleInfo.TryGetValue(effective, out var info))
        {
            unresolved = info.UnresolvedReturn;
        }

        return new ShimFallback(unresolved, false);
    }

    /// <summary>Removes the shim "module!symbol", to exercise the unresolved-import path.</summary>
    public bool Disable(string specification)
    {
        int bang = specification.IndexOf('!', StringComparison.Ordinal);

        if (bang < 0)
        {
            return false;
        }

        return _entries.Remove((ModuleNames.NormalizeImport(specification[..bang]), specification[(bang + 1)..]));
    }

    private void RegisterModules(Type type)
    {
        foreach (var module in type.GetCustomAttributes<ShimModuleAttribute>())
        {
            string key = ModuleNames.NormalizeImport(module.Module);
            _exclusiveModules.Add(key);
            _moduleInfo[key] = module;

            if (module.AliasPattern is not null)
            {
                _aliases.Add(new ShimAlias(new Regex(module.AliasPattern, RegexOptions.IgnoreCase), module.AliasSymbolPrefix, key));
            }
        }
    }

    private void RegisterNotImplemented(Type type)
    {
        foreach (var field in type.GetFields(StaticMembers))
        {
            var table = field.GetCustomAttribute<NotImplementedTableAttribute>();

            if (table is null)
            {
                continue;
            }

            foreach (var (name, value) in ((string Name, long ReturnValue)[])field.GetValue(null)!)
            {
                _notImplemented[(ModuleNames.NormalizeImport(table.Module), name)] = value;
            }
        }
    }

    private void RegisterMethods(Type type)
    {
        foreach (var method in type.GetMethods(StaticMembers))
        {
            var shims = method.GetCustomAttributes<ShimAttribute>().ToArray();

            if (shims.Length == 0)
            {
                continue;
            }

            if (method.GetCustomAttribute<UnmanagedCallersOnlyAttribute>() is null)
            {
                throw new InvalidOperationException($"[Shim] method {type.Name}.{method.Name} must be [UnmanagedCallersOnly]");
            }

            // For an [UnmanagedCallersOnly] method this is its native-callable entry point.
            nint address = method.MethodHandle.GetFunctionPointer();

            if (shims.Any(s => s.SetsLastError))
            {
                address = _lastError.Wrap(address);
            }

            foreach (var shim in shims)
            {
                var key = (ModuleNames.NormalizeImport(shim.Module), shim.Name);
                _entries[key] = new ShimEntry(key.Item1, key.Item2, address, method);
            }
        }
    }
}
