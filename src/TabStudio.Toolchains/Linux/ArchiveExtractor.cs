using System.Formats.Tar;
using System.IO.Compression;

namespace TabStudio.Toolchains.Linux;

/// <summary>What extracting an archive produced.</summary>
/// <param name="Files">Regular files written.</param>
/// <param name="Folders">Folders created.</param>
/// <param name="Links">Symbolic links created.</param>
/// <param name="HardLinks">Hard links, which were written as copies.</param>
public sealed record ExtractionResult(int Files, int Folders, int Links, int HardLinks);

/// <summary>
/// Unpacks <c>.tar.gz</c> and <c>.zip</c> archives (wheels are zips) into a folder, keeping symbolic links and
/// file modes, and never writing outside the folder.
/// </summary>
/// <remarks>
/// <para>
/// Android does not let apps create hard links, so a hard link becomes a copy of the file it names. Symbolic
/// links are kept as they are, absolute ones included: inside the Linux environment they resolve against its
/// root, as they should.
/// </para>
/// <para>
/// An entry whose path leaves the folder (<c>../</c>, or an absolute path) is refused, as is a file written
/// through a symbolic link that leads out of it, so a malicious archive cannot write anywhere else.
/// </para>
/// </remarks>
public static class ArchiveExtractor
{
    /// <summary>
    /// Unpacks an archive, choosing the format from the file name. A program that is one file, such as a PHP archive
    /// (<c>.phar</c>) or a Java archive (<c>.jar</c>), is not unpacked: it is copied into the folder as it is.
    /// </summary>
    /// <param name="archive">A <c>.tar.gz</c>, <c>.tgz</c>, <c>.zip</c>, <c>.whl</c>, <c>.vsix</c>, <c>.phar</c> or <c>.jar</c> file.</param>
    /// <param name="destination">The folder to unpack into; created when missing.</param>
    /// <param name="cancellationToken">Stops unpacking.</param>
    /// <exception cref="InvalidDataException">The archive is damaged, or an entry would land outside the folder.</exception>
    public static Task<ExtractionResult> ExtractAsync(string archive, string destination, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(archive);
        var name = archive.ToUpperInvariant();
        if (name.EndsWith(".PHAR", StringComparison.Ordinal) || name.EndsWith(".JAR", StringComparison.Ordinal))
        {
            return CopySingleFileAsync(archive, destination, cancellationToken);
        }

        return name.EndsWith(".ZIP", StringComparison.Ordinal) || name.EndsWith(".WHL", StringComparison.Ordinal) || name.EndsWith(".VSIX", StringComparison.Ordinal)
            ? Task.Run(() => ExtractZip(archive, destination, cancellationToken), cancellationToken)
            : ExtractTarGzAsync(archive, destination, cancellationToken);
    }

    /// <summary>Copies a one-file program into a folder, keeping its name, as an executable file.</summary>
    private static async Task<ExtractionResult> CopySingleFileAsync(string file, string destination, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(destination);
        var root = Path.GetFullPath(destination);
        Directory.CreateDirectory(root);
        var target = Path.Combine(root, Path.GetFileName(file));
        await using (var source = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true))
        await using (var copy = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
        {
            await source.CopyToAsync(copy, cancellationToken).ConfigureAwait(false);
        }

        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(target, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        }

        return new ExtractionResult(1, 0, 0, 0);
    }

