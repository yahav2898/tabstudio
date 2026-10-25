using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using TabStudio.Compilation;
using TabStudio.Core.Workspaces;

namespace TabStudio.Link.Language;

/// <summary>
/// The files of a tablet's workspace as the host needs them: every file under the workspace's folders except build
/// output, plus the <c>Directory.Build.props</c> and similar files above them. C# and XAML files carry the editor's
/// text, unsaved edits included; everything else, such as images, its bytes on disk.
/// </summary>
internal sealed class LinkWorkspaceContent
{
    /// <summary>Files larger than this are not sent; no source file or resource is this large.</summary>
    public const long MaxFileLength = 64L * 1024 * 1024;

    /// <summary>The most files a workspace may have to be linked.</summary>
    public const int MaxFiles = 20_000;

    /// <summary>The most bytes a workspace may have to be linked.</summary>
    public const long MaxTotalLength = 1024L * 1024 * 1024;

    /// <summary>Files MSBuild and NuGet read from the folders above a project.</summary>
    private static readonly string[] AncestorFileNames =
    [
        "Directory.Build.props", "Directory.Build.targets", "Directory.Packages.props", "global.json", "nuget.config", "NuGet.config", "NuGet.Config",
    ];

    private readonly List<string> _fullPaths;
    private readonly HashSet<string> _textFiles;
    private readonly ISourceTextProvider _sources;

    private LinkWorkspaceContent(string root, string entryPath, List<WorkspaceFileEntry> files, List<string> fullPaths, HashSet<string> textFiles, ISourceTextProvider sources)
    {
        Root = root;
        EntryPath = entryPath;
        Files = files;
        _fullPaths = fullPaths;
        _textFiles = textFiles;
        _sources = sources;
    }

    /// <summary>The folder that contains every file, the mirror's root.</summary>
    public string Root { get; }

    /// <summary>What the host loads: the solution file, or the workspace's folder.</summary>
    public string EntryPath { get; }

    /// <summary>The files, relative to <see cref="Root"/>.</summary>
    public IReadOnlyList<WorkspaceFileEntry> Files { get; }

    /// <summary>Collects a workspace's files and their hashes.</summary>
    /// <param name="workspace">The workspace.</param>
    /// <param name="sources">The editor's text of C# and XAML files.</param>
    /// <param name="hashes">Hashes of files on disk from earlier collections, kept while they are unchanged.</param>
    /// <param name="cancellationToken">Gives up.</param>
    /// <exception cref="IOException">The workspace is too large to link.</exception>
    public static LinkWorkspaceContent Collect(Workspace workspace, ISourceTextProvider sources, ConcurrentDictionary<string, FileHash> hashes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(hashes);

        var textFiles = TextFiles(workspace);
        var files = new SortedDictionary<string, FileInfo>(StringComparer.Ordinal);
        foreach (var directory in RootDirectories(workspace))
        {
            Walk(directory, files, cancellationToken);
        }

        foreach (var ancestor in AncestorFiles(workspace))
        {
            files.TryAdd(ancestor.FullName, ancestor);
        }

        if (files.Count > MaxFiles)
        {
            throw new IOException($"{workspace.Name} has {files.Count} files, more than the {MaxFiles} TabStudio Link sends to a host.");
        }

        var root = CommonRoot([workspace.RootDirectory, .. files.Keys.Select(path => Path.GetDirectoryName(path)).OfType<string>()]);
        var separator = Path.DirectorySeparatorChar;
        var prefix = root.EndsWith(separator) ? root : root + separator;
        var entries = new List<WorkspaceFileEntry>(files.Count);
        var fullPaths = new List<string>(files.Count);
        long total = 0;
        foreach (var (path, info) in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!path.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            FileHash hash;
            if (textFiles.Contains(path))
            {
                string text;
                try
                {
                    text = sources.GetText(path);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    continue;
                }

                hash = FileHash.Of(Encoding.UTF8.GetBytes(text));
            }
            else
            {
                var key = new FileHash(info.Length, info.LastWriteTimeUtc.Ticks, string.Empty);
                if (!hashes.TryGetValue(path, out var known) || known.Length != key.Length || known.LastWrite != key.LastWrite)
                {
                    try
                    {
                        known = FileHash.Of(File.ReadAllBytes(path)) with { LastWrite = key.LastWrite };
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                        continue;
                    }

                    hashes[path] = known;
                }

                hash = known;
            }

            total += hash.Length;
            if (total > MaxTotalLength)
            {
                throw new IOException($"{workspace.Name} is larger than the {MaxTotalLength / (1024 * 1024)} MB TabStudio Link sends to a host.");
            }

            entries.Add(new WorkspaceFileEntry(path[prefix.Length..].Replace(separator, '/'), hash.Length, hash.Hash));
            fullPaths.Add(path);
        }

        var entryPath = workspace.Solution?.FilePath ?? workspace.RootDirectory;
        return new LinkWorkspaceContent(root, entryPath, entries, fullPaths, textFiles, sources);
    }

