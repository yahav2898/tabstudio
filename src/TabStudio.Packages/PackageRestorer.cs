using System.Collections.Concurrent;
using System.Diagnostics;
using NuGet.Frameworks;
using TabStudio.Core.Diagnostics;
using TabStudio.Core.Workspaces;
using TabStudio.Packages.Assets;
using TabStudio.Packages.Caching;
using TabStudio.Packages.Feeds;
using TabStudio.Packages.Installation;
using TabStudio.Packages.Resolution;

namespace TabStudio.Packages;

/// <summary>
/// Restores the NuGet packages of a project on the device: resolves its package references to a dependency graph,
/// downloads what is missing into the global packages folder and selects the assemblies to compile and run with.
/// </summary>
/// <remarks>
/// <para>
/// Everything happens in-process, without MSBuild or the .NET SDK, so it works on Android. Resolution follows
/// NuGet's rules (lowest applicable version, direct and nearest wins, cousin dependencies) and reports problems
/// with NuGet's diagnostic ids. Packages come from NuGet V3 feeds over HTTP and from local folders.
/// </para>
/// <para>
/// A successful restore is remembered in the project's <c>obj/tabstudio.restore.json</c>. While the inputs stay
/// the same, the next restore reads that file and returns at once, without touching the network. Packages that
/// are already in the global packages folder are never downloaded again, and when the sources cannot be reached
/// the installed versions are used, so a project restored once keeps working offline.
/// </para>
/// <para>An instance is immutable and can restore several projects at once.</para>
/// </remarks>
public sealed partial class PackageRestorer
{
    /// <summary>The name of the restore cache file in a project's <c>obj</c> folder.</summary>
    public const string CacheFileName = "tabstudio.restore.json";

    private readonly HttpClient _http;
    private readonly NuGetFramework _framework;
    private readonly List<NuGetFramework> _fallback;
    private readonly FrameworkPackages _frameworkPackages;
    private readonly GlobalPackagesFolder _packages;
    private readonly SharedFrameworks _sharedFrameworks;

    /// <summary>Creates a restorer.</summary>
    /// <param name="options">Where packages come from and go to, and what they are restored for.</param>
    /// <param name="http">The client for web sources. The caller owns it and decides its timeout and proxy.</param>
    /// <exception cref="ArgumentException">The options name no packages folder or an unknown target framework.</exception>
    public PackageRestorer(PackageRestoreOptions options, HttpClient http)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(http);
        if (string.IsNullOrWhiteSpace(options.GlobalPackagesFolder))
        {
            throw new ArgumentException("The options must name a global packages folder.", nameof(options));
        }

        var framework = NuGetFramework.Parse(options.TargetFramework);
        if (framework.IsUnsupported || framework.IsAny)
        {
            throw new ArgumentException($"'{options.TargetFramework}' is not a target framework.", nameof(options));
        }

