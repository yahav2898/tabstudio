using System.IO.Enumeration;
using System.Text.Json;

namespace TabStudio.Toolchains.Projects;

/// <summary>npm workspace membership, for sharing the root install while keeping every package runnable.</summary>
public static class NodeWorkspaces
{
    /// <summary>The project that installs a package's dependencies: its workspace root, or the package itself.</summary>
    /// <param name="project">The package.</param>
    /// <param name="projects">All detected projects.</param>
    public static ToolchainProject DependencyOwner(ToolchainProject project, IEnumerable<ToolchainProject> projects)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(projects);
        return project.Toolchain.Id == "node"
            ? projects.Where(p => Contains(p, project)).MinBy(p => p.Directory.Length) ?? project
            : project;
    }

    /// <summary>Whether the root package declares this package as a workspace member.</summary>
    /// <param name="root">A possible workspace root.</param>
    /// <param name="package">A nested package.</param>
    public static bool Contains(ToolchainProject root, ToolchainProject package)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(package);
        if (root.Toolchain.Id != "node" || package.Toolchain.Id != "node"
            || !package.Directory.StartsWith(root.Directory.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            return false;
        }

        var relative = Path.GetRelativePath(root.Directory, package.Directory).Replace('\\', '/').Split('/');
        return Patterns(root).Any(pattern => Matches(pattern.Trim('/').Split('/'), relative));
    }

    private static string[] Patterns(ToolchainProject root)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(root.Directory, "package.json")));
            if (document.RootElement.ValueKind != JsonValueKind.Object || !document.RootElement.TryGetProperty("workspaces", out var workspaces))
            {
                return [];
            }

            if (workspaces.ValueKind == JsonValueKind.Object && workspaces.TryGetProperty("packages", out var packages))
            {
                workspaces = packages;
            }

            return workspaces.ValueKind == JsonValueKind.Array
                ? workspaces.EnumerateArray().Where(p => p.ValueKind == JsonValueKind.String).Select(p => p.GetString()!).ToArray()
                : [];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return [];
        }
    }

    private static bool Matches(ReadOnlySpan<string> pattern, ReadOnlySpan<string> path)
    {
        if (pattern.IsEmpty)
        {
            return path.IsEmpty;
        }

        if (pattern[0] == "**")
        {
            return Matches(pattern[1..], path) || (!path.IsEmpty && Matches(pattern, path[1..]));
        }

        return !path.IsEmpty && FileSystemName.MatchesSimpleExpression(pattern[0], path[0], ignoreCase: false)
            && Matches(pattern[1..], path[1..]);
    }
}
