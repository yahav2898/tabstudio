using System.IO.Compression;
using System.Text;
using TabStudio.Extensions.Manifests;

namespace TabStudio.Extensions.Packages;

/// <summary>
/// A <c>.vsix</c> file: a zip archive whose <c>extension/</c> folder holds the extension, with <c>package.json</c>
/// at its root. Reads the manifest and extracts the extension folder, refusing archives that would write outside
/// it or that are unreasonably large.
/// </summary>
public sealed class VsixPackage : IDisposable
{
    /// <summary>The most a package may hold once extracted.</summary>
    public const long MaximumExtractedBytes = 512L * 1024 * 1024;

    /// <summary>The most files a package may hold.</summary>
    public const int MaximumFiles = 50_000;

    private const string ExtensionFolder = "extension/";

    private readonly ZipArchive _zip;
    private readonly Dictionary<string, ZipArchiveEntry> _files = new(StringComparer.Ordinal);

    private VsixPackage(Stream stream, bool leaveOpen)
    {
        try
        {
            _zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen);
        }
        catch (InvalidDataException ex)
        {
            if (!leaveOpen)
            {
                stream.Dispose();
            }

            throw new ExtensionPackageException("The file is not a VS Code extension package (.vsix): it is not a zip archive.", ex);
        }

        foreach (var entry in _zip.Entries)
        {
            var name = entry.FullName.Replace('\\', '/');
            if (name.StartsWith(ExtensionFolder, StringComparison.OrdinalIgnoreCase) && name.Length > ExtensionFolder.Length && !name.EndsWith('/'))
            {
                _files[name[ExtensionFolder.Length..]] = entry;
            }
        }

        if (_files.Count > MaximumFiles)
        {
            _zip.Dispose();
            throw new ExtensionPackageException($"The package has more than {MaximumFiles} files.");
        }

        var packageJson = ReadText("package.json");
        if (packageJson is null)
        {
            _zip.Dispose();
            throw new ExtensionPackageException("The file is not a VS Code extension package (.vsix): it has no extension/package.json.");
        }

        try
        {
            Manifest = ManifestReader.Read(packageJson, ReadText("package.nls.json"));
        }
        catch (ExtensionPackageException)
        {
            _zip.Dispose();
            throw;
        }
    }

    /// <summary>The extension's manifest.</summary>
    public ExtensionManifest Manifest { get; }

    /// <summary>The files of the extension folder, relative to it, with forward slashes.</summary>
    public IReadOnlyCollection<string> Files => _files.Keys;

    /// <summary>Opens a package from a file.</summary>
    /// <param name="path">The .vsix file.</param>
    /// <exception cref="ExtensionPackageException">The file is not an extension package.</exception>
    public static VsixPackage Open(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        return new VsixPackage(File.OpenRead(path), leaveOpen: false);
    }

    /// <summary>Opens a package from a seekable stream.</summary>
    /// <param name="stream">The package's bytes.</param>
    /// <param name="leaveOpen">Whether the stream stays open when the package is disposed.</param>
    /// <exception cref="ExtensionPackageException">The stream is not an extension package.</exception>
    public static VsixPackage Open(Stream stream, bool leaveOpen = false)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanSeek)
        {
            throw new ArgumentException("The package must be read from a seekable stream.", nameof(stream));
        }

        return new VsixPackage(stream, leaveOpen);
    }

    /// <summary>Reads a text file of the extension, or returns <see langword="null"/> when there is no such file.</summary>
    /// <param name="relativePath">The path relative to the extension folder, such as <c>package.json</c>.</param>
    public string? ReadText(string relativePath)
    {
        ArgumentNullException.ThrowIfNull(relativePath);
        if (!_files.TryGetValue(Normalize(relativePath), out var entry))
        {
            return null;
        }

        using var stream = entry.Open();
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// Extracts the extension folder into an empty or missing folder. Files outside <c>extension/</c>, such as the
    /// <c>.vsixmanifest</c>, are left out.
    /// </summary>
    /// <param name="folder">The folder to extract to.</param>
    /// <param name="cancellationToken">Cancels the extraction; what was written so far stays.</param>
    /// <exception cref="ExtensionPackageException">The package would write outside the folder, or is too large.</exception>
    public void ExtractTo(string folder, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(folder);
        var root = Path.GetFullPath(folder);
        var rootWithSeparator = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
        Directory.CreateDirectory(root);

        long written = 0;
        var buffer = new byte[81920];
        foreach (var (relative, entry) in _files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var target = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
            if (!IsSafeRelativePath(relative) || !target.StartsWith(rootWithSeparator, StringComparison.Ordinal))
            {
                throw new ExtensionPackageException($"The package is not safe to install: '{relative}' points outside the extension folder.");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            using var input = entry.Open();
            using var output = File.Create(target);
            int read;
            while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
            {
                written += read;
                if (written > MaximumExtractedBytes)
                {
                    throw new ExtensionPackageException($"The package is larger than {MaximumExtractedBytes / (1024 * 1024)} MB once extracted.");
                }

                output.Write(buffer, 0, read);
            }
        }
    }

    /// <inheritdoc />
    public void Dispose() => _zip.Dispose();

    private static string Normalize(string relativePath)
    {
        var normalized = relativePath.Replace('\\', '/');
        while (normalized.StartsWith("./", StringComparison.Ordinal))
        {
            normalized = normalized[2..];
        }

        return normalized.TrimStart('/');
    }

    private static bool IsSafeRelativePath(string relative) =>
        !Path.IsPathRooted(relative)
        && !relative.Contains(':', StringComparison.Ordinal)
        && relative.Split('/').All(segment => segment is not ("." or ".."));
}