    /// <summary>The bytes of a file to send: the editor's text of C# and XAML files, the disk's bytes of others.</summary>
    /// <param name="index">The file's position in <see cref="Files"/>.</param>
    public byte[] Read(int index)
    {
        var path = _fullPaths[index];
        return _textFiles.Contains(path) ? Encoding.UTF8.GetBytes(_sources.GetText(path)) : File.ReadAllBytes(path);
    }

    /// <summary>The C# and XAML files of the workspace's projects, whose text the editor may hold unsaved.</summary>
    private static HashSet<string> TextFiles(Workspace workspace)
    {
        var directories = workspace.Projects.Select(p => p.DirectoryPath).ToList();
        var files = new HashSet<string>(StringComparer.Ordinal);
        foreach (var project in workspace.Projects.Where(p => p.Language == ProjectLanguage.CSharp))
        {
            files.UnionWith(ProjectSourceFiles.Enumerate(project, directories));
            files.UnionWith(ProjectSourceFiles.Enumerate(project, directories, ".axaml"));
        }

        return files;
    }

    /// <summary>The workspace's folder and the folders of projects outside it.</summary>
    private static List<string> RootDirectories(Workspace workspace)
    {
        var roots = new List<string> { workspace.RootDirectory };
        foreach (var directory in workspace.Projects.Select(p => p.DirectoryPath).Distinct(StringComparer.Ordinal))
        {
            if (directory.Length > 0 && !roots.Any(r => IsUnder(directory, r)))
            {
                roots.RemoveAll(r => IsUnder(r, directory));
                roots.Add(directory);
            }
        }

        return roots;
    }

    /// <summary>MSBuild and NuGet files in the folders above the workspace's folders.</summary>
    private static IEnumerable<FileInfo> AncestorFiles(Workspace workspace)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var start in RootDirectories(workspace))
        {
            for (var directory = Directory.GetParent(start); directory is not null; directory = directory.Parent)
            {
                if (!seen.Add(directory.FullName))
                {
                    break;
                }

                foreach (var name in AncestorFileNames)
                {
                    var file = new FileInfo(Path.Combine(directory.FullName, name));
                    bool exists;
                    try
                    {
                        exists = file.Exists;
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                        exists = false;
                    }

                    if (exists)
                    {
                        yield return file;
                    }
                }
            }
        }
    }

    private static void Walk(string directory, SortedDictionary<string, FileInfo> files, CancellationToken cancellationToken)
    {
        var pending = new Stack<DirectoryInfo>();
        pending.Push(new DirectoryInfo(directory));
        while (pending.TryPop(out var current))
        {
            cancellationToken.ThrowIfCancellationRequested();
            FileSystemInfo[] entries;
            try
            {
                entries = current.GetFileSystemInfos();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            foreach (var entry in entries)
            {
                if (entry is DirectoryInfo child)
                {
                    if (!PathUtilities.IsIgnoredDirectory(child.Name) && !child.Attributes.HasFlag(FileAttributes.ReparsePoint))
                    {
                        pending.Push(child);
                    }
                }
                else if (entry is FileInfo file && file.Length <= MaxFileLength && !file.Attributes.HasFlag(FileAttributes.ReparsePoint))
                {
                    files.TryAdd(file.FullName, file);
                    if (files.Count > MaxFiles)
                    {
                        return;
                    }
                }
            }
        }
    }

    /// <summary>The deepest folder that contains every given folder.</summary>
    private static string CommonRoot(IEnumerable<string> directories)
    {
        string? common = null;
        foreach (var directory in directories)
        {
            var path = Path.TrimEndingDirectorySeparator(directory);
            if (common is null)
            {
                common = path;
                continue;
            }

            while (!IsUnder(path, common) && Path.GetDirectoryName(common) is { } parent)
            {
                common = parent;
            }
        }

        return common ?? Path.GetPathRoot(Environment.CurrentDirectory) ?? "/";
    }

    private static bool IsUnder(string path, string directory)
    {
        var prefix = directory.EndsWith(Path.DirectorySeparatorChar) ? directory : directory + Path.DirectorySeparatorChar;
        return string.Equals(path, directory, StringComparison.Ordinal) || path.StartsWith(prefix, StringComparison.Ordinal);
    }
}

/// <summary>A file's length, its last write time when it was hashed, and its SHA-256.</summary>
/// <param name="Length">The length in bytes.</param>
/// <param name="LastWrite">The last write time in UTC ticks, or 0 for editor text.</param>
/// <param name="Hash">The SHA-256 in hexadecimal.</param>
internal sealed record FileHash(long Length, long LastWrite, string Hash)
{
    /// <summary>The hash of some bytes.</summary>
    public static FileHash Of(byte[] content) => new(content.Length, 0, Convert.ToHexString(SHA256.HashData(content)));
}
