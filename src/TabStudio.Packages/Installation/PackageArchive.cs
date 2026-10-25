using System.IO.Compression;
using TabStudio.Packages.Manifests;

namespace TabStudio.Packages.Installation;

/// <summary>
/// Reads and safely extracts <c>.nupkg</c> files, which are zip archives with a <c>.nuspec</c> at their root.
/// </summary>
/// <remarks>
/// <para>
/// Every entry name is unescaped (packages store names percent-encoded), normalized to forward slashes and checked
/// before anything is written: an entry that would land outside the target folder, such as <c>../evil.dll</c> or an
/// absolute path, rejects the whole package ("zip slip").
/// </para>
/// <para>
/// The packaging metadata (<c>[Content_Types].xml</c>, <c>_rels/</c>, <c>package/</c> and the signature) is not extracted.
/// </para>
/// </remarks>
internal static class PackageArchive
{
    /// <summary>Reads the manifest of a package without extracting it.</summary>
    /// <param name="packagePath">The <c>.nupkg</c> file.</param>
    /// <exception cref="InvalidPackageException">The file is not a zip archive or has no valid manifest.</exception>
    public static PackageManifest ReadManifest(string packagePath)
    {
        using var archive = Open(packagePath);
        var entry = archive.Entries.FirstOrDefault(e => IsRootManifest(RelativePath(e)))
            ?? throw new InvalidPackageException("The package has no .nuspec manifest at its root.");

        using var stream = entry.Open();
        return NuspecReader.Read(stream);
    }

    /// <summary>Extracts a package into a folder and returns its manifest.</summary>
    /// <param name="packagePath">The <c>.nupkg</c> file.</param>
    /// <param name="targetDirectory">The folder to extract into. It is created when missing.</param>
    /// <exception cref="InvalidPackageException">
    /// The file is not a zip archive, has no valid manifest, or has an entry that points outside <paramref name="targetDirectory"/>.
    /// Nothing is written in that last case.
    /// </exception>
    public static PackageManifest Extract(string packagePath, string targetDirectory)
    {
        using var archive = Open(packagePath);
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(targetDirectory)) + Path.DirectorySeparatorChar;

        // Check every entry before writing anything.
        var plan = new List<(ZipArchiveEntry Entry, string Destination, bool IsDirectory)>();
        string? manifestPath = null;
        foreach (var entry in archive.Entries)
        {
            var relative = RelativePath(entry);
            if (relative.Length == 0 || IsPackagingMetadata(relative))
            {
                continue;
            }

            var destination = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
            if (!destination.StartsWith(root, StringComparison.Ordinal))
            {
                throw new InvalidPackageException($"The package contains the entry '{entry.FullName}', which points outside its folder, so it was not extracted.");
            }

            var isDirectory = relative.EndsWith('/');
            plan.Add((entry, destination, isDirectory));
            if (!isDirectory && manifestPath is null && IsRootManifest(relative))
            {
                manifestPath = destination;
            }
        }

        if (manifestPath is null)
        {
            throw new InvalidPackageException("The package has no .nuspec manifest at its root.");
        }

        Directory.CreateDirectory(root);
        foreach (var (entry, destination, isDirectory) in plan)
        {
            if (isDirectory)
            {
                Directory.CreateDirectory(destination);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destination) ?? root);
            entry.ExtractToFile(destination, overwrite: true);
        }

        using var manifest = File.OpenRead(manifestPath);
        return NuspecReader.Read(manifest);
    }

    private static ZipArchive Open(string packagePath)
    {
        var stream = File.OpenRead(packagePath);
        try
        {
            return new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
        }
        catch (InvalidDataException ex)
        {
            stream.Dispose();
            throw new InvalidPackageException("The file is not a valid package: it is not a zip archive.", ex);
        }
    }

    /// <summary>The entry name as a relative path with forward slashes, percent-escapes decoded.</summary>
    private static string RelativePath(ZipArchiveEntry entry) =>
        Uri.UnescapeDataString(entry.FullName.Replace('\\', '/'));

    private static bool IsRootManifest(string relativePath) =>
        !relativePath.Contains('/', StringComparison.Ordinal) &&
        relativePath.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase);

    private static bool IsPackagingMetadata(string relativePath) =>
        relativePath.Equals("[Content_Types].xml", StringComparison.OrdinalIgnoreCase) ||
        relativePath.Equals(".signature.p7s", StringComparison.OrdinalIgnoreCase) ||
        relativePath.StartsWith("_rels/", StringComparison.OrdinalIgnoreCase) ||
        relativePath.StartsWith("package/", StringComparison.OrdinalIgnoreCase);
}