    /// <summary>Unpacks a gzip-compressed tar archive.</summary>
    /// <param name="archive">The archive.</param>
    /// <param name="destination">The folder to unpack into.</param>
    /// <param name="cancellationToken">Stops unpacking.</param>
    public static async Task<ExtractionResult> ExtractTarGzAsync(string archive, string destination, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(archive);
        ArgumentNullException.ThrowIfNull(destination);
        var root = Path.GetFullPath(destination);
        Directory.CreateDirectory(root);
        int files = 0, folders = 0, links = 0, hardLinks = 0;
        var deferredHardLinks = new List<(string Path, string Target, UnixFileMode Mode)>();

        try
        {
            await using var stream = new FileStream(archive, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
            await using var gzip = new GZipStream(stream, CompressionMode.Decompress);
            using var reader = new TarReader(gzip);
            while (await reader.GetNextEntryAsync(copyData: false, cancellationToken).ConfigureAwait(false) is { } entry)
            {
                if (entry.Name is "./" or "." or "")
                {
                    continue;
                }

                var path = Resolve(root, entry.Name);
                switch (entry.EntryType)
                {
                    case TarEntryType.Directory:
                        Directory.CreateDirectory(path);
                        SetMode(path, entry.Mode | UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
                        folders++;
                        break;

                    case TarEntryType.RegularFile or TarEntryType.V7RegularFile or TarEntryType.ContiguousFile:
                        PrepareParent(root, path);
                        await using (var output = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
                        {
                            if (entry.DataStream is { } data)
                            {
                                await data.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
                            }
                        }

                        SetMode(path, entry.Mode | UnixFileMode.UserRead | UnixFileMode.UserWrite);
                        files++;
                        break;

                    case TarEntryType.SymbolicLink:
                        PrepareParent(root, path);
                        File.CreateSymbolicLink(path, entry.LinkName);
                        links++;
                        break;

                    case TarEntryType.HardLink:
                        deferredHardLinks.Add((path, Resolve(root, entry.LinkName), entry.Mode));
                        break;

                    default:
                        // Devices, FIFOs and extended headers have no place in an app's storage.
                        break;
                }
            }
        }
        catch (Exception ex) when (ex is InvalidDataException or FormatException or EndOfStreamException)
        {
            throw new InvalidDataException($"The archive {Path.GetFileName(archive)} is damaged: {ex.Message}", ex);
        }

        // A hard link may come before the file it names; copy once everything is out.
        foreach (var (path, target, mode) in deferredHardLinks)
        {
            PrepareParent(root, path);
            File.Copy(target, path, overwrite: true);
            SetMode(path, mode | UnixFileMode.UserRead | UnixFileMode.UserWrite);
            hardLinks++;
        }

        return new ExtractionResult(files, folders, links, hardLinks);
    }

    private static ExtractionResult ExtractZip(string archive, string destination, CancellationToken cancellationToken)
    {
        var root = Path.GetFullPath(destination);
        Directory.CreateDirectory(root);
        int files = 0, folders = 0;
        try
        {
            using var zip = ZipFile.OpenRead(archive);
            foreach (var entry in zip.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var path = Resolve(root, entry.FullName);
                if (entry.FullName.EndsWith('/'))
                {
                    Directory.CreateDirectory(path);
                    folders++;
                    continue;
                }

                PrepareParent(root, path);
                entry.ExtractToFile(path, overwrite: true);

                // Zips made on Unix keep the file mode in the upper half of the external attributes.
                var mode = (UnixFileMode)((entry.ExternalAttributes >> 16) & 0x1FF);
                SetMode(path, mode == 0 ? UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead : mode | UnixFileMode.UserRead | UnixFileMode.UserWrite);
                files++;
            }
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException and not DirectoryNotFoundException
            && !(ex is IOException io && Installation.ToolchainInstallException.IsDiskFull(io)))
        {
            // Storage that fills up is not a damaged archive: called damaged, the verified download was deleted.
            throw new InvalidDataException($"The archive {Path.GetFileName(archive)} is damaged: {ex.Message}", ex);
        }

        return new ExtractionResult(files, folders, 0, 0);
    }

    /// <summary>The full path of an entry, refusing one that leaves the root.</summary>
    internal static string Resolve(string root, string entryName)
    {
        var relative = entryName.Replace('\\', '/').TrimStart('/');
        if (relative.StartsWith("./", StringComparison.Ordinal))
        {
            relative = relative[2..];
        }

        var path = Path.GetFullPath(Path.Combine(root, relative));
        var prefix = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
        if (!path.StartsWith(prefix, StringComparison.Ordinal) && path != root)
        {
            throw new InvalidDataException($"The archive entry '{entryName}' would be written outside {root}.");
        }

        return path.TrimEnd(Path.DirectorySeparatorChar);
    }

    /// <summary>
    /// Creates a file's folder and removes what is already at the path. Refuses to write through a symbolic link
    /// to a folder unless it is relative and stays inside the root: the device would resolve an absolute one
    /// against its own root, not the environment's.
    /// </summary>
    private static void PrepareParent(string root, string path)
    {
        var parent = Path.GetDirectoryName(path)!;
        for (var folder = parent; folder.Length > root.Length; folder = Path.GetDirectoryName(folder)!)
        {
            if (new DirectoryInfo(folder).LinkTarget is { } target)
            {
                var resolved = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(folder)!, target));
                if (Path.IsPathRooted(target) || (!resolved.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal) && resolved != root))
                {
                    throw new InvalidDataException($"The archive writes through the symbolic link {Path.GetRelativePath(root, folder)}.");
                }
            }
        }

        Directory.CreateDirectory(parent);
        var existing = new FileInfo(path);
        if (existing.Exists || existing.LinkTarget is not null)
        {
            existing.Delete();
        }
    }

    private static void SetMode(string path, UnixFileMode mode)
    {
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, mode);
        }
    }
}
