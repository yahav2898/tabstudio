using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using TsProject = TabStudio.Core.Workspaces.ProjectInfo;

namespace TabStudio.Compilation;

/// <summary>
/// Source generators: those of .NET itself, such as the ones behind <c>[GeneratedRegex]</c> and
/// <c>[JsonSerializable]</c>, and those NuGet packages bring, such as CommunityToolkit.Mvvm's. Roslyn runs them as
/// part of every compilation, so their code is there for IntelliSense, diagnostics and builds alike.
/// </summary>
/// <remarks>
/// Packages also bring diagnostic analyzers, which are loaded with the generators but not run: on a tablet the
/// compiler's own diagnostics are what the Error List shows.
/// </remarks>
public sealed partial class RoslynWorkspaceHost
{
    private readonly Dictionary<string, IReadOnlyList<AnalyzerReference>> _packageAnalyzers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, AnalyzerReference> _analyzerCache = new(StringComparer.Ordinal);
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
            }
        }
    }

    /// <summary>The analyzer assemblies a project uses, from .NET and its packages.</summary>
    /// <param name="projectFilePath">Full path of the project file.</param>
    public IReadOnlyList<string> GetAnalyzerPaths(string projectFilePath)
    {
        lock (_gate)
        {
            return _frameworkAnalyzers.Concat(_packageAnalyzers.GetValueOrDefault(projectFilePath) ?? [])
                .Select(reference => reference.FullPath ?? reference.Display ?? string.Empty)
                .ToList();
        }
    }

    /// <summary>
    /// The framework's and the project's analyzers, without the generators the SDK leaves off unless the project turns
    /// them on (see <see cref="SdkGenerators"/>). An Avalonia project gets TabStudio's XAML generator in place of
    /// Avalonia's, which expects XAML compiled after the C# compiler. The caller holds the gate.
    /// </summary>
    private IReadOnlyList<AnalyzerReference> GetAnalyzerReferences(TsProject project)
    {
        var analyzers = _frameworkAnalyzers
            .Concat(_packageAnalyzers.GetValueOrDefault(project.FilePath) ?? [])
            .Where(a => SdkGenerators.IsEnabled(project, a.FullPath));
        if (!UsesAvaloniaXaml(project))
        {
            return [.. analyzers];
        }

        analyzers = analyzers.Where(a => !string.Equals(Path.GetFileName(a.FullPath), "Avalonia.Generators.dll", StringComparison.OrdinalIgnoreCase));
        return [.. analyzers, Xaml.GeneratorReference.AvaloniaXaml];
    }

    /// <summary>Whether a project uses Avalonia, whose <c>.axaml</c> files TabStudio's XAML generator compiles.</summary>
    private static bool UsesAvaloniaXaml(TsProject project) => project.HasPackage("Avalonia");

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
            reference = new AnalyzerFileReference(path, AnalyzerLoader.Shared);
            _analyzerCache[path] = reference;
        }

        return reference;
    }

    /// <summary>
    /// The build properties generators read, as MSBuild hands them over in a generated global
    /// <c>.editorconfig</c>: every property the project file sets, plus the ones the SDK always provides.
    /// </summary>
    private static DocumentInfo CreateAnalyzerConfig(ProjectId id, TsProject project)
    {
        var properties = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in project.Properties)
        {
            properties[key] = value;
        }

        properties["TargetFramework"] = project.TargetFrameworks.Count > 0 ? project.TargetFrameworks[0] : ReferenceAssemblies.TargetFramework;
        properties.TryAdd("RootNamespace", project.Name);
        properties["ProjectDir"] = project.DirectoryPath.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        properties["MSBuildProjectDirectory"] = project.DirectoryPath;
        properties["MSBuildProjectName"] = project.Name;
        properties["UsingMicrosoftNETSdkWeb"] = string.Equals(project.Sdk, "Microsoft.NET.Sdk.Web", StringComparison.OrdinalIgnoreCase) ? "true" : string.Empty;

        var text = new StringBuilder("is_global = true\n");
        foreach (var (key, value) in properties)
        {
            text.Append(CultureInfo.InvariantCulture, $"build_property.{key} = {value.ReplaceLineEndings(" ")}\n");
        }

        var path = Path.Combine(project.DirectoryPath, "obj", $"{project.Name}.GeneratedMSBuildEditorConfig.editorconfig");
        return DocumentInfo.Create(
            DocumentId.CreateNewId(id, path),
            Path.GetFileName(path),
            loader: TextLoader.From(TextAndVersion.Create(SourceText.From(text.ToString(), Encoding.UTF8), VersionStamp.Create(), path)),
            filePath: path);
    }
}
