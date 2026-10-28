using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using TabStudio.Compilation.Uno;
using TabStudio.Core.Diagnostics;
using TsProject = TabStudio.Core.Workspaces.ProjectInfo;

namespace TabStudio.Compilation;

/// <summary>
/// Uno Platform and WinUI 3 projects: their XAML and string resources are additional files, which Uno's XAML
/// generator from the restored Uno.WinUI package turns into C# in every compilation, configured as Uno.Sdk configures
/// it (see <see cref="UnoBuild"/>); their XAML files are documents of the language service, with their own errors.
/// </summary>
public sealed partial class RoslynWorkspaceHost
{
    /// <summary>The XAML and resource files of each loaded Uno project, for its analyzer configuration.</summary>
    private readonly Dictionary<ProjectId, IReadOnlyList<string>> _unoGeneratorInputs = [];

    /// <summary>Whether a file is a XAML file of a loaded Uno or WinUI 3 project, which Uno's XAML generator compiles.</summary>
    /// <param name="filePath">Full path of the file.</param>
    public bool IsUnoXamlDocument(string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        lock (_gate)
        {
            return IsUnoXamlDocumentUnderGate(filePath);
        }
    }

    /// <summary>Returns the errors and warnings of a XAML file of an Uno project: the XAML generator's, and the C# compiler's in the code it generated for the file.</summary>
    /// <param name="filePath">Full path of the file.</param>
    /// <param name="cancellationToken">Cancels the analysis.</param>
    public async Task<IReadOnlyList<BuildDiagnostic>> GetUnoXamlDiagnosticsAsync(string filePath, CancellationToken cancellationToken = default)
    {
        Project? project;
        TsProject? info;
        lock (_gate)
        {
            if (!IsUnoXamlDocumentUnderGate(filePath) || !_additionalDocuments.TryGetValue(filePath, out var id))
            {
                return [];
            }

            project = _workspace.CurrentSolution.GetProject(id.ProjectId);
            info = _projects.GetValueOrDefault(id.ProjectId);
        }

        if (project is null || info is null || await project.GetCompilationAsync(cancellationToken).ConfigureAwait(false) is not { } compilation)
        {
            return [];
        }

        var diagnostics = await UnoCompilations.MapAsync(project, info, compilation, cancellationToken).ConfigureAwait(false);
        return [.. diagnostics.Where(d => d.Location is { } location && string.Equals(location.FilePath, filePath, StringComparison.Ordinal))];
    }

    /// <summary>
    /// The parse options of a project: Razor's, with Uno's symbols for an Uno project; Visual Basic's for a Visual
    /// Basic project.
    /// </summary>
    private static ParseOptions ParseOptionsFor(TsProject? project, bool optimize) =>
        project is { IsVisualBasic: true }
            ? VisualBasic.VisualBasicCompiler.CreateParseOptions(project, optimize)
            : UnoBuild.WithSymbols(project, Razor.RazorCompiler.ParseOptionsFor(project, optimize));

    /// <summary>The compiler's diagnostics of a file, with the "not found" errors about Windows-only APIs explained for an Uno project.</summary>
    private static IEnumerable<Diagnostic> ExplainForUno(TsProject project, IEnumerable<Diagnostic> diagnostics) =>
        UnoBuild.UsesUno(project) ? diagnostics.Select(UnoCompatibility.Explain) : diagnostics;

    /// <summary>Whether a source file of a project is compiled: always, except an Uno project's other platforms' files.</summary>
    private static bool IsCompiledSource(TsProject project, string path) => UnoBuild.IsCompiled(project, path);

    /// <summary>
    /// Adds an Uno project's XAML and resource files as additional documents, and the sources of Uno.Sdk's implicit
    /// global usings and of the build tools TabStudio stands in for, while the project is created. The caller holds the gate.
    /// </summary>
    private void AddUnoDocuments(
        TsProject project,
        ProjectId id,
        IReadOnlyList<string> allProjectDirectories,
        ISourceTextProvider sources,
        List<DocumentInfo> documents,
        List<DocumentInfo> additionalDocuments)
    {
        if (!UnoBuild.UsesUno(project))
        {
            return;
        }

        var inputs = UnoBuild.EnumerateGeneratorInputs(project, allProjectDirectories);
        _unoGeneratorInputs[id] = inputs;
        foreach (var path in inputs)
        {
            var documentId = DocumentId.CreateNewId(id, path);
            additionalDocuments.Add(CreateDocumentInfo(documentId, path, ReadSource(sources, path), isGenerated: false));
            _additionalDocuments[path] = documentId;
        }

        AddGenerated(UnoBuild.CreateImplicitUsingsSource(project), "UnoGlobalUsings");
        AddGenerated(UnoBuild.CreateBuildToolsSource(project), "UnoBuildTools");

        void AddGenerated(string? source, string name)
        {
            if (source is not null)
            {
                var path = Path.Combine(project.DirectoryPath, "obj", $"{project.Name}.{name}.g.cs");
                documents.Add(CreateDocumentInfo(DocumentId.CreateNewId(id, path), path, source, isGenerated: true));
            }
        }
    }

    /// <summary>
    /// The Uno part of a project's generated analyzer configuration: the build properties Uno's XAML generator reads,
    /// and a section per XAML and resource file with the item metadata it reads, as Uno.Sdk writes them.
    /// </summary>
    private static void AppendUnoAnalyzerConfig(TsProject project, IReadOnlyList<string> inputs, SortedDictionary<string, string> properties, StringBuilder sections)
    {
        if (!UnoBuild.UsesUno(project))
        {
            return;
        }

        var symbols = UnoBuild.WithSymbols(project, CSharpCompiler.CreateParseOptions()).PreprocessorSymbolNames;
        foreach (var (key, value) in UnoBuild.GeneratorProperties(project, symbols))
        {
            properties[key] = value;
        }

        foreach (var path in inputs)
        {
            if (UnoBuild.ItemGroupOf(project, path) is { } group)
            {
                sections.Append('\n').Append(UnoBuild.FileSection(path, group));
            }
        }
    }

    private bool IsUnoXamlDocumentUnderGate(string filePath) =>
        filePath.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase)
        && _additionalDocuments.TryGetValue(filePath, out var id)
        && UnoBuild.UsesUno(_projects.GetValueOrDefault(id.ProjectId));
}
