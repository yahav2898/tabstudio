using System.Text.Json.Serialization;

namespace TabStudio.Packages.Caching;

/// <summary>The contents of <c>obj/tabstudio.restore.json</c>.</summary>
internal sealed class RestoreCacheFile
{
    /// <summary>The layout of this file; a file with another version is ignored.</summary>
    public int FormatVersion { get; set; }

    /// <summary>The hash of everything the restore depended on. See <see cref="RestoreCache.ComputeInputsHash"/>.</summary>
    public string InputsHash { get; set; } = string.Empty;

    /// <summary>The restored packages.</summary>
    public List<CachedPackage> Packages { get; set; } = [];

    /// <summary>The assemblies to compile against.</summary>
    public List<string> CompileAssemblies { get; set; } = [];

    /// <summary>The assemblies to load at run time.</summary>
    public List<string> RuntimeAssemblies { get; set; } = [];

    /// <summary>The native libraries to load at run time.</summary>
    public List<string> NativeLibraries { get; set; } = [];

    /// <summary>The analyzers and source generators.</summary>
    public List<string> Analyzers { get; set; } = [];

    /// <summary>The warnings the restore reported, repeated whenever the cache is used.</summary>
    public List<CachedDiagnostic> Diagnostics { get; set; } = [];
}

/// <summary>One package in <see cref="RestoreCacheFile"/>.</summary>
internal sealed class CachedPackage
{
    /// <summary>See <see cref="RestoredPackage.Id"/>.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>See <see cref="RestoredPackage.Version"/>.</summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>See <see cref="RestoredPackage.DirectoryPath"/>.</summary>
    public string DirectoryPath { get; set; } = string.Empty;

    /// <summary>See <see cref="RestoredPackage.IsDirect"/>.</summary>
    public bool IsDirect { get; set; }

    /// <summary>See <see cref="RestoredPackage.CompileAssemblies"/>.</summary>
    public List<string> CompileAssemblies { get; set; } = [];

    /// <summary>See <see cref="RestoredPackage.RuntimeAssemblies"/>.</summary>
    public List<string> RuntimeAssemblies { get; set; } = [];

    /// <summary>See <see cref="RestoredPackage.Analyzers"/>.</summary>
    public List<string> Analyzers { get; set; } = [];

    /// <summary>See <see cref="RestoredPackage.NativeLibraries"/>.</summary>
    public List<string> NativeLibraries { get; set; } = [];

    /// <summary>See <see cref="RestoredPackage.ProvidedByFramework"/>.</summary>
    public bool ProvidedByFramework { get; set; }

    /// <summary>See <see cref="RestoredPackage.FlowsAnalyzers"/>.</summary>
    public bool FlowsAnalyzers { get; set; } = true;

    /// <summary>See <see cref="RestoredPackage.BuildFiles"/>.</summary>
    public List<string> BuildFiles { get; set; } = [];

    /// <summary>See <see cref="RestoredPackage.ContentSources"/>.</summary>
    public List<string> ContentSources { get; set; } = [];

    /// <summary>See <see cref="RestoredPackage.Depth"/>.</summary>
    public int Depth { get; set; } = 1;
}

/// <summary>One diagnostic in <see cref="RestoreCacheFile"/>.</summary>
internal sealed class CachedDiagnostic
{
    /// <summary>The diagnostic id.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>The severity, by name.</summary>
    public string Severity { get; set; } = string.Empty;

    /// <summary>The message.</summary>
    public string Message { get; set; } = string.Empty;
}

/// <summary>Source-generated JSON for the restore cache, which works in trimmed apps without reflection.</summary>
[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(RestoreCacheFile))]
internal sealed partial class RestoreCacheJsonContext : JsonSerializerContext
{
}
