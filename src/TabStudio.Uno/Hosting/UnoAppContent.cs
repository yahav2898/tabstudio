using TabStudio.Core.Workspaces;

namespace TabStudio.Uno.Hosting;

/// <summary>
/// The folder <c>ms-appx:///</c> addresses resolve in while an app runs in the preview, laid out as an app's output
/// folder is: the app's own files, such as <c>Assets/Logo.png</c>, at their paths in the project, and the folders its
/// libraries bring, such as <c>Uno.Fonts.OpenSans/Fonts/OpenSans.ttf</c>.
/// </summary>
/// <remarks>
/// Uno Platform resolves those addresses against one folder for the whole process, so the folder is shared by every run
/// and brought up to date before each one: files that changed are copied again, and files no run needs any more go.
/// </remarks>
public static class UnoAppContent
{
    /// <summary>The kinds of project files an app can open through <c>ms-appx:///</c>: images, fonts, media and data.</summary>
    public static IReadOnlySet<string> ContentExtensions { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp", ".svg", ".ico", ".tif", ".tiff",
        ".ttf", ".otf", ".woff", ".woff2",
        ".mp3", ".wav", ".ogg", ".m4a", ".mp4", ".webm", ".mov",
        ".json", ".txt", ".csv", ".html", ".htm", ".css", ".pdf", ".md", ".lottie",
    };

    /// <summary>Folders of a project that are never an app's content.</summary>
    private static readonly HashSet<string> SkippedFolders = new(StringComparer.OrdinalIgnoreCase)
    {
        "bin", "obj", "Platforms", "Properties", "node_modules",
    };

    /// <summary>Brings a content folder up to date for a run of an app.</summary>
    /// <param name="directory">The content folder.</param>
    /// <param name="project">The app's project.</param>
    /// <param name="assets">What its packages bring.</param>
    /// <param name="keep">File names at the folder's top level that belong to TabStudio, such as its host assembly.</param>
    /// <returns>How many files were copied.</returns>
    public static int Prepare(string directory, ProjectInfo project, UnoRuntimeAssets assets, IReadOnlyCollection<string> keep)
    {
        ArgumentException.ThrowIfNullOrEmpty(directory);
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(assets);
        ArgumentNullException.ThrowIfNull(keep);

        Directory.CreateDirectory(directory);
        var wanted = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var folder in assets.Content)
        {
            foreach (var file in Directory.EnumerateFiles(folder.SourceDirectory, "*", SearchOption.AllDirectories))
            {
                wanted[Path.Combine(folder.Name, Path.GetRelativePath(folder.SourceDirectory, file))] = file;
            }
        }

        foreach (var file in ProjectContent(project.DirectoryPath))
        {
            // The app's own files win over a library's at the same path, as in the build's output.
            wanted[Path.GetRelativePath(project.DirectoryPath, file)] = file;
        }

        var copied = 0;
        foreach (var (relative, source) in wanted)
        {
            var target = Path.Combine(directory, relative);
            if (IsCurrent(source, target))
            {
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(source, target, overwrite: true);
            File.SetLastWriteTimeUtc(target, File.GetLastWriteTimeUtc(source));
            copied++;
        }

        RemoveStale(directory, wanted.Keys.ToHashSet(StringComparer.Ordinal), keep);
        return copied;
    }

    /// <summary>The content files of a project: the files under its folder that are content, outside build and platform folders.</summary>
    public static IEnumerable<string> ProjectContent(string projectDirectory)
    {
        ArgumentException.ThrowIfNullOrEmpty(projectDirectory);
        var pending = new Stack<string>([projectDirectory]);
        while (pending.TryPop(out var directory))
        {
            IEnumerable<string> files;
            IEnumerable<string> folders;
            try
            {
                files = Directory.EnumerateFiles(directory).ToList();
                folders = Directory.EnumerateDirectories(directory).ToList();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            foreach (var file in files.Where(f => ContentExtensions.Contains(Path.GetExtension(f))))
            {
                yield return file;
            }

            foreach (var folder in folders)
            {
                var name = Path.GetFileName(folder);
                if (!SkippedFolders.Contains(name) && !PathUtilities.IsIgnoredDirectory(name))
                {
                    pending.Push(folder);
                }
            }
        }
    }

    private static bool IsCurrent(string source, string target)
    {
        var from = new FileInfo(source);
        var to = new FileInfo(target);
        return to.Exists && to.Length == from.Length && to.LastWriteTimeUtc == from.LastWriteTimeUtc;
    }

    private static void RemoveStale(string directory, HashSet<string> wanted, IReadOnlyCollection<string> keep)
    {
        foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories).ToList())
        {
            var relative = Path.GetRelativePath(directory, file);
            if (!wanted.Contains(relative) && !keep.Contains(relative))
            {
                try
                {
                    File.Delete(file);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // A file in use stays until the next run.
                }
            }
        }
    }
}
