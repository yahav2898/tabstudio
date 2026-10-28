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

    /// <summary>The MSBuild files that set properties for the projects under them, which only .NET repositories have.</summary>
    private static readonly HashSet<string> BuildPropertyFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        "Directory.Build.props", "Directory.Build.targets", "Directory.Packages.props",
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
        StackComponent? buildProperties = null;
        var nodeServesPages = false;

        foreach (var file in EnumerateFilesBreadthFirst(rootDirectory, maxDepth))
        {
            var relative = Path.GetRelativePath(rootDirectory, file);
            foreach (var component in InspectFile(file, relative))
            {
                found.TryAdd(component.Kind, component);
            }

            var name = Path.GetFileName(file);
            if (BuildPropertyFiles.Contains(name))
            {
                buildProperties ??= new StackComponent(StackKind.DotNet, ".NET", relative);
            }
            else if (!nodeServesPages && name.Equals("package.json", StringComparison.OrdinalIgnoreCase))
            {
                nodeServesPages = StaticSiteDetector.IsRunByNode(file);
            }
        }

        // Directory.Build.props is .NET too, but a solution or project names the repository better.
        if (buildProperties is not null)
        {
            found.TryAdd(StackKind.DotNet, buildProperties);
        }

        // An index.html is the startup item only when no .NET project or Node.js package serves the pages itself.
        if (!found.ContainsKey(StackKind.DotNet) && !found.ContainsKey(StackKind.Php) && !nodeServesPages && StaticSiteDetector.FindIndex(rootDirectory) is { } index)
        {
            found.TryAdd(StackKind.StaticSite, new StackComponent(StackKind.StaticSite, "Static web site", Path.GetRelativePath(rootDirectory, index)));
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
                if (File.Exists(Path.Combine(Path.GetDirectoryName(fullPath)!, "memory.x")))
                {
                    yield return new StackComponent(StackKind.EmbeddedRust, "Embedded Rust", relativePath);
                }
                yield break;
            case "GO.MOD":
                yield return new StackComponent(StackKind.Go, "Go", relativePath, ReadGoVersion(fullPath));
                yield break;
            case "CMAKELISTS.TXT":
                yield return new StackComponent(StackKind.Cpp, "C++", relativePath);
                if (EmbeddedCMakeKind(fullPath) is { } framework)
                {
                    yield return new StackComponent(framework.Kind, framework.Name, relativePath);
                }
                yield break;
            case "PLATFORMIO.INI":
                yield return new StackComponent(StackKind.PlatformIO, "PlatformIO", relativePath);
                yield break;
            case "LIBRARY.PROPERTIES":
                yield return new StackComponent(StackKind.Arduino, "Arduino", relativePath);
                yield break;
            case ".MICROPYTHON":
                yield return new StackComponent(StackKind.MicroPython, "MicroPython / CircuitPython", relativePath);
                yield break;
            case "TABSTUDIO-GCC.CMAKE":
                yield return new StackComponent(StackKind.EmbeddedVendor, "Embedded GCC import", relativePath);
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
            case "COMPOSER.JSON":
                foreach (var component in InspectComposerJson(fullPath, relativePath))
                {
                    yield return component;
                }

                yield break;
            case "PACKAGE.SWIFT":
                yield return new StackComponent(StackKind.Swift, "Swift", relativePath, ReadSwiftToolsVersion(fullPath));
                if (FileContains(fullPath, "vapor/vapor"))
                {
                    yield return new StackComponent(StackKind.Vapor, "Vapor", relativePath);
                }

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
            case ".INO":
                yield return new StackComponent(StackKind.Arduino, "Arduino", relativePath);
                break;
            case ".IOC":
                yield return new StackComponent(StackKind.Stm32Cube, "STM32Cube", relativePath);
                break;
            case ".UVPROJX" or ".EWP" or ".CPROJ":
                yield return new StackComponent(StackKind.EmbeddedVendor, "Embedded vendor project", relativePath);
                break;
            case ".SLN" or ".SLNX" or ".CSPROJ" or ".FSPROJ":
                yield return new StackComponent(StackKind.DotNet, ".NET", relativePath);
                break;
            case ".VBPROJ":
                yield return new StackComponent(StackKind.DotNet, ".NET", relativePath);
                yield return new StackComponent(StackKind.VisualBasic, "Visual Basic", relativePath);
                break;
            case ".PHP":
                yield return new StackComponent(StackKind.Php, "PHP", relativePath);
                break;
            case ".SWIFT":
                yield return new StackComponent(StackKind.Swift, "Swift", relativePath);
                break;
            case ".KT":
                yield return new StackComponent(StackKind.Kotlin, "Kotlin", relativePath);
                break;
            case ".VCXPROJ":
                yield return new StackComponent(StackKind.Cpp, "C++", relativePath);
                break;
            case ".SOL":
                yield return new StackComponent(StackKind.Solidity, "Solidity", relativePath);
                break;
            case ".PY":
                yield return new StackComponent(StackKind.Python, "Python", relativePath);
                break;
        }
    }

    private static (StackKind Kind, string Name)? EmbeddedCMakeKind(string path)
    {
        try
        {
            var info = new FileInfo(path);
            if (info.Length > 2 * 1024 * 1024 || (info.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                return null;
            }
            var text = string.Join('\n', File.ReadLines(path).Where(line => !line.TrimStart().StartsWith('#')));
            if (text.Contains("$ENV{IDF_PATH}", StringComparison.Ordinal) || text.Contains("idf.cmake", StringComparison.Ordinal))
            {
                return (StackKind.EspIdf, "ESP-IDF");
            }
            if (text.Contains("find_package(Zephyr", StringComparison.Ordinal)
                || text.Contains("find_package (Zephyr", StringComparison.Ordinal))
            {
                return (StackKind.Zephyr, "Zephyr");
            }
            if (text.Contains("pico_sdk_import.cmake", StringComparison.Ordinal) || text.Contains("pico_sdk_init(", StringComparison.Ordinal))
            {
                return (StackKind.PicoSdk, "Pico SDK");
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Detection stays available when a manifest vanishes while the workspace opens.
        }
        return null;
    }

    private static List<StackComponent> InspectPackageJson(string fullPath, string relativePath)
    {
        var result = new List<StackComponent> { new(StackKind.Node, "Node.js", relativePath) };

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(fullPath), JsonOptions);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return result;
            }

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

    /// <summary>PHP from a <c>composer.json</c>, with its PHP version constraint, and the framework it requires.</summary>
    private static IEnumerable<StackComponent> InspectComposerJson(string path, string relativePath)
    {
        yield return new StackComponent(StackKind.Php, "PHP", relativePath, ReadJsonString(path, "require", "php"));
        foreach (var (package, kind, name) in PhpFrameworks)
        {
            if (ReadJsonString(path, "require", package) is { } version)
            {
                yield return new StackComponent(kind, name, relativePath, version);
            }
        }
    }

    private static readonly (string Package, StackKind Kind, string Name)[] PhpFrameworks =
    [
        ("laravel/framework", StackKind.Laravel, "Laravel"),
        ("symfony/framework-bundle", StackKind.Symfony, "Symfony"),
        ("slim/slim", StackKind.Slim, "Slim"),
    ];

    /// <summary>The <c>swift-tools-version</c> a <c>Package.swift</c> declares on its first line, or <see langword="null"/>.</summary>
    private static string? ReadSwiftToolsVersion(string path)
    {
        try
        {
            var first = File.ReadLines(path).FirstOrDefault() ?? string.Empty;
            var marker = first.IndexOf("swift-tools-version:", StringComparison.Ordinal);
            return marker < 0 ? null : first[(marker + "swift-tools-version:".Length)..].Trim();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static bool FileContains(string path, string text)
    {
        try
        {
            return File.ReadAllText(path).Contains(text, StringComparison.Ordinal);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

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
                if (!PathUtilities.IsIgnoredDirectory(Path.GetFileName(subdirectory))
                    && (File.GetAttributes(subdirectory) & FileAttributes.ReparsePoint) == 0)
                {
                    queue.Enqueue((subdirectory, depth + 1));
                }
            }
        }
    }
}
