using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using TabStudio.Compilation.Razor;
using TabStudio.Core.Diagnostics;
using TsProject = TabStudio.Core.Workspaces.ProjectInfo;

namespace TabStudio.Compilation;

/// <summary>
/// Razor: the <c>.razor</c> components and <c>.cshtml</c> views and pages of projects on the Razor, Web and Blazor
/// WebAssembly SDKs. They are additional files of their project, which the Razor compiler turns into C# in every
/// compilation (see <see cref="RazorCompiler"/>), configured the way the SDK configures it. Their diagnostics are those
/// of the Razor compiler and of the C# it generates, placed in the Razor file.
/// </summary>
public sealed partial class RoslynWorkspaceHost
{
    /// <summary>The Razor files of the loaded projects; also in <see cref="_additionalDocuments"/>.</summary>
    private readonly HashSet<string> _razorDocuments = new(StringComparer.Ordinal);

    /// <summary>The Razor files of each loaded project, for its analyzer configuration.</summary>
    private readonly Dictionary<ProjectId, IReadOnlyList<string>> _razorFiles = [];

    /// <summary>Whether a file is a Razor component, view or page of a loaded project.</summary>
    /// <param name="filePath">Full path of the file.</param>
    public bool IsRazorDocument(string filePath)
    {
        lock (_gate)
        {
            return _razorDocuments.Contains(filePath);
        }
    }

    /// <summary>
    /// Adds a project's Razor files as additional documents, and the source of the attributes the Razor SDK adds to
    /// it, while the project is created. The caller holds the gate.
    /// </summary>
    private void AddRazorDocuments(
        TsProject project,
        ProjectId id,
        IReadOnlyList<string> allProjectDirectories,
        ISourceTextProvider sources,
        List<DocumentInfo> documents,
        List<DocumentInfo> additionalDocuments)
    {
        var razorFiles = RazorProjects.EnumerateRazorFiles(project, allProjectDirectories);
        _razorFiles[id] = razorFiles;
        if (razorFiles.Count == 0 && !RazorProjects.UsesRazor(project))
        {
            return;
        }

        foreach (var path in razorFiles)
        {
            var documentId = DocumentId.CreateNewId(id, path);
            additionalDocuments.Add(CreateDocumentInfo(documentId, path, ReadSource(sources, path), isGenerated: false));
            _additionalDocuments[path] = documentId;
            _razorDocuments.Add(path);
        }

        var referenced = project.ProjectReferences
            .Select(reference => _projects.Values.FirstOrDefault(p => string.Equals(p.FilePath, reference, StringComparison.Ordinal)))
            .OfType<TsProject>();
        if (RazorProjects.CreateSdkSource(project, razorFiles, referenced) is { } sdkSource)
        {
            var path = Path.Combine(project.DirectoryPath, "obj", $"{project.Name}.RazorAssemblyInfo.g.cs");
            documents.Add(CreateDocumentInfo(DocumentId.CreateNewId(id, path), path, sdkSource, isGenerated: true));
        }
    }

    /// <summary>
    /// The Razor part of a project's generated analyzer configuration: the build properties the Razor compiler reads,
    /// and a section per Razor file with its target path and CSS scope, as the SDK writes them.
    /// </summary>
    private static void AppendRazorAnalyzerConfig(TsProject project, IReadOnlyList<string> razorFiles, SortedDictionary<string, string> properties, StringBuilder sections)
    {
        if (!RazorProjects.UsesRazor(project))
        {
            return;
        }

        foreach (var (key, value) in RazorProjects.CompilerProperties(project))
        {
            properties[key] = value;
        }

        foreach (var path in razorFiles)
        {
            sections.Append(CultureInfo.InvariantCulture, $"\n[{path.Replace('\\', '/')}]\n");
            sections.Append(CultureInfo.InvariantCulture, $"build_metadata.AdditionalFiles.TargetPath = {RazorProjects.EncodedTargetPath(project, path)}\n");
            sections.Append(CultureInfo.InvariantCulture, $"build_metadata.AdditionalFiles.CssScope = {RazorProjects.CssScopeOf(project, path)}\n");
        }
    }

