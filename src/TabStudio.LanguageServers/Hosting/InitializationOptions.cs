using System.Text.Json.Nodes;

namespace TabStudio.LanguageServers.Hosting;

/// <summary>
/// Fills in the variables of a server's <c>initializationOptions</c> and settings. <c>${packageFile:typescript/lib/tsserver.js}</c>
/// is a file of an npm package: the workspace's own copy in <c>node_modules</c> when it has one, otherwise the copy
/// installed next to the server's own package, as <c>npm install -g</c> puts them.
/// <c>${workspaceFile:.venv/bin/python|.venv/Scripts/python.exe}</c> is the first of those files of the folder the
/// server serves that exists, such as the interpreter of the project's virtual environment, which Pyright does not
/// look for by itself. <c>${env:NAME:default}</c> is an environment variable. An option whose file is not found is left
/// out, so the server falls back to its own search; the project setup can create the file later, so the variables are
/// filled in again whenever the options are sent.
/// </summary>
/// <remarks>
/// Paths are the server's: in TabStudio's Linux environment on Android the server sees its files at other paths than
/// TabStudio does, so files are checked through the launcher's <see cref="IServerProcessLauncher.ToLocalPath"/> and
/// the options get the paths the server sees.
/// </remarks>
internal static class InitializationOptions
{
    private const string PackageFilePrefix = "${packageFile:";
    private const string WorkspaceFilePrefix = "${workspaceFile:";
    private const int MaxLinks = 16;

    /// <summary>The options with their variables filled in, or <see langword="null"/> when there are none.</summary>
    /// <param name="options">The server's options.</param>
    /// <param name="rootDirectory">The workspace folder the server runs for.</param>
    /// <param name="executable">The server's program, as the launcher resolved it.</param>
    /// <param name="log">Told which file each variable became, or that none was found (a warning).</param>
    /// <param name="toLocalPath">Finds a file the server sees at a path on this device; the same path when <see langword="null"/>.</param>
    public static JsonNode? Resolve(JsonObject? options, string rootDirectory, string executable, Action<string, bool>? log = null, Func<string, string?>? toLocalPath = null)
    {
        if (options?.DeepClone() is not JsonObject copy)
        {
            return null;
        }

        Fill(copy, rootDirectory, executable, log, toLocalPath ?? LocalFile);
        return copy;
    }

