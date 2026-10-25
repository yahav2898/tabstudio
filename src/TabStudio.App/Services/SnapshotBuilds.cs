using TabStudio.Compilation;
using TabStudio.Compilation.Building;
using TabStudio.Link.Language;

namespace TabStudio.App.Services;

/// <summary>Builds the test projects of a snapshot of the workspace, for the agent, on whichever device has the workspace.</summary>
internal static class SnapshotBuilds
{
    /// <summary>Builds every test project of the workspace with changes that are not applied yet.</summary>
    /// <param name="host">The Roslyn host that has the workspace.</param>
    /// <param name="changes">Full path to new text, or <see langword="null"/> for a deleted file.</param>
    /// <param name="cancellationToken">Cancels the builds.</param>
    public static async Task<IReadOnlyList<LinkProjectBuild>> BuildTestProjectsAsync(RoslynWorkspaceHost host, IReadOnlyDictionary<string, string?> changes, CancellationToken cancellationToken)
    {
        var snapshot = host.CreateSnapshot(changes);
        var builder = new ProjectBuilder(snapshot);
        var builds = new List<LinkProjectBuild>();
        foreach (var project in snapshot.Projects.Where(p => p.IsTestProject))
        {
            builds.Add(new LinkProjectBuild(project.FilePath, await builder.BuildAsync(project, cancellationToken).ConfigureAwait(false)));
        }

        return builds;
    }
}