    /// <summary>
    /// The errors and warnings of a Razor file: the Razor compiler's, and those of the C# it generated from the file,
    /// or from any file an <c>_Imports.razor</c> or <c>_ViewImports.cshtml</c> applies to.
    /// </summary>
    private async Task<IReadOnlyList<BuildDiagnostic>> GetRazorDocumentDiagnosticsAsync(string filePath, CancellationToken cancellationToken)
    {
        Project? project;
        TsProject? info;
        lock (_gate)
        {
            if (!_razorDocuments.Contains(filePath) || !_additionalDocuments.TryGetValue(filePath, out var id))
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

        var importsFolder = IsImportsFile(filePath) ? Path.GetDirectoryName(filePath) + Path.DirectorySeparatorChar : null;
        var diagnostics = new List<Diagnostic>();
        foreach (var tree in compilation.SyntaxTrees)
        {
            if (RazorSourceMap.SourceFileOf(tree) is { } source
                && (string.Equals(source, filePath, StringComparison.Ordinal)
                    || (importsFolder is not null && source.StartsWith(importsFolder, StringComparison.Ordinal))))
            {
                diagnostics.AddRange(compilation.GetSemanticModel(tree).GetDiagnostics(cancellationToken: cancellationToken));
            }
        }

        diagnostics.AddRange(await GeneratorDiagnostics.GetAsync(project, cancellationToken).ConfigureAwait(false));
        var mapper = await RazorCompilations.CreateMapperAsync(project, cancellationToken).ConfigureAwait(false);
        return mapper.Map(diagnostics, info.Name)
            .Where(d => d.Location is { } location && string.Equals(location.FilePath, filePath, StringComparison.Ordinal))
            .ToList();
    }

    /// <summary>
    /// The C# the Razor compiler generated for a Razor file, and its current text, for IntelliSense in the file.
    /// </summary>
    /// <param name="filePath">Full path of the Razor file.</param>
    /// <param name="cancellationToken">Cancels the work, which may run the generators.</param>
    /// <returns>The generated document and the Razor file's text, or <see langword="null"/> when the file is not loaded.</returns>
    internal async Task<(Document Generated, SourceText RazorText)?> GetRazorGeneratedDocumentAsync(string filePath, CancellationToken cancellationToken)
    {
        Project? project;
        TextDocument? razor;
        lock (_gate)
        {
            if (!_razorDocuments.Contains(filePath) || !_additionalDocuments.TryGetValue(filePath, out var id))
            {
                return null;
            }

            var solution = _workspace.CurrentSolution;
            project = solution.GetProject(id.ProjectId);
            razor = solution.GetAdditionalDocument(id);
        }

        if (project is null || razor is null)
        {
            return null;
        }

        var text = await razor.GetTextAsync(cancellationToken).ConfigureAwait(false);
        foreach (var generated in await project.GetSourceGeneratedDocumentsAsync(cancellationToken).ConfigureAwait(false))
        {
            if (await generated.GetSyntaxTreeAsync(cancellationToken).ConfigureAwait(false) is { } tree
                && string.Equals(RazorSourceMap.SourceFileOf(tree), filePath, StringComparison.Ordinal))
            {
                return (generated, text);
            }
        }

        return null;
    }

    /// <summary>The Roslyn project that owns a Razor file, for IntelliSense in it.</summary>
    /// <param name="filePath">Full path of the Razor file.</param>
    internal Project? GetRazorProject(string filePath)
    {
        lock (_gate)
        {
            return _razorDocuments.Contains(filePath) && _additionalDocuments.TryGetValue(filePath, out var id)
                ? _workspace.CurrentSolution.GetProject(id.ProjectId)
                : null;
        }
    }

    /// <summary>The current text of a Razor file of a loaded project.</summary>
    /// <param name="filePath">Full path of the Razor file.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    internal async Task<SourceText?> GetRazorTextAsync(string filePath, CancellationToken cancellationToken)
    {
        TextDocument? document;
        lock (_gate)
        {
            document = _razorDocuments.Contains(filePath) && _additionalDocuments.TryGetValue(filePath, out var id)
                ? _workspace.CurrentSolution.GetAdditionalDocument(id)
                : null;
        }

        return document is null ? null : await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The text of a Razor file, when it is at hand without waiting, for checks on every keystroke.</summary>
    /// <param name="filePath">Full path of the Razor file.</param>
    internal SourceText? TryGetRazorText(string filePath)
    {
        lock (_gate)
        {
            return _razorDocuments.Contains(filePath)
                && _additionalDocuments.TryGetValue(filePath, out var id)
                && _workspace.CurrentSolution.GetAdditionalDocument(id) is { } document
                && document.TryGetText(out var text)
                    ? text
                    : null;
        }
    }

    /// <summary>Whether a Razor file's directives apply to the other Razor files of its folder and below.</summary>
    private static bool IsImportsFile(string filePath) =>
        Path.GetFileName(filePath) is var name
        && (string.Equals(name, "_Imports.razor", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "_ViewImports.cshtml", StringComparison.OrdinalIgnoreCase));
}
