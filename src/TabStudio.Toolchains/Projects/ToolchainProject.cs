using TabStudio.Toolchains.Catalog;

namespace TabStudio.Toolchains.Projects;

/// <summary>
/// A folder that is one of a toolchain's projects, such as a Python app (<c>pyproject.toml</c>,
/// <c>requirements.txt</c> or <c>.py</c> files) or a Node.js app (<c>package.json</c>).
/// </summary>
/// <param name="Toolchain">The toolchain that runs it.</param>
/// <param name="Directory">The project folder.</param>
/// <param name="Marker">The file that made it a project, such as <c>package.json</c>, or <see langword="null"/> for a folder of source files.</param>
public sealed record ToolchainProject(ToolchainDefinition Toolchain, string Directory, string? Marker)
{
    /// <summary>The project's name: its folder's name.</summary>
    public string Name => Path.GetFileName(Directory.TrimEnd(Path.DirectorySeparatorChar)) is { Length: > 0 } name ? name : Directory;

    /// <summary>The path of the file that made it a project, if any.</summary>
    public string? MarkerPath => Marker is null ? null : Path.Combine(Directory, Marker);

    /// <summary>Whether a file belongs to the project: it is inside its folder.</summary>
    /// <param name="path">A file.</param>
    public bool Contains(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var folder = Directory.EndsWith(Path.DirectorySeparatorChar) ? Directory : Directory + Path.DirectorySeparatorChar;
        return Path.GetFullPath(path).StartsWith(folder, StringComparison.Ordinal);
    }
}
