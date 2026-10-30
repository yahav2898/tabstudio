using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TabStudio.Core.Diagnostics;
using TabStudio.Packages.Installation;
using TabStudio.Packages.Manifests;

namespace TabStudio.Packages.Caching;

/// <summary>
/// Remembers the outcome of a restore in the project's <c>obj/tabstudio.restore.json</c>, so the next restore with
/// the same inputs is instant and needs no network.
/// </summary>
/// <remarks>
/// The inputs are everything that can change the outcome: the package references and their versions (including
/// those of referenced projects), the target
/// framework, its fallbacks and the runtime identifier, the sources, the global packages folder and the framework's assemblies. The cache
/// is used only when their hash matches and every package folder and assembly it lists is still on disk. Only
/// successful restores are cached. Writing the cache is best effort: a read-only project folder only costs speed.
/// </remarks>
internal static class RestoreCache
{
    /// <summary>The current layout of the file.</summary>
    public const int FormatVersion = 6;

    /// <summary>Hashes the inputs of a restore.</summary>
    /// <param name="graph">The project's references and those of the projects it references.</param>
    /// <param name="options">The restore options.</param>
    /// <returns>A hexadecimal SHA-256 hash.</returns>
    public static string ComputeInputsHash(ProjectDependencyGraph graph, PackageRestoreOptions options)
    {
        var text = new StringBuilder();
        void Line(string key, string value) => text.Append(key).Append('=').Append(value).Append('\n');
        void Dependencies(IEnumerable<PackageDependency> dependencies)
        {
            foreach (var dependency in dependencies.OrderBy(d => d.Id, StringComparer.OrdinalIgnoreCase))
            {
                var kind = dependency.IsProject ? "project" : "package";
                Line("dependency", $"{kind}|{NuGetNames.Lower(dependency.Id)}|{dependency.Range.ToNormalizedString()}|{(int)dependency.Excluded}");
            }
        }

        Line("format", FormatVersion.ToString(CultureInfo.InvariantCulture));
        Line("framework", options.TargetFramework);
        Line("compiler", options.CompilerVersion?.ToString() ?? "none");
        Line("runtime", options.RuntimeIdentifier ?? "none");
        Line("fallback", string.Join(';', options.AssetTargetFallback));
        Line("packages", Path.GetFullPath(options.GlobalPackagesFolder));
        foreach (var source in options.Sources)
        {
            Line("source", $"{source.Name}|{source.Location}");
        }

        foreach (var name in options.FrameworkAssemblyNames.Select(NuGetNames.Lower).Order(StringComparer.Ordinal))
        {
            Line("frameworkAssembly", name);
        }

        Dependencies(graph.Dependencies);
        foreach (var pin in graph.Pins ?? [])
        {
            Line("pin", $"{NuGetNames.Lower(pin.Id)}|{pin.Range.ToNormalizedString()}");
        }

        foreach (var (name, project) in graph.Projects.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase))
        {
            Line("referencedProject", NuGetNames.Lower(name));
            Dependencies(project.Dependencies);
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())));
    }

    /// <summary>Reads the cached result, when it is still valid.</summary>
    /// <param name="path">The cache file.</param>
    /// <param name="inputsHash">The hash of the current inputs.</param>
    /// <param name="projectName">The project, for the result and its diagnostics.</param>
    /// <returns>The cached result, or <see langword="null"/> when there is none or it is out of date.</returns>
    public static RestoreResult? TryRead(string path, string inputsHash, string projectName)
    {
        RestoreCacheFile? file;
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            using var stream = File.OpenRead(path);
            file = JsonSerializer.Deserialize(stream, RestoreCacheJsonContext.Default.RestoreCacheFile);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }

        if (file is null || file.FormatVersion != FormatVersion || !string.Equals(file.InputsHash, inputsHash, StringComparison.Ordinal))
        {
            return null;
        }

        var stillInstalled = file.Packages.All(p => File.Exists(Path.Combine(p.DirectoryPath, GlobalPackagesFolder.CompletionMarkerName))) &&
            file.CompileAssemblies.Concat(file.RuntimeAssemblies).Concat(file.NativeLibraries).Concat(file.Analyzers)
                .Concat(file.Packages.SelectMany(p => p.BuildFiles)).Concat(file.Packages.SelectMany(p => p.ContentSources)).All(File.Exists);
        if (!stillInstalled)
        {
            return null;
        }

        return new RestoreResult
        {
            ProjectName = projectName,
            Packages = file.Packages
                .Select(p => new RestoredPackage(p.Id, p.Version, p.DirectoryPath, p.IsDirect, p.CompileAssemblies, p.RuntimeAssemblies, p.ProvidedByFramework) { Analyzers = p.Analyzers, FlowsAnalyzers = p.FlowsAnalyzers, NativeLibraries = p.NativeLibraries, BuildFiles = p.BuildFiles, ContentSources = p.ContentSources, Depth = p.Depth })
                .ToList(),
            CompileAssemblies = file.CompileAssemblies,
            RuntimeAssemblies = file.RuntimeAssemblies,
            NativeLibraries = file.NativeLibraries,
            Analyzers = file.Analyzers,
            Diagnostics = file.Diagnostics
                .Select(d => new BuildDiagnostic(
                    d.Id,
                    Enum.TryParse<DiagnosticSeverityLevel>(d.Severity, out var severity) ? severity : DiagnosticSeverityLevel.Warning,
                    d.Message,
                    null,
                    projectName))
                .ToList(),
            FromCache = true,
        };
    }

    /// <summary>Writes a successful result to the cache. Failures to write are ignored.</summary>
    /// <param name="path">The cache file.</param>
    /// <param name="inputsHash">The hash of the inputs that produced the result.</param>
    /// <param name="result">The result.</param>
    public static void TryWrite(string path, string inputsHash, RestoreResult result)
    {
        var file = new RestoreCacheFile
        {
            FormatVersion = FormatVersion,
            InputsHash = inputsHash,
            Packages = result.Packages.Select(p => new CachedPackage
            {
                Id = p.Id,
                Version = p.Version,
                DirectoryPath = p.DirectoryPath,
                IsDirect = p.IsDirect,
                CompileAssemblies = [.. p.CompileAssemblies],
                RuntimeAssemblies = [.. p.RuntimeAssemblies],
                Analyzers = [.. p.Analyzers],
                NativeLibraries = [.. p.NativeLibraries],
                FlowsAnalyzers = p.FlowsAnalyzers,
                ProvidedByFramework = p.ProvidedByFramework,
                BuildFiles = [.. p.BuildFiles],
                ContentSources = [.. p.ContentSources],
                Depth = p.Depth,
            }).ToList(),
            CompileAssemblies = [.. result.CompileAssemblies],
            RuntimeAssemblies = [.. result.RuntimeAssemblies],
            NativeLibraries = [.. result.NativeLibraries],
            Analyzers = [.. result.Analyzers],
            Diagnostics = result.Diagnostics.Select(d => new CachedDiagnostic
            {
                Id = d.Id,
                Severity = d.Severity.ToString(),
                Message = d.Message,
            }).ToList(),
        };

        var temporary = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
            using (var stream = File.Create(temporary))
            {
                JsonSerializer.Serialize(stream, file, RestoreCacheJsonContext.Default.RestoreCacheFile);
            }

            File.Move(temporary, path, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The cache only saves time; restoring again next time is always correct.
            try
            {
                File.Delete(temporary);
            }
            catch (Exception cleanup) when (cleanup is IOException or UnauthorizedAccessException)
            {
                // Nothing more to do.
            }
        }
    }
}
