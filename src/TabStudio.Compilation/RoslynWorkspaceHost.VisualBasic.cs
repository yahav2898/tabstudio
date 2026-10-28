using Microsoft.CodeAnalysis;
using TabStudio.Compilation.VisualBasic;
using TabStudio.Core.Workspaces;
using RoslynProjectInfo = Microsoft.CodeAnalysis.ProjectInfo;
using TsProject = TabStudio.Core.Workspaces.ProjectInfo;

namespace TabStudio.Compilation;

/// <summary>
/// Visual Basic projects: Roslyn's Visual Basic compiler, in the same workspace as C#, so IntelliSense, quick fixes,
/// navigation, builds, the debugger and Live Unit Testing serve both languages and projects of one language reference
/// projects of the other.
/// </summary>
public sealed partial class RoslynWorkspaceHost
{
    private IReadOnlyList<MetadataReference> _visualBasicReferences = [];

    /// <summary>
    /// Sets the assemblies every Visual Basic project compiles against beside the framework: TabStudio's
    /// <c>Microsoft.VisualBasic.Forms</c>, with the base classes of <c>My.Application</c>, <c>My.Computer</c> and
    /// <c>My.User</c> and the Windows Forms application framework. Projects already loaded get them too.
    /// </summary>
    /// <param name="references">The references; empty to compile against the framework only.</param>
    public void SetVisualBasicReferences(IReadOnlyList<MetadataReference> references)
    {
        ArgumentNullException.ThrowIfNull(references);
        lock (_gate)
        {
            _visualBasicReferences = references;
            foreach (var project in _projects.Values.Where(p => p.IsVisualBasic).ToList())
            {
                ApplyMetadataReferences(project.FilePath);
            }
        }
    }

    /// <summary>The Visual Basic runtime's references for a Visual Basic project, or none. The caller holds the gate.</summary>
    private IReadOnlyList<MetadataReference> VisualBasicReferencesFor(TsProject project) =>
        project.IsVisualBasic ? _visualBasicReferences : [];

    /// <summary>A Visual Basic project's Roslyn project: its <c>.vb</c> files, its options, imports and root namespace.</summary>
    private RoslynProjectInfo CreateVisualBasicProjectInfo(TsProject project, IReadOnlyList<string> allProjectDirectories, ISourceTextProvider sources)
    {
        var id = _projectIds[project.FilePath];
        var documents = new List<DocumentInfo>();
        foreach (var path in ProjectSourceFiles.Enumerate(project, allProjectDirectories, VisualBasicCompiler.SourceExtension))
        {
            var documentId = DocumentId.CreateNewId(id, path);
            documents.Add(CreateDocumentInfo(documentId, path, ReadSource(sources, path), isGenerated: false));
            _documents[path] = documentId;
        }

        var additionalDocuments = new List<DocumentInfo>();
        foreach (var path in AdditionalFilePaths(project).Where(path => !_additionalDocuments.ContainsKey(path)))
        {
            var documentId = DocumentId.CreateNewId(id, path);
            additionalDocuments.Add(CreateDocumentInfo(documentId, path, ReadSource(sources, path), isGenerated: false));
            _additionalDocuments[path] = documentId;
        }

        if (VisualBasicCompiler.CreateAssemblyAttributesSource(project) is { } assemblyInfo)
        {
            var path = Path.Combine(project.DirectoryPath, "obj", $"{project.Name}.AssemblyInfo.g.vb");
            documents.Add(CreateDocumentInfo(DocumentId.CreateNewId(id, path), path, assemblyInfo, isGenerated: true));
        }

        if (VisualBasicSettings.Applies(project, documents.Where(d => !d.IsGenerated).Select(d => d.FilePath ?? string.Empty)))
        {
            var path = Path.Combine(project.DirectoryPath, "obj", "TabStudio", "System.Configuration.Settings.g.vb");
            documents.Add(CreateDocumentInfo(DocumentId.CreateNewId(id, path), path, VisualBasicSettings.Source, isGenerated: true));
        }

        var projectReferences = CompileReferencesOf(project)
            .Where(reference => _projectIds.ContainsKey(reference) && !_skeletons.ContainsKey(reference))
            .Select(reference => new ProjectReference(_projectIds[reference]));

        // A test project, even an executable one, is loaded as a library by TabStudio's test runner.
        var kind = project.IsExecutable && !project.IsTestProject ? AssemblyKind.ConsoleApplication : AssemblyKind.Library;
        var options = VisualBasicCompiler.CreateCompilationOptions(project, kind, _optimize, _concurrentBuild);
        return RoslynProjectInfo.Create(
            id,
            VersionStamp.Create(),
            project.Name,
            project.GetProperty("AssemblyName") ?? project.Name,
            LanguageNames.VisualBasic,
            filePath: project.FilePath,
            compilationOptions: WithStrongName(WithPathMap(options, project), project),
            parseOptions: options.ParseOptions,
            documents: documents,
            projectReferences: projectReferences,
            metadataReferences: GetMetadataReferences(project),
            analyzerReferences: GetAnalyzerReferences(project),
            additionalDocuments: additionalDocuments)
            .WithAnalyzerConfigDocuments([CreateAnalyzerConfig(id, project, [], [])]);
    }
}
