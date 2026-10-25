using System.Text.Json.Nodes;

namespace TabStudio.LanguageServers.Hosting;

/// <summary>
/// Fills in the variables of a server's <c>initializationOptions</c>. <c>${packageFile:typescript/lib/tsserver.js}</c>
/// is a file of an npm package: the workspace's own copy in <c>node_modules</c> when it has one, otherwise the copy
/// installed next to the server's own package, as <c>npm install -g</c> puts them. An option whose file is not found
/// is left out, so the server falls back to its own search.
/// </summary>
internal static class InitializationOptions
{
    private const string PackageFilePrefix = "${packageFile:";

    /// <summary>The options with their variables filled in, or <see langword="null"/> when there are none.</summary>
    /// <param name="options">The server's options.</param>
    /// <param name="rootDirectory">The workspace folder the server runs for.</param>
    /// <param name="executable">The server's program, as the launcher resolved it.</param>
    /// <param name="log">Told which file each variable became, or that none was found.</param>
    public static JsonNode? Resolve(JsonObject? options, string rootDirectory, string executable, Action<string>? log = null)
    {
        if (options?.DeepClone() is not JsonObject copy)
        {
            return null;
        }

        Fill(copy, rootDirectory, executable, log);
        return copy;
    }

    /// <summary>Finds a file of an npm package for a server.</summary>
    /// <param name="relativePath">The package and the file in it, such as <c>typescript/lib/tsserver.js</c>.</param>
    /// <param name="rootDirectory">The workspace folder.</param>
    /// <param name="executable">The server's program.</param>
    /// <returns>The file's full path, or <see langword="null"/> when no copy was found.</returns>
    public static string? FindPackageFile(string relativePath, string rootDirectory, string executable)
    {
        var relative = relativePath.Replace('/', Path.DirectorySeparatorChar);
        foreach (var modules in NodeModulesFolders(rootDirectory, executable))
        {
            var candidate = Path.Combine(modules, relative);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static void Fill(JsonObject node, string rootDirectory, string executable, Action<string>? log)
    {
        foreach (var name in node.Select(p => p.Key).ToList())
        {
            switch (node[name])
            {
                case JsonObject child:
                    Fill(child, rootDirectory, executable, log);
                    break;
                case JsonValue value when value.GetValueKind() == System.Text.Json.JsonValueKind.String
                    && value.GetValue<string>() is { } text
                    && text.StartsWith(PackageFilePrefix, StringComparison.Ordinal) && text.EndsWith('}'):
                    var relative = text[PackageFilePrefix.Length..^1];
                    var file = FindPackageFile(relative, rootDirectory, executable);
                    if (file is null)
                    {
                        node.Remove(name);
                        log?.Invoke($"{relative} was not found in the workspace or beside {executable}.");
                    }
                    else
                    {
                        node[name] = file;
                        log?.Invoke($"Using {file}.");
                    }

                    break;
            }
        }
    }

    /// <summary>The <c>node_modules</c> folders to look in: the workspace's, then those around the server's program.</summary>
    private static IEnumerable<string> NodeModulesFolders(string rootDirectory, string executable)
    {
        yield return Path.Combine(rootDirectory, "node_modules");

        // npm links a global command to its package: bin/typescript-language-server -> lib/node_modules/<package>/...
        var program = RealPath(executable);
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

    private static string RealPath(string path)
    {
        try
        {
            return new FileInfo(path).ResolveLinkTarget(returnFinalTarget: true)?.FullName ?? path;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return path;
        }
    }
}
