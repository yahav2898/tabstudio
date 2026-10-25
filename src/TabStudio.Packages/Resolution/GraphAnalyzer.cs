using NuGet.Versioning;

namespace TabStudio.Packages.Resolution;

/// <summary>A downgrade: a nearer dependency selected a lower version than a farther one asked for.</summary>
/// <param name="DowngradedFrom">The farther dependency, which asked for more.</param>
/// <param name="DowngradedBy">The nearer dependency, whose lower version won.</param>
internal sealed record Downgrade(GraphNode DowngradedFrom, GraphNode DowngradedBy);

/// <summary>Two dependencies on one package that the selected version cannot both satisfy.</summary>
/// <param name="Selected">The dependency whose version was selected.</param>
/// <param name="Conflicting">A dependency whose range does not include that version.</param>
internal sealed record VersionConflict(GraphNode Selected, GraphNode Conflicting);

/// <summary>
/// Decides which nodes of a walked dependency graph make it into the final graph, with NuGet's rules.
/// </summary>
/// <remarks>
/// <para>This is a port of the analysis in NuGet's <c>GraphOperations</c>:</para>
/// <list type="number">
///   <item><b>Cycles</b> are removed and reported.</item>
///   <item><b>Nearest wins.</b> The walk already left out dependencies that a nearer one eclipses. Those it kept
///   because the nearer one asks for a lower version are checked here: if the version the nearer one resolved
///   to is outside the farther range, that is a downgrade.</item>
///   <item><b>Cousins.</b> When different paths reach different versions of a package, the highest of those
///   versions wins (each is the lowest its own range allows, so the highest is the lowest that can satisfy all
///   of them), and the losing nodes are rejected along with everything below them. This repeats until every
///   node is decided.</item>
///   <item><b>Conflicts.</b> An accepted dependency whose range does not include the selected version is a conflict.</item>
/// </list>
/// </remarks>
internal static class GraphAnalyzer
{
    private const int MaxRounds = 1000;

    private enum WalkState
    {
        Walking,
        Rejected,
        Ambiguous,
    }

    /// <summary>Removes cycles and potential downgrades from the graph.</summary>
    /// <param name="root">The project node.</param>
    /// <param name="cycles">Receives the nodes that close a cycle.</param>
    /// <returns>The candidate downgrades; see <see cref="RelevantDowngrades"/>.</returns>
    public static List<Downgrade> RemoveCyclesAndDowngrades(GraphNode root, List<GraphNode> cycles)
    {
        var downgrades = new Dictionary<GraphNode, GraphNode>();
        foreach (var node in BreadthFirst(root))
        {
            if (node.Disposition == Disposition.Cycle)
            {
                cycles.Add(node);
                node.Parent?.Children.Remove(node);
                continue;
            }

            if (node.Disposition != Disposition.PotentiallyDowngraded)
            {
                continue;
            }

            for (var ancestor = node.Parent; ancestor is not null; ancestor = ancestor.Parent)
            {
                foreach (var side in ancestor.Children)
                {
                    if (side == node || !NuGetNames.SameId(side.Id, node.Id))
                    {
                        continue;
                    }

                    if (VersionRangeOrdering.IsGreaterThanOrEqualTo(side.Range, node.Range))
                    {
                        downgrades.Remove(node);
                        continue;
                    }

                    // The nearer range starts lower, but it may still have resolved into the farther range.
                    if (side.Match is { IsUnresolved: false } match && node.Range.Satisfies(match.Version))
                    {
                        continue;
                    }

                    downgrades[node] = side;
                }
            }

            node.Parent?.Children.Remove(node);
        }

        return downgrades.Select(d => new Downgrade(d.Key, d.Value)).ToList();
    }

    /// <summary>Accepts or rejects every remaining node, resolving differences between cousins.</summary>
    /// <param name="root">The project node.</param>
    /// <returns>The accepted node for each package id, including the project itself.</returns>
    public static Dictionary<string, GraphNode> ResolveCousins(GraphNode root)
    {
        var accepted = new Dictionary<string, GraphNode>(StringComparer.OrdinalIgnoreCase);
        var rounds = 0;
        var incomplete = true;
        while (incomplete && ++rounds < MaxRounds)
        {
            var tracker = new Tracker();

            // Reject everything below a rejected node, and note which versions of each package are still in play.
            Visit(root, true, (node, parentAlive) =>
            {
                if (!parentAlive || node.Disposition == Disposition.Rejected)
                {
                    node.Disposition = Disposition.Rejected;
                    return false;
                }

                tracker.Track(node.ResolvedMatch);
                return true;
            });

            // Below a package that is still disputed, nothing can be decided yet.
            Visit(root, WalkState.Walking, (node, state) =>
            {
                if (node.Disposition == Disposition.Rejected)
                {
                    return WalkState.Rejected;
                }

                if (state == WalkState.Walking && tracker.IsDisputed(node.ResolvedMatch))
                {
                    return WalkState.Ambiguous;
                }

                if (state == WalkState.Ambiguous)
                {
                    tracker.MarkAmbiguous(node.ResolvedMatch);
                }

                return state;
            });

            // Decide every undisputed node: the highest version in play wins.
            Visit(root, true, (node, parentAccepted) =>
            {
                if (!parentAccepted || node.Disposition == Disposition.Rejected || tracker.IsAmbiguous(node.ResolvedMatch))
                {
                    return false;
                }

                if (node.Disposition == Disposition.Acceptable)
                {
                    if (tracker.IsBestVersion(node.ResolvedMatch))
                    {
                        node.Disposition = Disposition.Accepted;
                        accepted[node.Id] = node;
                    }
                    else
                    {
                        node.Disposition = Disposition.Rejected;
                    }
                }

                return node.Disposition == Disposition.Accepted;
            });

            incomplete = BreadthFirst(root).Any(n => n.Disposition == Disposition.Acceptable);
        }

        return accepted;
    }

