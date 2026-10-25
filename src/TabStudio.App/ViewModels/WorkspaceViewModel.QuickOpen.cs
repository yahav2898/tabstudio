using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.Core.Workspaces;

namespace TabStudio.App.ViewModels;

/// <summary>A file offered by the search box in the menu bar.</summary>
/// <param name="Path">Full path of the file.</param>
/// <param name="Name">The file name.</param>
/// <param name="Folder">The folder, relative to the workspace root.</param>
public sealed record QuickOpenItem(string Path, string Name, string Folder)
{
    /// <summary>For a symbol, the one-based line it is on; <see langword="null"/> for a file.</summary>
    public int? Line { get; init; }

    /// <summary>For a symbol, the one-based column it is at.</summary>
    public int Column { get; init; } = 1;
}

/// <summary>The search box in the menu bar: type part of a file name, tap a result to open it.</summary>
public sealed partial class WorkspaceViewModel
{
    private const int QuickOpenLimit = 20;

    /// <summary>What the user typed in the search box.</summary>
    [ObservableProperty]
    public partial string QuickOpenText { get; set; } = string.Empty;

    /// <summary>Whether the results list is open.</summary>
    [ObservableProperty]
    public partial bool IsQuickOpenOpen { get; set; }

    /// <summary>The matching files, best matches first.</summary>
    public ObservableCollection<QuickOpenItem> QuickOpenResults { get; } = [];

    /// <summary>The path segments of the active document, for the breadcrumb under the tabs.</summary>
    public IReadOnlyList<string> Breadcrumb => ActiveDocument is { IsExtensionsPage: false } document && Workspace is { } workspace
        ? BuildBreadcrumb(workspace, document.FilePath)
        : [];

    [RelayCommand]
    private async Task OpenQuickOpenItemAsync(QuickOpenItem? item)
    {
        item ??= QuickOpenResults.FirstOrDefault();
        if (item is null)
        {
            return;
        }

        QuickOpenText = string.Empty;
        IsQuickOpenOpen = false;
        if (item.Line is { } line)
        {
            await NavigateToAsync(item.Path, line, item.Column);
        }
        else
        {
            await OpenDocumentAsync(item.Path);
        }
    }

    partial void OnQuickOpenTextChanged(string value)
    {
        QuickOpenResults.Clear();
        var query = value.Trim();
        if (query.Length == 0 || Workspace is not { } workspace)
        {
            IsQuickOpenOpen = false;
            return;
        }

        var matches = workspace.Tree.DescendantsAndSelf()
            .Where(n => n.IsOpenableFile && n.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
            .OrderBy(n => n.Name.StartsWith(query, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(n => n.Name.Length)
            .ThenBy(n => n.Name, StringComparer.OrdinalIgnoreCase)
            .Take(QuickOpenLimit);

        foreach (var node in matches)
        {
            var folder = Path.GetRelativePath(workspace.RootDirectory, Path.GetDirectoryName(node.FullPath!)!);
            QuickOpenResults.Add(new QuickOpenItem(node.FullPath!, node.Name, folder == "." ? string.Empty : folder));
        }

        IsQuickOpenOpen = QuickOpenResults.Count > 0;
    }

    private static List<string> BuildBreadcrumb(Workspace workspace, string filePath)
    {
        var project = workspace.FindProjectForFile(filePath);
        var root = project?.DirectoryPath ?? workspace.RootDirectory;
        var relative = Path.GetRelativePath(root, filePath);
        var segments = new List<string>();
        if (project is not null)
        {
            segments.Add(project.Name);
        }

        segments.AddRange(relative.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries));
        return segments;
    }
}
