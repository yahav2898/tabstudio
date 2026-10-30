using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.App.Editing;
using TabStudio.Core.Compatibility;
using TabStudio.Core.Workspaces;

namespace TabStudio.App.ViewModels;

/// <summary>One row of the Properties panel.</summary>
/// <param name="Name">The property name.</param>
/// <param name="Value">Its value, as text.</param>
/// <param name="IsPath">Whether the value is a path, which trims in the middle on one line, with the whole path in its tooltip.</param>
public sealed record PropertyRow(string Name, string Value, bool IsPath = false)
{
    /// <summary>Whether the value wraps: anything but a path.</summary>
    public bool IsText => !IsPath;
}

/// <summary>
/// The Properties panel: facts about whatever is selected in the Solution Explorer. It folds into its header, which
/// shows the selected item's name, and opens with a tap on it; rows with no value are left out.
/// </summary>
public sealed partial class PropertiesViewModel : ObservableObject
{
    /// <summary>Whether the panel is open; folded into its 40-pixel header by default, so the Solution Explorer has the room.</summary>
    [ObservableProperty]
    public partial bool IsExpanded { get; set; }

    /// <summary>The selected item's name.</summary>
    [ObservableProperty]
    public partial string Title { get; private set; } = string.Empty;

    /// <summary>What kind of item it is, such as "File Properties".</summary>
    [ObservableProperty]
    public partial string Subtitle { get; private set; } = string.Empty;

    /// <summary>The rows.</summary>
    public ObservableCollection<PropertyRow> Rows { get; } = [];

    /// <summary>Shows the properties of a Solution Explorer node.</summary>
    /// <param name="node">The node, or <see langword="null"/> to clear the panel.</param>
    /// <param name="workspace">The workspace the node belongs to.</param>
    /// <param name="otherProjects">Projects the workspace's solution does not list, such as its Python and Node.js projects.</param>
    /// <param name="startupProject">The startup project the user chose, if any; the workspace's default otherwise.</param>
    public void Show(WorkspaceNode? node, Workspace? workspace, IReadOnlyList<ProjectInfo>? otherProjects = null, ProjectInfo? startupProject = null)
    {
        Rows.Clear();
        if (node is null)
        {
            Title = string.Empty;
            Subtitle = string.Empty;
            return;
        }

        Title = node.Name;
        switch (node.Kind)
        {
            case WorkspaceNodeKind.File when node.FullPath is { } path:
                Subtitle = "File Properties";
                var project = workspace?.FindProjectForFile(path);
                Add("Build Action", BuildActionFor(path, project));
                Add("File Type", FileTypes.For(path).DisplayName);
                Add("File Name", Path.GetFileName(path));
                if (TryGetFile(path) is { } info)
                {
                    Add("Size", FormatSize(info.Length));
                    Add("Modified", info.LastWriteTime.ToString("g", CultureInfo.CurrentCulture));
                }

                Add("Project", project?.Name);
                AddPath("Full Path", path);
                break;

            case WorkspaceNodeKind.Project when node.Project is { } projectInfo:
                Subtitle = "Project Properties";
                var report = CompatibilityDoctor.Diagnose(projectInfo);
                Add("Project Type", ProjectKindNames.For(projectInfo.Kind));
                Add("Target Framework", string.Join(", ", projectInfo.TargetFrameworks));
                Add("Output Type", projectInfo.OutputType ?? "Library");
                Add("Runs Via", report.RunsVia);
                Add("Packages", projectInfo.PackageReferences.Count.ToString(CultureInfo.InvariantCulture));
                Add("Project References", projectInfo.ProjectReferences.Count.ToString(CultureInfo.InvariantCulture));
                Add("Nullable", projectInfo.NullableEnabled ? "Enabled" : "Disabled");
                Add("Implicit Usings", projectInfo.ImplicitUsings ? "Enabled" : "Disabled");
                AddPath("Project File", projectInfo.FilePath);
                break;

            case WorkspaceNodeKind.Solution or WorkspaceNodeKind.Root:
                Subtitle = node.Kind == WorkspaceNodeKind.Solution ? "Solution Properties" : "Folder Properties";
                var projects = (workspace?.Projects.Count ?? 0) + (otherProjects?.Count ?? 0);
                Add("Projects", projects > 0 ? projects.ToString(CultureInfo.InvariantCulture) : null);
                Add("Startup Project", startupProject?.Name ?? workspace?.DefaultStartupProject?.Name);
                AddPath("Path", node.FullPath ?? workspace?.RootDirectory);
                break;

            case WorkspaceNodeKind.Folder or WorkspaceNodeKind.SolutionFolder:
                Subtitle = "Folder Properties";
                Add("Folder Name", node.Name);
                AddPath("Full Path", node.FullPath);

                break;

            case WorkspaceNodeKind.Package:
                Subtitle = "Package Properties";
                Add("Package", node.Name);
                break;

            case WorkspaceNodeKind.ProjectReference:
                Subtitle = "Reference Properties";
                Add("Project", node.Name);
                AddPath("Path", node.FullPath);
                break;

            default:
                Subtitle = string.Empty;
                break;
        }
    }

    /// <summary>Folds the panel into its header, or opens it.</summary>
    [RelayCommand]
    private void ToggleExpanded() => IsExpanded = !IsExpanded;

    /// <summary>Adds a row, unless it has no value.</summary>
    private void Add(string name, string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            Rows.Add(new PropertyRow(name, value));
        }
    }

    /// <summary>Adds a path's row, unless there is no path.</summary>
    private void AddPath(string name, string? path)
    {
        if (!string.IsNullOrEmpty(path))
        {
            Rows.Add(new PropertyRow(name, path, IsPath: true));
        }
    }

    private static string BuildActionFor(string path, ProjectInfo? project)
    {
        if (project is null)
        {
            return "None";
        }

        var extension = Path.GetExtension(path);
        if (string.Equals(extension, ".cs", StringComparison.OrdinalIgnoreCase) && project.Language == ProjectLanguage.CSharp)
        {
            return "C# compiler";
        }

        if (string.Equals(extension, ".vb", StringComparison.OrdinalIgnoreCase) && project.IsVisualBasic)
        {
            return "Visual Basic compiler";
        }

        return ProjectParser.IsProjectFile(path) ? "Project file" : "None";
    }

    private static FileInfo? TryGetFile(string path)
    {
        try
        {
            var info = new FileInfo(path);
            return info.Exists ? info : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes.ToString(CultureInfo.InvariantCulture)} bytes",
        < 1024 * 1024 => $"{(bytes / 1024.0).ToString("0.#", CultureInfo.InvariantCulture)} KB",
        _ => $"{(bytes / (1024.0 * 1024)).ToString("0.#", CultureInfo.InvariantCulture)} MB",
    };
}
