using TabStudio.Extensions.Manifests;

namespace TabStudio.Extensions.Installation;

/// <summary>An extension in TabStudio's extensions folder.</summary>
/// <param name="Manifest">Its manifest.</param>
/// <param name="Folder">The folder it was extracted to, which holds its <c>package.json</c>.</param>
/// <param name="IsEnabled">Whether its contributions are active.</param>
/// <param name="Source">Where it came from.</param>
/// <param name="InstalledAt">When it was installed or last updated.</param>
public sealed record InstalledExtension(ExtensionManifest Manifest, string Folder, bool IsEnabled, ExtensionSource Source, DateTimeOffset InstalledAt)
{
    /// <summary>The extension's identifier.</summary>
    public ExtensionIdentifier Id => Manifest.Id;

    /// <summary>The installed version.</summary>
    public string Version => Manifest.Version;

    /// <summary>
    /// The full path of a file of the extension, from a path relative to its folder as manifests write them
    /// (<c>./syntaxes/perl.tmLanguage.json</c>), or <see langword="null"/> when the path leads outside the folder.
    /// </summary>
    /// <param name="relativePath">The relative path.</param>
    public string? Resolve(string? relativePath) => ResolveInFolder(Folder, relativePath);

    /// <summary>Resolves a relative path inside a folder, refusing paths that lead outside it.</summary>
    /// <param name="folder">The folder.</param>
    /// <param name="relativePath">The relative path, with forward or back slashes.</param>
    internal static string? ResolveInFolder(string folder, string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath) || relativePath.Contains(':', StringComparison.Ordinal))
        {
            return null;
        }

        var root = Path.GetFullPath(folder);
        var full = Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar)));
        var rootWithSeparator = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
        return full.StartsWith(rootWithSeparator, StringComparison.Ordinal) ? full : null;
    }
}