    /// <summary>Finds a file of an npm package for a server.</summary>
    /// <param name="relativePath">The package and the file in it, such as <c>typescript/lib/tsserver.js</c>.</param>
    /// <param name="rootDirectory">The workspace folder.</param>
    /// <param name="executable">The server's program.</param>
    /// <param name="toLocalPath">Finds a file the server sees at a path on this device; the same path when <see langword="null"/>.</param>
    /// <returns>The file's full path as the server sees it, or <see langword="null"/> when no copy was found.</returns>
    public static string? FindPackageFile(string relativePath, string rootDirectory, string executable, Func<string, string?>? toLocalPath = null)
    {
        toLocalPath ??= LocalFile;
        var relative = relativePath.Replace('/', Path.DirectorySeparatorChar);
        foreach (var modules in NodeModulesFolders(rootDirectory, executable, toLocalPath))
        {
            var candidate = Path.Combine(modules, relative);
            if (toLocalPath(candidate) is not null)
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>The first of a server folder's files that exists, such as a project's virtual environment interpreter.</summary>
    /// <param name="relativePaths">The files relative to the folder, separated by <c>|</c>, in order of preference.</param>
    /// <param name="rootDirectory">The folder the server serves.</param>
    /// <param name="toLocalPath">Finds a file the server sees at a path on this device; the same path when <see langword="null"/>.</param>
    /// <returns>The file's full path, or <see langword="null"/> when none of them exists.</returns>
    public static string? FindWorkspaceFile(string relativePaths, string rootDirectory, Func<string, string?>? toLocalPath = null)
    {
        ArgumentNullException.ThrowIfNull(relativePaths);
        ArgumentNullException.ThrowIfNull(rootDirectory);
        toLocalPath ??= LocalFile;
        foreach (var relative in relativePaths.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var candidate = Path.GetFullPath(Path.Combine(rootDirectory, relative.Replace('/', Path.DirectorySeparatorChar)));
            if (toLocalPath(candidate) is not null || IsLink(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static string? LocalFile(string path) => File.Exists(path) ? path : null;

    /// <summary>
    /// Whether a path is a symbolic link: a virtual environment's <c>bin/python</c> links to an interpreter that, in the
    /// Linux environment on Android, only exists at the path the server sees, so the link itself is what counts.
    /// </summary>
    private static bool IsLink(string path)
    {
        try
        {
            return new FileInfo(path).LinkTarget is not null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static void Fill(JsonObject node, string rootDirectory, string executable, Action<string, bool>? log, Func<string, string?> toLocalPath)
    {
        foreach (var name in node.Select(p => p.Key).ToList())
        {
            if (node[name] is JsonObject child)
            {
                Fill(child, rootDirectory, executable, log, toLocalPath);
            }
            else if (node[name] is JsonArray array)
            {
                for (var i = array.Count - 1; i >= 0; i--)
                {
                    if (array[i] is JsonObject entry)
                    {
                        Fill(entry, rootDirectory, executable, log, toLocalPath);
                    }
                    else if (array[i] is JsonValue value && value.TryGetValue<string>(out var text) && IsVariable(text))
                    {
                        var resolved = ResolveVariable(text, rootDirectory, executable, log, toLocalPath);
                        if (resolved is null)
                        {
                            array.RemoveAt(i);
                        }
                        else
                        {
                            array[i] = resolved;
                        }
                    }
                }
            }
            else if (node[name] is JsonValue value && value.TryGetValue<string>(out var text) && IsVariable(text))
            {
                var resolved = ResolveVariable(text, rootDirectory, executable, log, toLocalPath);
                if (resolved is null)
                {
                    node.Remove(name);
                }
                else
                {
                    node[name] = resolved;
                }
            }
        }
    }

    private static bool IsVariable(string text) => text.EndsWith('}')
        && (text.StartsWith(PackageFilePrefix, StringComparison.Ordinal) || text.StartsWith(WorkspaceFilePrefix, StringComparison.Ordinal)
            || text.StartsWith("${env:", StringComparison.Ordinal));

    private static string? ResolveVariable(string text, string rootDirectory, string executable, Action<string, bool>? log, Func<string, string?> toLocalPath)
    {
        if (text.StartsWith("${env:", StringComparison.Ordinal))
        {
            var parts = text[6..^1].Split(':', 2);
            return Environment.GetEnvironmentVariable(parts[0]) is { Length: > 0 } value ? value : parts.Length == 2 ? parts[1] : null;
        }

        if (text.StartsWith(WorkspaceFilePrefix, StringComparison.Ordinal))
        {
            return FindWorkspaceFile(text[WorkspaceFilePrefix.Length..^1], rootDirectory, toLocalPath);
        }

        var relative = text[PackageFilePrefix.Length..^1];
        var file = FindPackageFile(relative, rootDirectory, executable, toLocalPath);
        log?.Invoke(file is null ? $"{relative} was not found in the workspace or beside {executable}." : $"Using {file}.", file is null);
        return file;
    }

    /// <summary>The <c>node_modules</c> folders to look in: the workspace's, then those around the server's program.</summary>
    private static IEnumerable<string> NodeModulesFolders(string rootDirectory, string executable, Func<string, string?> toLocalPath)
    {
        yield return Path.Combine(rootDirectory, "node_modules");

        // npm links a global command to its package: bin/typescript-language-server -> lib/node_modules/<package>/...
        var program = RealPath(executable, toLocalPath);
        for (var folder = Path.GetDirectoryName(program); folder is not null; folder = Path.GetDirectoryName(folder))
        {
            if (string.Equals(Path.GetFileName(folder), "node_modules", StringComparison.Ordinal))
            {
                yield return folder;
                break;
            }
        }

        // Without links, as on Windows, the commands sit beside node_modules, or in bin beside lib/node_modules.
        if (Path.GetDirectoryName(executable) is { } bin)
        {
            yield return Path.Combine(bin, "node_modules");
            if (Path.GetDirectoryName(bin) is { } prefix)
            {
                yield return Path.Combine(prefix, "lib", "node_modules");
            }
        }
    }

    /// <summary>
    /// Follows a program's links to the file they lead to, as the server sees the paths: each link is read from
    /// the file on this device, and a relative target is taken from the folder of the link.
    /// </summary>
    private static string RealPath(string path, Func<string, string?> toLocalPath)
    {
        for (var hop = 0; hop < MaxLinks; hop++)
        {
            string? target;
            try
            {
                target = toLocalPath(path) is { } local ? new FileInfo(local).LinkTarget : null;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return path;
            }

            if (target is null || Path.GetDirectoryName(path) is not { } folder)
            {
                return path;
            }

            path = Path.GetFullPath(Path.IsPathRooted(target) ? target : Path.Combine(folder, target));
        }

        return path;
    }
}
