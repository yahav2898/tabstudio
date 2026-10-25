using TabStudio.Core.Workspaces;

namespace TabStudio.App.ViewModels;

/// <summary>Source control glyphs on the Solution Explorer's rows.</summary>
public sealed partial class SolutionExplorerViewModel
{
    private IReadOnlyDictionary<string, GitFileStatus> _gitStatus = new Dictionary<string, GitFileStatus>(StringComparer.Ordinal);
    private HashSet<string> _foldersWithChanges = new(StringComparer.Ordinal);

    /// <summary>Shows which files changed since the last sync with GitHub.</summary>
    /// <param name="files">The status of each changed file, by full path.</param>
    public void SetSourceControlStatus(IReadOnlyDictionary<string, GitFileStatus> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        _gitStatus = files;
        _foldersWithChanges = new HashSet<string>(StringComparer.Ordinal);
        foreach (var path in files.Keys)
        {
            // Every folder above a changed file contains changes; stop at one already known.
            var folder = Path.GetDirectoryName(path);
            while (!string.IsNullOrEmpty(folder) && _foldersWithChanges.Add(folder))
            {
                folder = Path.GetDirectoryName(folder);
            }
        }

        ApplySourceControlStatus(Rows);
    }

    /// <summary>Gives each row its glyph. Called whenever rows are shown.</summary>
    private void ApplySourceControlStatus(IEnumerable<ExplorerItemViewModel> rows)
    {
        foreach (var row in rows)
        {
            row.GitGlyph = GlyphFor(row.Node);
        }
    }

    private ExplorerGitGlyph GlyphFor(WorkspaceNode node)
    {
        if (node.FullPath is not { } path)
        {
            return ExplorerGitGlyph.None;
        }

        // A project file stands for its folder too: its own change wins, then changes inside.
        if (node.Kind is WorkspaceNodeKind.File or WorkspaceNodeKind.Project or WorkspaceNodeKind.Solution
            && _gitStatus.TryGetValue(path, out var status))
        {
            return status switch
            {
                GitFileStatus.Added => ExplorerGitGlyph.Added,
                GitFileStatus.Conflict => ExplorerGitGlyph.Conflict,
                _ => ExplorerGitGlyph.Modified,
            };
        }

        var folder = node.Kind switch
        {
            WorkspaceNodeKind.Folder or WorkspaceNodeKind.Root => path,
            WorkspaceNodeKind.Project or WorkspaceNodeKind.Solution => Path.GetDirectoryName(path),
            _ => null,
        };
        return folder is not null && _foldersWithChanges.Contains(folder.TrimEnd(Path.DirectorySeparatorChar))
            ? ExplorerGitGlyph.ContainsChanges
            : ExplorerGitGlyph.None;
    }
}
