using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using TsProject = TabStudio.Core.Workspaces.ProjectInfo;

namespace TabStudio.Compilation;

// Speculative copies of the workspace, for checking changes before they are applied. The main part of the class
// (RoslynWorkspaceHost.cs) holds the documentation.
public sealed partial class RoslynWorkspaceHost
{
    /// <summary>
    /// A copy of the workspace with changes that are not saved: edited, new and deleted files. It shares
    /// everything unchanged with the live workspace, so creating one is cheap.
    /// </summary>
    /// <param name="changes">Full path to new text, or <see langword="null"/> for a deleted file. New C# files join the project whose folder holds them.</param>
    public WorkspaceSnapshot CreateSnapshot(IReadOnlyDictionary<string, string?> changes)
    {
        ArgumentNullException.ThrowIfNull(changes);
        Solution solution;
        Dictionary<string, ProjectId> projectIds;
        Dictionary<ProjectId, TsProject> projects;
        Dictionary<string, DocumentId> documents;
        lock (_gate)
        {
            solution = _workspace.CurrentSolution;
            projectIds = new Dictionary<string, ProjectId>(_projectIds, StringComparer.Ordinal);
            projects = new Dictionary<ProjectId, TsProject>(_projects);
            documents = new Dictionary<string, DocumentId>(_documents, StringComparer.Ordinal);
        }

        foreach (var (path, text) in changes)
        {
            if (documents.TryGetValue(path, out var documentId) && solution.GetDocument(documentId) is not null)
            {
                solution = text is null
                    ? solution.RemoveDocument(documentId)
                    : solution.WithDocumentText(documentId, SourceText.From(text, Encoding.UTF8), PreservationMode.PreserveIdentity);
            }
            else if (text is not null
                && OwningProject(projects.Values, path) is { } owner
                && path.EndsWith(owner.IsVisualBasic ? ".vb" : ".cs", StringComparison.OrdinalIgnoreCase))
            {
                var id = DocumentId.CreateNewId(projectIds[owner.FilePath], path);
                solution = solution.AddDocument(DocumentInfo.Create(
                    id,
                    Path.GetFileName(path),
                    loader: TextLoader.From(TextAndVersion.Create(SourceText.From(text, Encoding.UTF8), VersionStamp.Create(), path)),
                    filePath: path));
            }
        }

        return new WorkspaceSnapshot(this, solution, projectIds, projects);
    }

    /// <summary>The project whose folder most closely contains a file.</summary>
    private static TsProject? OwningProject(IEnumerable<TsProject> projects, string path) =>
        projects
            .Where(p => path.StartsWith(p.DirectoryPath.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            .OrderByDescending(p => p.DirectoryPath.Length)
            .FirstOrDefault();
}
