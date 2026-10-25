using System.Text.Json;
using TabStudio.Core.Workspaces;

namespace TabStudio.Core.Detection;

/// <summary>
/// Looks at a repository and works out what it is built with, so that TabStudio can set it up
/// without asking: "Open any repo" in the UI.
/// </summary>
/// <remarks>
/// Detection only reads marker files (<c>global.json</c>, <c>package.json</c>, <c>Cargo.toml</c> and so on)
/// within a few levels of the root. It never runs anything from the repository.
/// </remarks>
public static class StackDetector
{
    private const int MaxFilesScanned = 5_000;

    private static readonly JsonDocumentOptions JsonOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    private static readonly (string Package, StackKind Kind, string Name)[] NodeFrameworks =
    [
        ("next", StackKind.NextJs, "Next.js"),
        ("react", StackKind.React, "React"),
        ("vue", StackKind.Vue, "Vue"),
        ("@angular/core", StackKind.Angular, "Angular"),
        ("svelte", StackKind.Svelte, "Svelte"),
        ("vite", StackKind.Vite, "Vite"),
        ("typescript", StackKind.TypeScript, "TypeScript"),
    ];

    /// <summary>Detects the technologies used in a repository.</summary>
    /// <param name="rootDirectory">The repository root.</param>
    /// <param name="projects">Projects already loaded for the workspace, used to detect UI frameworks. Optional.</param>
    /// <param name="maxDepth">How many folder levels below the root to look at.</param>
    public static StackProfile Detect(string rootDirectory, IEnumerable<ProjectInfo>? projects = null, int maxDepth = 3)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        if (!Directory.Exists(rootDirectory))
        {
            return StackProfile.Empty;
        }

        var found = new Dictionary<StackKind, StackComponent>();

        foreach (var file in EnumerateFilesBreadthFirst(rootDirectory, maxDepth))
        {
            var relative = Path.GetRelativePath(rootDirectory, file);
            foreach (var component in InspectFile(file, relative))
            {
                found.TryAdd(component.Kind, component);
            }
        }

        foreach (var project in projects ?? [])
        {
            if (FromProjectKind(project) is { } component)
            {
                found.TryAdd(component.Kind, component with { Evidence = Path.GetRelativePath(rootDirectory, project.FilePath) });
            }
        }