        Options = options;
        _http = http;
        _framework = framework;
        _fallback = options.AssetTargetFallback
            .Select(name => NuGetFramework.Parse(name))
            .Where(f => !f.IsUnsupported)
            .ToList();
        _frameworkPackages = new FrameworkPackages(options.FrameworkAssemblyNames, framework);
        _packages = new GlobalPackagesFolder(options.GlobalPackagesFolder);
        _sharedFrameworks = new SharedFrameworks(_packages, CreateFeeds, options.TargetFramework, options.CompilerVersion);
    }

    /// <summary>The options the restorer was created with.</summary>
    public PackageRestoreOptions Options { get; }

    /// <summary>The restore cache file of a project: <c>obj/tabstudio.restore.json</c> next to the project file.</summary>
    /// <param name="project">The project.</param>
    public static string GetCacheFilePath(ProjectInfo project)
    {
        ArgumentNullException.ThrowIfNull(project);
        return Path.Combine(project.DirectoryPath, "obj", CacheFileName);
    }

    /// <summary>Restores the packages of a project.</summary>
    /// <remarks>
    /// As in NuGet, the package references of the projects it references (read from disk, transitively) flow into
    /// its graph, one level further away than its own. So the result of a startup project holds everything it
    /// compiles against and everything its program loads, including what its libraries need.
    /// </remarks>
    /// <param name="project">The project whose <c>PackageReference</c> items to restore.</param>
    /// <param name="progress">Receives progress for the status bar, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancels the restore. Packages that finished downloading stay installed.</param>
    /// <returns>
    /// The packages and their assemblies, and every problem found. Problems never throw: a package that cannot be
    /// restored is an error in <see cref="RestoreResult.Diagnostics"/>, and the others are still restored.
    /// </returns>
    public async Task<RestoreResult> RestoreAsync(ProjectInfo project, IProgress<RestoreProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);

        var stopwatch = Stopwatch.StartNew();
        var diagnostics = new List<BuildDiagnostic>();
        var frameworkDiagnostics = new List<BuildDiagnostic>();
        var frameworks = await RestoreFrameworksAsync(project, frameworkDiagnostics, cancellationToken).ConfigureAwait(false);
        project = await WithUnoPackagesAsync(project, diagnostics, cancellationToken).ConfigureAwait(false);
        var graph = ProjectPackageReferences.ReadGraph(project, diagnostics);
        var cachePath = GetCacheFilePath(project);
        var inputsHash = RestoreCache.ComputeInputsHash(graph, Options);

        // A reference that could not be read is not in the hash, so the cache cannot vouch for the project.
        var referencesValid = diagnostics.All(d => d.Severity != DiagnosticSeverityLevel.Error);
        if (Options.UseRestoreCache && referencesValid && RestoreCache.TryRead(cachePath, inputsHash, project.Name) is { } cached)
        {
            progress?.Report(new RestoreProgress(RestoreStage.Completed, $"The packages of {project.Name} are up to date", cached.Packages.Count, cached.Packages.Count));
            var fromCache = cached with { UnrestoredReferences = FindUnrestoredReferences(project, cached.Packages) };
            return WithFrameworks(fromCache, frameworks, frameworkDiagnostics) with { Duration = stopwatch.Elapsed };
        }

        diagnostics.AddRange(graph.Dependencies
            .Where(d => !d.IsProject && _frameworkPackages.Provides(d.Id, d.Range))
            .Select(d => PackageDiagnostics.Warning(
                PackageDiagnosticIds.RedundantFrameworkPackage,
                $"{d.Id} is part of {Options.TargetFramework}, so the reference to it is probably unnecessary. Its assemblies are not used.",
                project.Name)));

        IReadOnlyList<RestoredPackage> packages = [];
        if (graph.HasPackages)
        {
            progress?.Report(new RestoreProgress(RestoreStage.Resolving, $"Resolving the packages of {project.Name}", 0, 0));
            var finder = new PackageFinder(CreateFeeds(), _packages, _framework, _fallback, _frameworkPackages, graph.Projects, progress);
            var resolution = await new DependencyResolver(finder).ResolveAsync(project.Name, graph.Dependencies, cancellationToken).ConfigureAwait(false);
            diagnostics.AddRange(resolution.Diagnostics);

            foreach (var node in resolution.Unresolved)
            {
                if (await finder.DescribeUnresolvedAsync(node, project.Name, cancellationToken).ConfigureAwait(false) is { } unresolved)
                {
                    diagnostics.Add(unresolved);
                }
            }

            diagnostics.AddRange(finder.Failures.Select(f => DescribeSourceFailure(f, project.Name, isError: resolution.Unresolved.Count > 0)));
            packages = await InstallAsync(resolution.Packages, project.Name, diagnostics, progress, cancellationToken).ConfigureAwait(false);
        }

        var result = new RestoreResult
        {
            ProjectName = project.Name,
            Packages = packages,
            CompileAssemblies = Combine(packages, p => p.CompileAssemblies),
            RuntimeAssemblies = Combine(packages, p => p.RuntimeAssemblies),
            NativeLibraries = packages.SelectMany(p => p.NativeLibraries).DistinctBy(Path.GetFileName, StringComparer.Ordinal).ToList(),
            Analyzers = Combine(packages.Where(p => p.FlowsAnalyzers).ToList(), p => p.Analyzers),
            Diagnostics = PackageDiagnostics.Sort(diagnostics),
            UnrestoredReferences = FindUnrestoredReferences(project, packages),
            Duration = stopwatch.Elapsed,
        };

        // A restore with errors still returns the packages it restored, but it is never remembered: the next
        // restore with the same inputs tries the sources again and reports the errors again.
        if (Options.UseRestoreCache && result.Success)
        {
            RestoreCache.TryWrite(cachePath, inputsHash, result);
        }

        progress?.Report(new RestoreProgress(RestoreStage.Completed, $"Restored {packages.Count} packages for {project.Name}", packages.Count, packages.Count));
        return WithFrameworks(result, frameworks, frameworkDiagnostics);
    }

    /// <summary>
    /// Restores .NET's own source generators, such as those behind <c>[GeneratedRegex]</c> and <c>[JsonSerializable]</c>,
    /// from the targeting pack on NuGet. Every C# project can use them; the pack is downloaded once.
    /// </summary>
    /// <param name="cancellationToken">Cancels the download.</param>
    /// <returns>The generators, and any problem, such as no network before the pack was ever downloaded.</returns>
    public async Task<(IReadOnlyList<string> Analyzers, IReadOnlyList<BuildDiagnostic> Diagnostics)> RestoreFrameworkAnalyzersAsync(CancellationToken cancellationToken = default)
    {
        var diagnostics = new List<BuildDiagnostic>();
        var framework = await _sharedFrameworks.RestoreAsync(SharedFrameworks.NetCoreFramework, ".NET", diagnostics, cancellationToken).ConfigureAwait(false);
        return (framework?.Analyzers ?? [], diagnostics);
    }

    /// <summary>
    /// Restores the shared frameworks a project uses, such as ASP.NET Core: its own, and, as in the .NET SDK, those of
    /// the projects it references, which flow to it.
    /// </summary>
    private async Task<List<SharedFrameworkAssets>> RestoreFrameworksAsync(ProjectInfo project, List<BuildDiagnostic> diagnostics, CancellationToken cancellationToken)
    {
        var frameworks = new List<SharedFrameworkAssets>();
        foreach (var name in ProjectPackageReferences.FrameworkReferencesWithReferencedProjects(project))
        {
            if (await _sharedFrameworks.RestoreAsync(name, project.Name, diagnostics, cancellationToken).ConfigureAwait(false) is { } framework)
            {
                frameworks.Add(framework);
            }
        }

        return frameworks;
    }

    /// <summary>
    /// Adds the shared frameworks to a result. A framework's assemblies win over packages that carry assemblies
    /// with the same names, as the SDK's conflict resolution does.
    /// </summary>
    private static RestoreResult WithFrameworks(RestoreResult result, List<SharedFrameworkAssets> frameworks, List<BuildDiagnostic> frameworkDiagnostics)
    {
        if (frameworks.Count == 0 && frameworkDiagnostics.Count == 0)
        {
            return result;
        }

        var provided = frameworks.SelectMany(f => f.RuntimeAssemblies.Concat(f.CompileAssemblies))
            .Select(Path.GetFileName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        bool NotProvided(string path) => !provided.Contains(Path.GetFileName(path));

        return result with
        {
            Frameworks = frameworks,
            CompileAssemblies = [.. frameworks.SelectMany(f => f.CompileAssemblies), .. result.CompileAssemblies.Where(NotProvided)],
            RuntimeAssemblies = [.. frameworks.SelectMany(f => f.RuntimeAssemblies), .. result.RuntimeAssemblies.Where(NotProvided)],
            Analyzers = [.. frameworks.SelectMany(f => f.Analyzers), .. result.Analyzers.Where(NotProvided)],
            Diagnostics = PackageDiagnostics.Sort([.. frameworkDiagnostics, .. result.Diagnostics]),
        };
    }

    private List<IPackageFeed> CreateFeeds() =>
        Options.Sources
            .Select(source => source.IsLocal ? (IPackageFeed)new LocalFolderPackageFeed(source) : new HttpPackageFeed(source, _http))
            .ToList();

    /// <summary>Downloads the packages that are not installed yet and selects the assets of every package.</summary>
    private async Task<IReadOnlyList<RestoredPackage>> InstallAsync(
        IReadOnlyList<ResolvedPackage> resolved,
        string projectName,
        List<BuildDiagnostic> diagnostics,
        IProgress<RestoreProgress>? progress,
        CancellationToken cancellationToken)
    {
        var directories = new string?[resolved.Count];
        var problems = new ConcurrentQueue<BuildDiagnostic>();
        var completed = 0;
        var parallel = new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Options.MaxParallelDownloads), CancellationToken = cancellationToken };

        await Parallel.ForEachAsync(Enumerable.Range(0, resolved.Count), parallel, async (index, token) =>
        {
            var package = resolved[index];
            var match = package.Match;
            var name = $"{match.Id} {match.Version.ToNormalizedString()}";
            try
            {
                directories[index] = await InstallAsync(match, token).ConfigureAwait(false);
            }
            catch (PackageSourceException ex)
            {
                problems.Enqueue(DescribeSourceFailure(ex, projectName, isError: true));
            }
            catch (InvalidPackageException ex)
            {
                problems.Enqueue(PackageDiagnostics.Error(PackageDiagnosticIds.InvalidPackage, $"{name} could not be installed: {ex.Message}", projectName));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                problems.Enqueue(PackageDiagnostics.Error(
                    PackageDiagnosticIds.PackagesFolderFailed,
                    $"{name} could not be written to the packages folder '{_packages.Root}': {ex.Message}",
                    projectName));
            }

            var done = Interlocked.Increment(ref completed);
            progress?.Report(new RestoreProgress(RestoreStage.Downloading, $"Restored {name}", done, resolved.Count));
        }).ConfigureAwait(false);

        // The RID graph takes in the runtime.json files of the graph's packages, as NuGet's does, before any
        // runtimes/{rid}/ folder is chosen.
        var installed = directories.OfType<string>().ToList();
        IReadOnlyList<string> runtimeIdentifiers = Options.RuntimeIdentifier is { Length: > 0 } rid
            ? RuntimeGraph.Fallback.MergeFrom(installed).Expand(rid)
            : [];
        var restored = new List<RestoredPackage>(resolved.Count);
        for (var index = 0; index < resolved.Count; index++)
        {
            if (directories[index] is { } directory)
            {
                restored.Add(SelectAssets(resolved[index], directory, projectName, runtimeIdentifiers, problems));
            }
        }

        diagnostics.AddRange(problems);
        return restored;
    }

    private async Task<string> InstallAsync(PackageMatch match, CancellationToken cancellationToken)
    {
        if (_packages.IsInstalled(match.Id, match.Version))
        {
            return _packages.GetPackageDirectory(match.Id, match.Version);
        }

        var feed = match.Feed ?? throw new InvalidPackageException(
            $"The package was removed from the packages folder '{_packages.Root}' during the restore. Restore again to download it.");
        return await _packages.InstallAsync(feed, match.Id, match.Version, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Selects the assemblies of an installed package, leaving out those the framework provides.</summary>
    private RestoredPackage SelectAssets(
        ResolvedPackage package,
        string directory,
        string projectName,
        IReadOnlyList<string> runtimeIdentifiers,
        ConcurrentQueue<BuildDiagnostic> problems)
    {
        var match = package.Match;
        var name = $"{match.Id} {match.Version.ToNormalizedString()}";
        var files = Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .Select(file => Path.GetRelativePath(directory, file).Replace(Path.DirectorySeparatorChar, '/'))
            .ToList();
        var assets = AssetSelector.Select(files, _framework, _fallback, Options.CompilerVersion, runtimeIdentifiers);
        var buildFiles = AssetSelector.SelectBuildFiles(files, match.Id, _framework, _fallback, package.Assets);

        if (!assets.IsCompatible)
        {
            var supported = string.Join(", ", assets.SupportedFrameworks.Select(f => f.GetShortFolderName()).Order(StringComparer.Ordinal));
            problems.Enqueue(PackageDiagnostics.Error(
                PackageDiagnosticIds.IncompatibleFramework,
                $"Package {name} is not compatible with {Options.TargetFramework} ({_framework.DotNetFrameworkName}). It supports: {supported}.",
                projectName));
        }
        else if (assets.FallbackFramework is { } fallback)
        {
            problems.Enqueue(PackageDiagnostics.Warning(
                PackageDiagnosticIds.FallbackFramework,
                $"Package {name} was restored using '{fallback.GetShortFolderName()}' instead of the target framework '{Options.TargetFramework}'. It may not be fully compatible.",
                projectName));
        }

        if (assets.AnalyzersNeedCompiler is { } needed)
        {
            problems.Enqueue(PackageDiagnostics.Warning(
                PackageDiagnosticIds.AnalyzersNeedNewerCompiler,
                $"The source generators and analyzers of {name} need Roslyn {needed} or later, and TabStudio has Roslyn {Options.CompilerVersion}, so they do not run and the code they generate is missing. A version of {match.Id} built for an earlier compiler works.",
                projectName));
        }

        var compile = ToFullPaths(directory, assets.CompileAssemblies);
        var runtime = ToFullPaths(directory, assets.RuntimeAssemblies);
        var providedByFramework = assets.CompileAssemblies.Count + assets.RuntimeAssemblies.Count > 0 && compile.Count + runtime.Count == 0;
        var analyzers = assets.Analyzers.Select(path => Path.Combine(directory, path.Replace('/', Path.DirectorySeparatorChar))).ToList();
        var native = assets.NativeLibraries.Select(path => Path.Combine(directory, path.Replace('/', Path.DirectorySeparatorChar))).ToList();
        return new RestoredPackage(match.Id, match.Version.ToNormalizedString(), directory, package.IsDirect, compile, runtime, providedByFramework)
        {
            Analyzers = analyzers,
            FlowsAnalyzers = package.FlowsAnalyzers,
            NativeLibraries = native,
            BuildFiles = [.. buildFiles.Select(path => Path.Combine(directory, path.Replace('/', Path.DirectorySeparatorChar)))],
            Depth = package.Depth,
        };
    }

    /// <summary>Turns relative asset paths into full paths, without the assemblies the framework already has.</summary>
    private List<string> ToFullPaths(string directory, IReadOnlyList<string> relativePaths) =>
        relativePaths
            .Where(path => !_frameworkPackages.IsFrameworkAssembly(Path.GetFileNameWithoutExtension(path)))
            .Select(path => Path.Combine(directory, path.Replace('/', Path.DirectorySeparatorChar)))
            .ToList();

    /// <summary>
    /// The project's own package references that no restored package answers: not found, an invalid version, or a
    /// download that failed. A package restored with problems, such as a version conflict, is restored.
    /// </summary>
    private static List<string> FindUnrestoredReferences(ProjectInfo project, IReadOnlyList<RestoredPackage> packages)
    {
        var restored = packages.Select(p => p.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return project.PackageReferences
            .Select(r => r.Name)
            .Where(id => !restored.Contains(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>The assemblies of every package, without two assemblies of the same name: the first package wins.</summary>
    private static List<string> Combine(IReadOnlyList<RestoredPackage> packages, Func<RestoredPackage, IReadOnlyList<string>> select) =>
        packages
            .SelectMany(select)
            .DistinctBy(path => Path.GetFileNameWithoutExtension(path), StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static BuildDiagnostic DescribeSourceFailure(PackageSourceException failure, string projectName, bool isError)
    {
        var id = failure.IsUnreachable ? PackageDiagnosticIds.NetworkUnreachable : PackageDiagnosticIds.SourceFailed;
        var message = failure.IsUnreachable
            ? $"{failure.Message} Check the device's internet connection; packages restored before keep working offline."
            : failure.Message;
        if (!isError)
        {
            message += " Packages already on this device were used instead.";
        }

        return isError ? PackageDiagnostics.Error(id, message, projectName) : PackageDiagnostics.Warning(id, message, projectName);
    }
}