    /// <summary>Finds accepted dependencies whose range does not include the version that was selected.</summary>
    /// <param name="root">The project node.</param>
    /// <param name="accepted">The accepted node for each package id.</param>
    public static List<VersionConflict> FindConflicts(GraphNode root, Dictionary<string, GraphNode> accepted)
    {
        var conflicts = new List<VersionConflict>();
        foreach (var node in BreadthFirst(root).Where(n => n.Disposition == Disposition.Accepted))
        {
            foreach (var child in node.Children)
            {
                if (accepted.TryGetValue(child.Id, out var selected) &&
                    selected != child &&
                    selected.Match is { IsUnresolved: false } match &&
                    !child.Range.Satisfies(match.Version))
                {
                    conflicts.Add(new VersionConflict(selected, child));
                }
            }
        }

        return conflicts;
    }

    /// <summary>
    /// Keeps the downgrades that matter: the lower version really was selected, and the node that asked for more
    /// was on an accepted path.
    /// </summary>
    public static IEnumerable<Downgrade> RelevantDowngrades(IEnumerable<Downgrade> downgrades) =>
        downgrades.Where(d => d.DowngradedBy.Disposition == Disposition.Accepted && AncestorsAccepted(d.DowngradedFrom));

    /// <summary>Every node of the graph, breadth first.</summary>
    public static List<GraphNode> BreadthFirst(GraphNode root)
    {
        var nodes = new List<GraphNode>();
        var queue = new Queue<GraphNode>();
        queue.Enqueue(root);
        while (queue.TryDequeue(out var node))
        {
            nodes.Add(node);
            foreach (var child in node.Children)
            {
                queue.Enqueue(child);
            }
        }

        return nodes;
    }

    private static bool AncestorsAccepted(GraphNode node)
    {
        for (var ancestor = node.Parent; ancestor is not null; ancestor = ancestor.Parent)
        {
            if (ancestor.Disposition != Disposition.Accepted)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Visits the graph breadth first, handing each node the state its parent's visit returned.</summary>
    private static void Visit<TState>(GraphNode root, TState initial, Func<GraphNode, TState, TState> visitor)
    {
        var queue = new Queue<(GraphNode Node, TState State)>();
        queue.Enqueue((root, initial));
        while (queue.TryDequeue(out var item))
        {
            var state = visitor(item.Node, item.State);
            foreach (var child in item.Node.Children)
            {
                queue.Enqueue((child, state));
            }
        }
    }

    /// <summary>The versions of each package still in play during one round of <see cref="ResolveCousins"/>.</summary>
    private sealed class Tracker
    {
        private readonly Dictionary<string, Entry> _entries = new(StringComparer.OrdinalIgnoreCase);

        public void Track(PackageMatch match) => GetEntry(match.Id).Versions.Add(match.Version);

        public bool IsDisputed(PackageMatch match) => _entries.TryGetValue(match.Id, out var entry) && entry.Versions.Count > 1;

        public bool IsAmbiguous(PackageMatch match) => _entries.TryGetValue(match.Id, out var entry) && entry.Ambiguous;

        public void MarkAmbiguous(PackageMatch match) => GetEntry(match.Id).Ambiguous = true;

        public bool IsBestVersion(PackageMatch match) =>
            !_entries.TryGetValue(match.Id, out var entry) || entry.Versions.All(version => match.Version >= version);

        private Entry GetEntry(string id)
        {
            if (!_entries.TryGetValue(id, out var entry))
            {
                entry = new Entry();
                _entries[id] = entry;
            }

            return entry;
        }

        private sealed class Entry
        {
            public HashSet<NuGetVersion> Versions { get; } = [];

            public bool Ambiguous { get; set; }
        }
    }
}
