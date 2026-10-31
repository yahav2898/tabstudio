using TabStudio.App.Services;
using TabStudio.Compilation;
using TabStudio.Core.Workspaces;
using TabStudio.LanguageServers.Hosting;

namespace TabStudio.App.ViewModels;

/// <summary>Creating, renaming and deleting files and folders from the Solution Explorer.</summary>
public sealed partial class WorkspaceViewModel
{
    /// <inheritdoc />
    async Task IExplorerActions.OpenAsync(WorkspaceNode node)
    {
        if (node.IsOpenableFile)
        {
            await OpenDocumentAsync(node.FullPath!);
        }
    }

    /// <inheritdoc />
    async Task IExplorerActions.NewFileAsync(WorkspaceNode node)
    {
        if (IsRemoteWorkspace) { StatusText = "Create remote files in the SSH Terminal, then Refresh in Remote Explorer."; return; }
        if (DirectoryOf(node) is not { } folder || Workspace is not { } workspace)
        {
            return;
        }

        var project = node.Project ?? workspace.FindProjectForFile(Path.Combine(folder, "_"));
        var hint = project?.Language == ProjectLanguage.CSharp ? "A name such as Order or Order.cs creates a C# class."
            : project?.IsVisualBasic == true ? "A name such as Order or Order.vb creates a Visual Basic class."
            : "Include the extension, such as notes.md.";
        var typed = await Prompt.AskAsync("New File", $"In {DisplayFolder(folder)}. {hint}", "Create");
        if (typed is null || !IsValidName(typed))
        {
            ReportInvalidName(typed);
            return;
        }

        var path = Path.Combine(folder, FileTemplates.FileNameFor(typed, project));
        if (File.Exists(path) || Directory.Exists(path))
        {
            StatusText = $"'{Path.GetFileName(path)}' already exists.";
            return;
        }

        try
        {
            await File.WriteAllTextAsync(path, FileTemplates.ContentFor(path, project));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Storage full or a read-only folder: said in the status bar instead of ending the app.
            StatusText = $"Could not create {Path.GetFileName(path)}: {ex.Message.TrimEnd('.')}.";
            return;
        }

        NotifyLanguageServersOfFiles((path, FileChangeKind.Created));
        await ReloadWorkspaceAsync();
        await OpenDocumentAsync(path);
        Explorer.Reveal(path);
        StatusText = $"Created {Path.GetFileName(path)}";
    }

