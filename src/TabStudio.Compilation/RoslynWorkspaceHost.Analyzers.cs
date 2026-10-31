using System.Globalization;
using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using TabStudio.Core.Diagnostics;
using TabStudio.Core.Workspaces;
using TsProject = TabStudio.Core.Workspaces.ProjectInfo;

namespace TabStudio.Compilation;

/// <summary>
/// Source generators: those of .NET itself, such as the ones behind <c>[GeneratedRegex]</c> and
/// <c>[JsonSerializable]</c>, those NuGet packages bring, such as CommunityToolkit.Mvvm's, and those of projects of the
/// workspace referenced with <c>OutputItemType="Analyzer"</c> (see <see cref="ProjectGeneratorReference"/>). Roslyn runs
/// them as part of every compilation, so their code is there for IntelliSense, diagnostics and builds alike.
/// </summary>
/// <remarks>
/// Packages also bring diagnostic analyzers, which are loaded with the generators but not run: on a tablet the
/// compiler's own diagnostics are what the Error List shows.
/// </remarks>
public sealed partial class RoslynWorkspaceHost
{
    private readonly Dictionary<string, IReadOnlyList<AnalyzerReference>> _packageAnalyzers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, AnalyzerReference> _analyzerCache = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Version> _analyzerVersions = new(StringComparer.Ordinal);
    private IReadOnlyList<AnalyzerReference> _frameworkAnalyzers = [];

    /// <summary>
    /// Sets the analyzers and source generators of a project's packages, replacing any it had before, such as
    /// <c>RestoreResult.Analyzers</c>.
    /// </summary>
    /// <param name="projectFilePath">Full path of the project file. The project does not have to be loaded yet.</param>
    /// <param name="analyzerPaths">Full paths of the analyzer assemblies.</param>
    public void SetAnalyzers(string projectFilePath, IReadOnlyList<string> analyzerPaths)
    {
        ArgumentException.ThrowIfNullOrEmpty(projectFilePath);
        ArgumentNullException.ThrowIfNull(analyzerPaths);

        lock (_gate)
        {
            _packageAnalyzers[projectFilePath] = analyzerPaths.Distinct(StringComparer.Ordinal).Select(GetOrCreateAnalyzerReference).ToList();
            ApplyAnalyzerReferences(projectFilePath);
            InvalidateGenerators(projectFilePath);
        }
    }

    /// <summary>
    /// Sets the source generators of .NET itself, from its targeting pack, which every C# project uses.
    /// </summary>
    /// <param name="analyzerPaths">Full paths of the generator assemblies.</param>
    public void SetFrameworkAnalyzers(IReadOnlyList<string> analyzerPaths)
    {
        ArgumentNullException.ThrowIfNull(analyzerPaths);

        lock (_gate)
        {
            _frameworkAnalyzers = analyzerPaths.Distinct(StringComparer.Ordinal).Select(GetOrCreateAnalyzerReference).ToList();
            foreach (var path in _projectIds.Keys.ToList())
            {
                ApplyAnalyzerReferences(path);
                InvalidateGenerators(path);
            }
        }
    }

    /// <summary>
    /// The analyzer assemblies a project uses, from .NET and its packages, one per file name (see
    /// <see cref="WithoutDuplicateFiles"/>).
    /// </summary>
    /// <param name="projectFilePath">Full path of the project file.</param>
    public IReadOnlyList<string> GetAnalyzerPaths(string projectFilePath)
    {
        lock (_gate)
        {
            return WithoutDuplicateFiles(_frameworkAnalyzers.Concat(_packageAnalyzers.GetValueOrDefault(projectFilePath) ?? []))
                .Select(reference => reference.FullPath ?? reference.Display ?? string.Empty)
                .ToList();
        }
    }