        return new StackProfile(found.Values.OrderBy(c => c.Kind).ToList());
    }

    private static IEnumerable<StackComponent> InspectFile(string fullPath, string relativePath)
    {
        var name = Path.GetFileName(fullPath);
        var extension = Path.GetExtension(fullPath).ToUpperInvariant();

        switch (name.ToUpperInvariant())
        {
            case "GLOBAL.JSON":
                yield return new StackComponent(StackKind.DotNet, ".NET", relativePath, ReadJsonString(fullPath, "sdk", "version"));
                yield break;
            case "PACKAGE.JSON":
                foreach (var component in InspectPackageJson(fullPath, relativePath))
                {
                    yield return component;
                }

                yield break;
            case "TSCONFIG.JSON":
                yield return new StackComponent(StackKind.TypeScript, "TypeScript", relativePath);
                yield break;
            case "CARGO.TOML":
                yield return new StackComponent(StackKind.Rust, "Rust", relativePath);
                yield break;
            case "GO.MOD":
                yield return new StackComponent(StackKind.Go, "Go", relativePath, ReadGoVersion(fullPath));
                yield break;
            case "CMAKELISTS.TXT":
                yield return new StackComponent(StackKind.Cpp, "C++", relativePath);
                yield break;
            case "PYPROJECT.TOML" or "REQUIREMENTS.TXT" or "SETUP.PY" or "PIPFILE":
                yield return new StackComponent(StackKind.Python, "Python", relativePath);
                yield break;
            case "POM.XML" or "BUILD.GRADLE" or "SETTINGS.GRADLE":
                yield return new StackComponent(StackKind.Java, "Java", relativePath);
                yield break;
            case "BUILD.GRADLE.KTS" or "SETTINGS.GRADLE.KTS":
                yield return new StackComponent(StackKind.Kotlin, "Kotlin", relativePath);
                yield break;
            case "FOUNDRY.TOML" or "HARDHAT.CONFIG.JS" or "HARDHAT.CONFIG.TS" or "HARDHAT.CONFIG.CJS" or "HARDHAT.CONFIG.MJS":
                yield return new StackComponent(StackKind.Solidity, "Solidity", relativePath);
                yield break;
            case "MOVE.TOML":
                yield return new StackComponent(StackKind.Move, "Move", relativePath);
                yield break;
            case "DEVCONTAINER.JSON" or ".DEVCONTAINER.JSON":
                yield return new StackComponent(StackKind.DevContainer, "Dev Container", relativePath);
                yield break;
            case "DOCKERFILE":
                yield return new StackComponent(StackKind.Docker, "Docker", relativePath);
                yield break;
        }

        switch (extension)
        {
            case ".SLN" or ".SLNX" or ".CSPROJ" or ".FSPROJ" or ".VBPROJ":
                yield return new StackComponent(StackKind.DotNet, ".NET", relativePath);
                break;
            case ".VCXPROJ":
                yield return new StackComponent(StackKind.Cpp, "C++", relativePath);
                break;
            case ".SOL":
                yield return new StackComponent(StackKind.Solidity, "Solidity", relativePath);
                break;
        }
    }

    private static List<StackComponent> InspectPackageJson(string fullPath, string relativePath)
    {
        var result = new List<StackComponent> { new(StackKind.Node, "Node.js", relativePath) };

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(fullPath), JsonOptions);
            var dependencies = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            foreach (var section in new[] { "dependencies", "devDependencies", "peerDependencies" })
            {
                if (document.RootElement.TryGetProperty(section, out var element) && element.ValueKind == JsonValueKind.Object)
                {
                    foreach (var dependency in element.EnumerateObject())
                    {
                        dependencies.TryAdd(dependency.Name, dependency.Value.ValueKind == JsonValueKind.String ? dependency.Value.GetString() : null);
                    }
                }
            }

            foreach (var (package, kind, name) in NodeFrameworks)
            {
                if (dependencies.TryGetValue(package, out var version))
                {
                    result.Add(new StackComponent(kind, name, relativePath, version?.TrimStart('^', '~', '=', 'v')));
                }
            }
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            // A malformed package.json still proves the repository uses Node.
        }

        return result;
    }

    private static StackComponent? FromProjectKind(ProjectInfo project) => project.Kind switch
    {
        ProjectKind.AspNetCore => new StackComponent(StackKind.AspNetCore, "ASP.NET Core", string.Empty),
        ProjectKind.BlazorWebAssembly => new StackComponent(StackKind.Blazor, "Blazor", string.Empty),
        ProjectKind.BlazorWebApp => new StackComponent(StackKind.Blazor, "Blazor", string.Empty),
        ProjectKind.Wpf => new StackComponent(StackKind.Wpf, "WPF", string.Empty),
        ProjectKind.WinForms => new StackComponent(StackKind.WinForms, "Windows Forms", string.Empty),
        ProjectKind.WinUI => new StackComponent(StackKind.WinUI, "WinUI", string.Empty),
        ProjectKind.Maui => new StackComponent(StackKind.Maui, ".NET MAUI", string.Empty),
        ProjectKind.Avalonia => new StackComponent(StackKind.Avalonia, "Avalonia", string.Empty),
        ProjectKind.Uno => new StackComponent(StackKind.Uno, "Uno Platform", string.Empty),
        _ => null,
    };

    private static string? ReadJsonString(string path, params string[] propertyPath)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path), JsonOptions);
            var element = document.RootElement;
            foreach (var property in propertyPath)
            {
                if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(property, out element))
                {
                    return null;
                }
            }

            return element.ValueKind == JsonValueKind.String ? element.GetString() : null;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string? ReadGoVersion(string path)
    {
        try
        {
            return File.ReadLines(path)
                .Select(line => line.Trim())
                .Where(line => line.StartsWith("go ", StringComparison.Ordinal))
                .Select(line => line[3..].Trim())
                .FirstOrDefault();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Lists files level by level, so evidence near the root is found first.</summary>
    private static IEnumerable<string> EnumerateFilesBreadthFirst(string root, int maxDepth)
    {
        var queue = new Queue<(string Directory, int Depth)>();
        queue.Enqueue((root, 0));
        var scanned = 0;

        while (queue.Count > 0)
        {
            var (directory, depth) = queue.Dequeue();
            List<string> files;
            List<string> subdirectories;
            try
            {
                files = Directory.EnumerateFiles(directory).Order(StringComparer.OrdinalIgnoreCase).ToList();
                subdirectories = Directory.EnumerateDirectories(directory).Order(StringComparer.OrdinalIgnoreCase).ToList();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            foreach (var file in files)
            {
                if (++scanned > MaxFilesScanned)
                {
                    yield break;
                }

                yield return file;
            }

            if (depth >= maxDepth)
            {
                continue;
            }

            foreach (var subdirectory in subdirectories)
            {
                if (!PathUtilities.IsIgnoredDirectory(Path.GetFileName(subdirectory)))
                {
                    queue.Enqueue((subdirectory, depth + 1));
                }
            }
        }
    }
}