    /// <inheritdoc />
    async Task IExplorerActions.NewFolderAsync(WorkspaceNode node)
    {
        if (IsRemoteWorkspace) { StatusText = "Create remote folders in the SSH Terminal, then Refresh in Remote Explorer."; return; }
        if (DirectoryOf(node) is not { } folder)
        {
            return;
        }

        var typed = await Prompt.AskAsync("New Folder", $"In {DisplayFolder(folder)}.", "Create");
        if (typed is null || !IsValidName(typed))
        {
            ReportInvalidName(typed);
            return;
        }

        var path = Path.Combine(folder, typed);
        if (File.Exists(path) || Directory.Exists(path))
        {
            StatusText = $"'{typed}' already exists.";
            return;
        }

        try
        {
            Directory.CreateDirectory(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            StatusText = $"Could not create the folder {typed}: {ex.Message.TrimEnd('.')}.";
            return;
        }

        await ReloadWorkspaceAsync();
        StatusText = $"Created folder {typed}";
    }

    /// <inheritdoc />
    async Task IExplorerActions.RenameAsync(WorkspaceNode node)
    {
        if (IsRemoteWorkspace) { StatusText = "Rename remote files in the SSH Terminal, then Refresh in Remote Explorer."; return; }
        if (node.FullPath is not { } path)
        {
            return;
        }

        var typed = await Prompt.AskAsync("Rename", $"Rename '{node.Name}' to:", "Rename", node.Name);
        if (typed is null || typed == node.Name)
        {
            return;
        }

        if (!IsValidName(typed))
        {
            ReportInvalidName(typed);
            return;
        }

        var target = Path.Combine(Path.GetDirectoryName(path)!, typed);
        if (File.Exists(target) || Directory.Exists(target))
        {
            StatusText = $"'{typed}' already exists.";
            return;
        }

        // Open documents inside what is renamed are saved and reopened at their new location. One that cannot be
        // saved stops the rename: it was closed anyway, and its edits were lost.
        foreach (var dirty in DocumentsUnder(path).Where(d => d.IsDirty))
        {
            if (!await SaveDocumentAsync(dirty))
            {
                StatusText = $"'{node.Name}' was not renamed, because {dirty.FileName} could not be saved.";
                return;
            }
        }

        var affected = await CloseDocumentsUnderAsync(path, save: false);
        try
        {
            if (node.Kind == WorkspaceNodeKind.Folder)
            {
                Directory.Move(path, target);
            }
            else
            {
                File.Move(path, target);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            foreach (var document in affected)
            {
                await OpenDocumentAsync(document);
            }

            StatusText = $"Could not rename '{node.Name}': {ex.Message.TrimEnd('.')}.";
            return;
        }

        NotifyLanguageServersOfFiles((path, FileChangeKind.Deleted), (target, FileChangeKind.Created));
        await ReloadWorkspaceAsync();
        foreach (var document in affected)
        {
            await OpenDocumentAsync(target + document[path.Length..]);
        }

        StatusText = $"Renamed to {typed}";
    }

    /// <inheritdoc />
    async Task IExplorerActions.DeleteAsync(WorkspaceNode node)
    {
        if (IsRemoteWorkspace) { StatusText = "Delete remote files in the SSH Terminal, then Refresh in Remote Explorer."; return; }
        if (node.FullPath is not { } path)
        {
            return;
        }

        var confirmed = await Prompt.ConfirmAsync(
            $"Delete {node.Name}?",
            "It moves to TabStudio's trash, so you can still get it back. Open documents inside it are closed without saving.",
            "Delete",
            destructive: true);
        if (!confirmed)
        {
            return;
        }

        await CloseDocumentsUnderAsync(path, save: false);
        try
        {
            var trash = Path.Combine(_paths.Trash, DateTime.Now.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture));
            Directory.CreateDirectory(trash);
            var destination = Path.Combine(trash, node.Name);

            // Off the UI thread: the trash is in the app's own storage, so a folder in shared storage is copied there,
            // and a node_modules or bin folder froze the IDE until Android offered to close it.
            StatusText = $"Deleting {node.Name}";
            var folder = node.Kind == WorkspaceNodeKind.Folder;
            await Task.Run(() =>
            {
                if (folder)
                {
                    MoveDirectory(path, destination);
                }
                else
                {
                    File.Move(path, destination);
                }
            });
            StatusText = $"Deleted {node.Name}";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            await ReloadWorkspaceAsync();
            StatusText = $"Could not delete {node.Name}: {ex.Message.TrimEnd('.')}.";
            return;
        }

        NotifyLanguageServersOfFiles((path, FileChangeKind.Deleted));
        await ReloadWorkspaceAsync();
        StatusText = $"Moved {node.Name} to TabStudio's trash";
    }

    /// <inheritdoc />
    void IExplorerActions.SetAsStartupProject(WorkspaceNode node)
    {
        if (node.Project is { } project)
        {
            SetStartupProject(Workspace?.Projects.FirstOrDefault(p => p.FilePath == project.FilePath) ?? project);
            StatusText = $"{project.Name} is the startup project";
        }
    }

    /// <summary>
    /// Reads the workspace again after files changed on disk: the Solution Explorer, the language service
    /// (with the unsaved text of open documents) and the Compatibility Doctor all follow.
    /// </summary>
    public Task ReloadWorkspaceAsync() => ReloadWorkspaceAsync(null);

    private async Task ReloadWorkspaceAsync(string? inspectPath)
    {
        if (Workspace is not { } current)
        {
            return;
        }

        var path = inspectPath ?? current.Solution?.FilePath ?? current.RootDirectory;
        var preview = await WorkspaceInspector.InspectAsync(path);
        Workspace = preview.Workspace;
        Explorer.Load(preview.Workspace, preview.Reports);

        Compatibility.Clear();
        foreach (var report in preview.Reports)
        {
            Compatibility.Add(new CompatibilityRowViewModel(report));
        }

        DatabaseExplorer.EfCore.LoadProjects(preview.Workspace.Projects);
        StartDatabaseCheck(preview.Workspace);
        ShowNetFrameworkDiagnostics(preview.Workspace);
        var startup = StartupProject?.FilePath;
        LoadStartupProjects(preview.Workspace, startup);
        await LoadToolchainProjectsAsync(preview.Workspace, startup);

        if (_language is { } language)
        {
            var openTexts = Documents.ToDictionary(d => d.FilePath, d => d.Document.Text, StringComparer.Ordinal);
            await Task.Run(() => language.Reload(preview.Workspace, new OpenDocumentTextProvider(openTexts)));
            foreach (var document in Documents)
            {
                _ = document.AnalyzeAgainAsync();
            }
        }
    }

    private List<DocumentViewModel> DocumentsUnder(string path)
    {
        var prefix = path.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return [.. Documents.Where(d => d.FilePath == path || d.FilePath.StartsWith(prefix, StringComparison.Ordinal))];
    }

    private async Task<List<string>> CloseDocumentsUnderAsync(string path, bool save)
    {
        var affected = DocumentsUnder(path);

        foreach (var document in affected)
        {
            if (save && document.IsDirty)
            {
                await SaveDocumentAsync(document);
            }

            Documents.Remove(document);
            Detach(document);
            document.Dispose();
        }

        if (ActiveDocument is not null && !Documents.Contains(ActiveDocument))
        {
            ActiveDocument = Documents.FirstOrDefault();
        }

        return affected.Select(d => d.FilePath).ToList();
    }

    private static string? DirectoryOf(WorkspaceNode node) => node.Kind switch
    {
        WorkspaceNodeKind.Folder or WorkspaceNodeKind.Root => node.FullPath,
        WorkspaceNodeKind.Project or WorkspaceNodeKind.Solution => node.FullPath is { } file ? Path.GetDirectoryName(file) : null,
        _ => null,
    };

    private string DisplayFolder(string folder) =>
        Workspace is { } workspace && folder.StartsWith(workspace.RootDirectory, StringComparison.Ordinal)
            ? (Path.GetRelativePath(workspace.RootDirectory, folder) is var relative && relative != "." ? relative : workspace.Name)
            : folder;

    private static bool IsValidName(string name) =>
        name.Length > 0
        && name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0
        && name.IndexOfAny(['/', '\\', ':', '*', '?', '"', '<', '>', '|']) < 0
        && name is not "." and not "..";

    private void ReportInvalidName(string? name)
    {
        if (name is not null)
        {
            StatusText = $"'{name}' is not a valid name.";
        }
    }

    private static void MoveDirectory(string source, string destination)
    {
        try
        {
            Directory.Move(source, destination);
        }
        catch (IOException)
        {
            // Directory.Move cannot cross volumes; copy and delete instead. Links are copied as links: followed, a
            // link back to a folder above was copied over and over until the storage was full.
            CopyDirectory(source, destination);
            Directory.Delete(source, recursive: true);
        }
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var entry in new DirectoryInfo(source).EnumerateFileSystemInfos())
        {
            var target = Path.Combine(destination, entry.Name);
            if (entry.LinkTarget is { } link)
            {
                if (entry is DirectoryInfo)
                {
                    Directory.CreateSymbolicLink(target, link);
                }
                else
                {
                    File.CreateSymbolicLink(target, link);
                }
            }
            else if (entry is DirectoryInfo)
            {
                CopyDirectory(entry.FullName, target);
            }
            else
            {
                File.Copy(entry.FullName, target);
            }
        }
    }

    /// <summary>Serves the editor's text for open documents and the disk for everything else.</summary>
    private sealed class OpenDocumentTextProvider(Dictionary<string, string> openTexts) : ISourceTextProvider
    {
        public string GetText(string filePath) =>
            openTexts.TryGetValue(filePath, out var text) ? text : FileSystemTextProvider.Instance.GetText(filePath);
    }
}