    /// <summary>
    /// The framework's and the project's analyzers, one per file name (see <see cref="WithoutDuplicateFiles"/>), without
    /// the generators the SDK leaves off unless the project turns them on (see <see cref="SdkGenerators"/>), and the
    /// generators of the projects it references with <c>OutputItemType="Analyzer"</c>. A Razor project gets the Razor
    /// compiler TabStudio carries, in place of any a package brings. An Avalonia project gets TabStudio's XAML generator
    /// in place of Avalonia's, which expects XAML compiled after the C# compiler. The caller holds the gate.
    /// </summary>
    private IReadOnlyList<AnalyzerReference> GetAnalyzerReferences(TsProject project)
    {
        var all = WithoutDuplicateFiles(_frameworkAnalyzers.Concat(_packageAnalyzers.GetValueOrDefault(project.FilePath) ?? []));
        var removed = TabStudio.Core.Workspaces.TargetItems.RemovedAnalyzers(
            project,
            [.. all.Where(a => a.FullPath is not null).Select(a => new TabStudio.Core.Workspaces.AnalyzerItem(a.FullPath!, PackageIdOf(a.FullPath!)))]);
        var analyzers = all
            .Where(a => a.FullPath is null || !removed.Contains(a.FullPath, StringComparer.Ordinal))
            .Where(a => SdkGenerators.IsEnabled(project, a.FullPath))
            .Concat(GeneratorProjectReferences(project));
        if (Razor.RazorProjects.UsesRazor(project))
        {
            analyzers = analyzers
                .Where(a => !string.Equals(Path.GetFileName(a.FullPath), "Microsoft.CodeAnalysis.Razor.Compiler.dll", StringComparison.OrdinalIgnoreCase))
                .Append(Razor.RazorCompiler.Reference);
        }

        if (!UsesAvaloniaXaml(project))
        {
            return [.. analyzers, .. WpfAnalyzers(project)];
        }

        analyzers = analyzers.Where(a => !string.Equals(Path.GetFileName(a.FullPath), "Avalonia.Generators.dll", StringComparison.OrdinalIgnoreCase));
        return [.. analyzers, Xaml.GeneratorReference.AvaloniaXaml];
    }

