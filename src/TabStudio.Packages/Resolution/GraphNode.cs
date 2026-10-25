using NuGet.Versioning;

namespace TabStudio.Packages.Resolution;

/// <summary>What the resolver has decided about a node of the dependency graph.</summary>
internal enum Disposition
{
    /// <summary>Not decided yet.</summary>
    Acceptable,

    /// <summary>Lost to another version of the same package.</summary>
    Rejected,

    /// <summary>Part of the final graph.</summary>
    Accepted,

    /// <summary>A nearer node names the same package with a lower version; kept to report a downgrade.</summary>
    PotentiallyDowngraded,

    /// <summary>The package depends on itself through this node.</summary>
    Cycle,
}

/// <summary>
/// One path to a package in the dependency graph: a dependency, as declared by its parent, and the package it resolved to.
/// </summary>
/// <remarks>The same package can appear in many nodes, once for every path that leads to it.</remarks>
/// <param name="id">The id of the dependency, as the parent declares it.</param>
/// <param name="range">The versions the parent accepts.</param>
/// <param name="parent">The node that declares the dependency, or <see langword="null"/> for the project itself.</param>
internal sealed class GraphNode(string id, VersionRange range, GraphNode? parent)
{
    /// <summary>The id of the dependency, as the parent declares it.</summary>
    public string Id { get; } = id;

    /// <summary>The versions the parent accepts.</summary>
    public VersionRange Range { get; } = range;

    /// <summary>The node that declares this dependency, or <see langword="null"/> for the project.</summary>
    public GraphNode? Parent { get; } = parent;

    /// <summary>The dependencies of the resolved package, as nodes.</summary>
    public List<GraphNode> Children { get; } = [];

    /// <summary>The package the dependency resolved to. Not set for cycles and potential downgrades, which are never resolved.</summary>
    public PackageMatch? Match { get; set; }

    /// <summary>What the resolver has decided about this node.</summary>
    public Disposition Disposition { get; set; } = Disposition.Acceptable;

    /// <summary>Whether this is the project itself.</summary>
    public bool IsRoot => Parent is null;

    /// <summary>The resolved package. Only valid for nodes that were resolved.</summary>
    /// <exception cref="InvalidOperationException">The node was never resolved.</exception>
    public PackageMatch ResolvedMatch => Match ?? throw new InvalidOperationException($"The dependency on {Id} was not resolved.");

    /// <summary>Describes the node with its resolved version, such as <c>Newtonsoft.Json 13.0.3</c>.</summary>
    public string DescribeVersion()
    {
        if (IsRoot)
        {
            return Id;
        }

        return Match is { IsUnresolved: false } match ? $"{match.Id} {match.Version.ToNormalizedString()}" : DescribeRange();
    }

    /// <summary>Describes the node with its requested range, such as <c>Newtonsoft.Json (&gt;= 13.0.1)</c>.</summary>
    public string DescribeRange() => IsRoot ? Id : $"{Id} {Range.PrettyPrint()}";

    /// <summary>The path from the project to this node, such as <c>App -&gt; A 1.0.0 -&gt; B (&gt;= 2.0.0)</c>.</summary>
    public string DescribePath()
    {
        var parts = new Stack<string>();
        parts.Push(DescribeRange());
        for (var node = Parent; node is not null; node = node.Parent)
        {
            parts.Push(node.DescribeVersion());
        }

        return string.Join(" -> ", parts);
    }
}
