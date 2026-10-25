namespace TabStudio.Core.Workspaces;

/// <summary>What a node in the Solution Explorer represents.</summary>
public enum WorkspaceNodeKind
{
    /// <summary>A plain folder opened without a solution.</summary>
    Root,

    /// <summary>The solution itself.</summary>
    Solution,

    /// <summary>A virtual folder declared in the solution file.</summary>
    SolutionFolder,

    /// <summary>A project.</summary>
    Project,

    /// <summary>The virtual "Dependencies" node under a project.</summary>
    Dependencies,

    /// <summary>A NuGet package under "Dependencies".</summary>
    Package,

    /// <summary>A project reference under "Dependencies".</summary>
    ProjectReference,

    /// <summary>A folder on disk.</summary>
    Folder,

    /// <summary>A file on disk.</summary>
    File,
}

/// <summary>
/// One node in the workspace tree shown by the Solution Explorer.
/// </summary>
/// <remarks>
/// The tree is built once by <see cref="WorkspaceLoader"/> and is immutable from the outside.
/// </remarks>
public sealed class WorkspaceNode
{
    private readonly List<WorkspaceNode> _children = [];

    /// <summary>Creates a node.</summary>
    /// <param name="kind">What the node represents.</param>
    /// <param name="name">The text shown in the tree.</param>
    /// <param name="fullPath">The file or folder on disk, for nodes that have one.</param>
    /// <param name="project">The project, for <see cref="WorkspaceNodeKind.Project"/> nodes.</param>
    public WorkspaceNode(WorkspaceNodeKind kind, string name, string? fullPath = null, ProjectInfo? project = null)
    {
        ArgumentNullException.ThrowIfNull(name);
        Kind = kind;
        Name = name;
        FullPath = fullPath;
        Project = project;
    }

    /// <summary>What the node represents.</summary>
    public WorkspaceNodeKind Kind { get; }

    /// <summary>The text shown in the tree.</summary>
    public string Name { get; }

    /// <summary>The file or folder on disk, or <see langword="null"/> for virtual nodes.</summary>
    public string? FullPath { get; }

    /// <summary>The project, for project nodes.</summary>
    public ProjectInfo? Project { get; }

    /// <summary>Child nodes, in display order.</summary>
    public IReadOnlyList<WorkspaceNode> Children => _children;

    /// <summary>Whether the node is a file that can be opened in the editor.</summary>
    public bool IsOpenableFile => Kind == WorkspaceNodeKind.File && FullPath is not null;

    /// <summary>This node and every node below it, depth first.</summary>
    public IEnumerable<WorkspaceNode> DescendantsAndSelf()
    {
        var stack = new Stack<WorkspaceNode>();
        stack.Push(this);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            yield return node;
            for (var i = node._children.Count - 1; i >= 0; i--)
            {
                stack.Push(node._children[i]);
            }
        }
    }

    /// <inheritdoc />
    public override string ToString() => $"{Kind}: {Name}";

    internal WorkspaceNode Add(WorkspaceNode child)
    {
        _children.Add(child);
        return child;
    }
}