    /// <summary>
    /// The package an analyzer comes from, by its folder in the packages folder (<c>{id}/{version}/analyzers/...</c>), as
    /// the <c>NuGetPackageId</c> metadata packages' targets match analyzers by; <see langword="null"/> for one elsewhere.
    /// </summary>
    private static string? PackageIdOf(string path)
    {
        var parts = path.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);
        var analyzers = Array.FindLastIndex(parts, p => p.Equals("analyzers", StringComparison.OrdinalIgnoreCase));
        return analyzers >= 2 ? parts[analyzers - 2] : null;
    }

    /// <summary>
    /// Whether a project uses Avalonia, whose <c>.axaml</c> files TabStudio's XAML generator compiles: it references
    /// Avalonia or a package that brings it, such as <c>Avalonia.Desktop</c> (see <see cref="ProjectClassifier.UsesAvalonia"/>).
    /// </summary>
    private static bool UsesAvaloniaXaml(TsProject project) => ProjectClassifier.UsesAvalonia(project);

    /// <summary>
    /// One analyzer assembly per file name, as the SDK's <c>ResolvePackageFileConflicts</c> leaves them: the one with the
    /// highest assembly version, the first on a tie, so the framework's wins over a package's. A generator that both
    /// ASP.NET Core and a package bring, such as <c>Microsoft.Extensions.Logging.Generators.dll</c>, would otherwise run
    /// twice and generate every member twice (CS0102, CS0111). The caller holds the gate.
    /// </summary>
    /// <param name="analyzers">The analyzers, the framework's first.</param>
    private List<AnalyzerReference> WithoutDuplicateFiles(IEnumerable<AnalyzerReference> analyzers)
    {
        var all = analyzers.ToList();
        var winners = new Dictionary<string, AnalyzerReference>(StringComparer.OrdinalIgnoreCase);
        foreach (var analyzer in all)
        {
            if (analyzer.FullPath is not { } path)
            {
                continue;
            }

            var file = Path.GetFileName(path);
            if (!winners.TryGetValue(file, out var known) || AnalyzerVersion(path) > AnalyzerVersion(known.FullPath!))
            {
                winners[file] = analyzer;
            }
        }

        // The same file listed twice, by the framework and by a package, is one shared reference: it is kept once.
        return [.. all
            .Where(a => a.FullPath is not { } path || ReferenceEquals(winners[Path.GetFileName(path)], a))
            .Distinct<AnalyzerReference>(ReferenceEqualityComparer.Instance)];
    }

    /// <summary>The assembly version of an analyzer file, read once; 0.0 when it cannot be read. The caller holds the gate.</summary>
    private Version AnalyzerVersion(string path)
    {
        if (!_analyzerVersions.TryGetValue(path, out var version))
        {
            try
            {
                version = AssemblyName.GetAssemblyName(path).Version ?? new Version(0, 0);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or BadImageFormatException or ArgumentException)
            {
                // Roslyn reports a file it cannot load (CS8034); here it only loses to any copy that can be read.
                version = new Version(0, 0);
            }

            _analyzerVersions[path] = version;
        }

        return version;
    }

    /// <summary>Pushes a project's analyzers into the loaded solution. The caller holds the gate.</summary>
    private void ApplyAnalyzerReferences(string projectFilePath)
    {
        if (_projectIds.TryGetValue(projectFilePath, out var id))
        {
            var solution = _workspace.CurrentSolution.WithProjectAnalyzerReferences(id, GetAnalyzerReferences(_projects[id]));
            _workspace.TryApplyChanges(solution);
        }
    }

    private AnalyzerReference GetOrCreateAnalyzerReference(string path)
    {
        if (!_analyzerCache.TryGetValue(path, out var reference))
        {
            // Registered first, so an analyzer can find a dependency that another folder of its package holds.
            AnalyzerLoader.Shared.AddDependencyLocation(path);
            var file = new AnalyzerFileReference(path, AnalyzerLoader.Shared);
            file.AnalyzerLoadFailed += (_, e) => RecordAnalyzerFailure(path, e);
            reference = file;
            _analyzerCache[path] = reference;
        }

        return reference;
    }

    private readonly Lock _failureGate = new();
    private readonly Dictionary<string, BuildDiagnostic> _analyzerFailures = new(StringComparer.Ordinal);

    /// <summary>
    /// Keeps a failure to load an analyzer or source generator, which Roslyn's workspace only raises as an event: the
    /// compiler would report it as a warning, and without it a generator that never ran looks like code that is wrong.
    /// </summary>
    private void RecordAnalyzerFailure(string path, AnalyzerLoadFailureEventArgs e)
    {
        // A library next to the generators, such as Vogen.SharedTypes.dll, has none of its own.
        if (e.ErrorCode == AnalyzerLoadFailureEventArgs.FailureErrorCode.NoAnalyzers)
        {
            return;
        }

        var file = Path.GetFileName(path);
        var (id, message) = e.ErrorCode switch
        {
            AnalyzerLoadFailureEventArgs.FailureErrorCode.UnableToCreateAnalyzer =>
                ("CS8032", $"An instance of analyzer {e.TypeName} cannot be created from {file}: {e.Message.TrimEnd('.')}. Its generated code is missing."),
            AnalyzerLoadFailureEventArgs.FailureErrorCode.ReferencesNewerCompiler =>
                ("CS9057", $"The analyzer assembly {file} references a newer version of the compiler than the one in TabStudio. Its generated code is missing."),
            _ => ("CS8034", $"Unable to load analyzer assembly {file}: {e.Message.TrimEnd('.')}. Its generated code is missing."),
        };
        lock (_failureGate)
        {
            _analyzerFailures[path + "\n" + e.TypeName] = new BuildDiagnostic(id, DiagnosticSeverityLevel.Warning, message, null);
        }
    }

    /// <summary>The analyzers and generators of a project that could not be loaded, as the compiler's warnings.</summary>
    /// <param name="project">The project.</param>
    /// <param name="analyzerPaths">Full paths of its analyzer assemblies.</param>
    private List<BuildDiagnostic> AnalyzerFailuresFor(TsProject project, IEnumerable<string?> analyzerPaths)
    {
        var paths = analyzerPaths.OfType<string>().ToHashSet(StringComparer.Ordinal);
        lock (_failureGate)
        {
            return [.. _analyzerFailures
                .Where(f => paths.Contains(f.Key[..f.Key.IndexOf('\n', StringComparison.Ordinal)]))
                .Select(f => f.Value with { ProjectName = project.Name })];
        }
    }

    /// <summary>
    /// The build properties generators read, as MSBuild hands them over in a generated global
    /// <c>.editorconfig</c>: every property the project file and its packages' props and targets set, every property
    /// they make visible to generators (empty when nothing sets it), the ones the SDK always provides, a section per
    /// additional file with the item metadata made visible, and for a Razor project a section per Razor file with the
    /// metadata the Razor compiler reads.
    /// </summary>
    private static DocumentInfo CreateAnalyzerConfig(ProjectId id, TsProject project, IReadOnlyList<string> razorFiles, IReadOnlyList<string> unoInputs)
    {
        var path = AnalyzerConfigPath(project);
        return DocumentInfo.Create(
            DocumentId.CreateNewId(id, path),
            Path.GetFileName(path),
            loader: TextLoader.From(TextAndVersion.Create(SourceText.From(AnalyzerConfigText(project, razorFiles, unoInputs), Encoding.UTF8), VersionStamp.Create(), path)),
            filePath: path);
    }

    /// <summary>Where the SDK writes a project's generated analyzer configuration.</summary>
    private static string AnalyzerConfigPath(TsProject project) =>
        Path.Combine(project.DirectoryPath, "obj", $"{project.Name}.GeneratedMSBuildEditorConfig.editorconfig");

    /// <summary>The text of a project's generated analyzer configuration (see <see cref="CreateAnalyzerConfig"/>).</summary>
    private static string AnalyzerConfigText(TsProject project, IReadOnlyList<string> razorFiles, IReadOnlyList<string> unoInputs)
    {
        var properties = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in project.Properties)
        {
            properties[key] = value;
        }

        // The SDK's defaults for what the project does not set, which generators read through CompilerVisibleProperty:
        // ResxSourceGenerator names MudBlazor's LanguageResource class from RootNamespace.
        properties.TryAdd("RootNamespace", project.Name);
        properties.TryAdd("AssemblyName", project.Name);

        // The SDK writes every compiler-visible property, empty when nothing sets it.
        foreach (var name in project.CompilerVisibleProperties)
        {
            properties.TryAdd(name, string.Empty);
        }

        // The inner build's framework, as the SDK compiles the project for it (see ProjectParser.EvaluationTargetFramework).
        properties["TargetFramework"] = ProjectParser.EvaluationTargetFramework(project.TargetFrameworks) ?? ReferenceAssemblies.TargetFramework;
        properties["ProjectDir"] = project.DirectoryPath.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        properties["MSBuildProjectDirectory"] = project.DirectoryPath;
        properties["MSBuildProjectName"] = project.Name;
        properties["UsingMicrosoftNETSdkWeb"] = string.Equals(project.Sdk, "Microsoft.NET.Sdk.Web", StringComparison.OrdinalIgnoreCase) ? "true" : string.Empty;
        var razorSections = new StringBuilder();
        AppendRazorAnalyzerConfig(project, razorFiles, properties, razorSections);
        AppendWpfAnalyzerConfig(project, razorSections);

        AppendUnoAnalyzerConfig(project, unoInputs, properties, razorSections);

        var text = new StringBuilder("is_global = true\n");
        foreach (var (key, value) in properties)
        {
            text.Append(CultureInfo.InvariantCulture, $"build_property.{key} = {value.ReplaceLineEndings(" ")}\n");
        }

        text.Append(razorSections);
        text.Append(AdditionalFileSections(project, [.. razorFiles, .. unoInputs]));
        return text.ToString();
    }
}
