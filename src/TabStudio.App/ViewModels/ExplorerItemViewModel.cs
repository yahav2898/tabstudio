using CommunityToolkit.Mvvm.ComponentModel;
using TabStudio.App.Editing;
using TabStudio.Core.Compatibility;
using TabStudio.Core.Workspaces;

namespace TabStudio.App.ViewModels;

/// <summary>The icon of a Solution Explorer row.</summary>
public enum ExplorerIcon
{
    /// <summary>The solution.</summary>
    Solution,

    /// <summary>A solution folder or a plain folder.</summary>
    Folder,

    /// <summary>A project that produces a program.</summary>
    AppProject,

    /// <summary>A class library.</summary>
    LibraryProject,

    /// <summary>A test project.</summary>
    TestProject,

    /// <summary>The Dependencies node of a project.</summary>
    Dependencies,

    /// <summary>A NuGet package.</summary>
    Package,

    /// <summary>A reference to another project.</summary>
    ProjectReference,

    /// <summary>A file, shown with a badge instead of an icon.</summary>
    File,
}

/// <summary>
/// One visible row of the Solution Explorer. The tree is shown as a flat, virtualized list of rows, which
/// keeps scrolling smooth on a tablet even for repositories with thousands of files.
/// </summary>
public sealed partial class ExplorerItemViewModel : ObservableObject
{
    private const double IndentPerLevel = 16;

    /// <summary>Creates a row for a node.</summary>
    /// <param name="node">The workspace node.</param>
    /// <param name="depth">How deep it is in the tree; the root is 0.</param>
    /// <param name="report">The Compatibility Doctor's verdict, for project rows.</param>
    public ExplorerItemViewModel(WorkspaceNode node, int depth, CompatibilityReport? report = null)
    {
        ArgumentNullException.ThrowIfNull(node);
        Node = node;
        Depth = depth;
        Report = report;
        Icon = IconFor(node);
        if (node.Kind == WorkspaceNodeKind.File)
        {
            var type = FileTypes.For(node.Name);
            Badge = type.Badge;
            BadgeKind = type.BadgeKind;
        }
    }

    /// <summary>The workspace node this row shows.</summary>
    public WorkspaceNode Node { get; }

    /// <summary>How deep the row is in the tree.</summary>
    public int Depth { get; }

    /// <summary>The Compatibility Doctor's verdict, for project rows.</summary>
    public CompatibilityReport? Report { get; }

    /// <summary>The row's icon.</summary>
    public ExplorerIcon Icon { get; }

    /// <summary>The badge text for files, such as "C#".</summary>
    public string? Badge { get; }

    /// <summary>How the badge is colored.</summary>
    public BadgeKind BadgeKind { get; }

    /// <summary>The name shown.</summary>
    public string Name => Node.Name;

    /// <summary>Extra text after the name, such as "(2 projects)".</summary>
    public string? Detail { get; init; }

    /// <summary>A text shown when searching, such as the folder the file is in.</summary>
    public string? Location { get; init; }

    /// <summary>The left indentation, in pixels.</summary>
    public double IndentWidth => 8 + (Depth * IndentPerLevel);

    /// <summary>Whether the row can be expanded.</summary>
    public bool IsExpandable => Node.Children.Count > 0;

    /// <summary>Whether the row's children are shown.</summary>
    [ObservableProperty]
    public partial bool IsExpanded { get; set; }

    /// <summary>The run host label on project rows, such as "In-process".</summary>
    public string? Pill => Report?.Status switch
    {
        null => null,
        CompatibilityStatus.Ready => Report.RunsVia,
        CompatibilityStatus.NotRunnable => null,
        _ => CompatibilityStatusNames.For(Report.Status),
    };

    /// <summary>Whether the pill marks a project that runs on this device.</summary>
    public bool IsPillReady => Report?.Status == CompatibilityStatus.Ready;

    /// <summary>Whether this row is a project.</summary>
    public bool IsProject => Node.Kind == WorkspaceNodeKind.Project;

    /// <summary>Whether this row is a file.</summary>
    public bool IsFile => Node.IsOpenableFile;

    private static ExplorerIcon IconFor(WorkspaceNode node) => node.Kind switch
    {
        WorkspaceNodeKind.Solution or WorkspaceNodeKind.Root => ExplorerIcon.Solution,
        WorkspaceNodeKind.SolutionFolder or WorkspaceNodeKind.Folder => ExplorerIcon.Folder,
        WorkspaceNodeKind.Project when node.Project is { IsTestProject: true } => ExplorerIcon.TestProject,
        WorkspaceNodeKind.Project when node.Project is { IsExecutable: true } => ExplorerIcon.AppProject,
        WorkspaceNodeKind.Project => ExplorerIcon.LibraryProject,
        WorkspaceNodeKind.Dependencies => ExplorerIcon.Dependencies,
        WorkspaceNodeKind.Package => ExplorerIcon.Package,
        WorkspaceNodeKind.ProjectReference => ExplorerIcon.ProjectReference,
        _ => ExplorerIcon.File,
    };
}
