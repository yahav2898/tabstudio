using System.Text.RegularExpressions;

namespace TabStudio.Toolchains.Python;

/// <summary>
/// What a Python project depends on, from every file that records it: the requirements files, <c>pyproject.toml</c>
/// (PEP 621, PEP 735 dependency groups and Poetry), <c>Pipfile</c>, and the lock files <c>poetry.lock</c> and
/// <c>uv.lock</c>, which pin every package, the project's own dependencies' dependencies included.
/// </summary>
public static partial class PythonDependencies
{
    /// <summary>The lock files that pin a project's packages, as <c>[[package]]</c> tables with a name and a version.</summary>
    public static IReadOnlyList<string> LockFiles { get; } = ["poetry.lock", "uv.lock"];

    private const int MaxFileSize = 8 * 1024 * 1024;

    /// <summary>
    /// The packages a project's lock files pin (<see cref="LockFiles"/>), from the package index; packages from a
    /// folder, a Git repository or a URL, the project itself, and Poetry's development packages
    /// (<c>category = "dev"</c>) are left out.
    /// </summary>
    /// <param name="projectDirectory">The project folder.</param>
    public static IReadOnlyList<PythonPin> LockPins(string projectDirectory)
    {
        ArgumentNullException.ThrowIfNull(projectDirectory);
        return [.. LockFiles.Select(f => Read(Path.Combine(projectDirectory, f))).OfType<string>().SelectMany(text => LockPinsOf(text)).DistinctBy(p => p.Name)];
    }

    /// <summary>
    /// Every package a project pins to one version: those its requirements manifest pins with <c>==</c>, when the
    /// install reads one, and then those its lock files record (<see cref="LockPins"/>). A package both name keeps the
    /// manifest's version, which pip installs.
    /// </summary>
    /// <param name="projectDirectory">The project folder.</param>
    /// <param name="manifest">The manifest the install reads, such as <c>requirements.txt</c> or <c>pyproject.toml</c>; only a <c>.txt</c> one is read for pins.</param>
    public static IReadOnlyList<PythonPin> Pins(string projectDirectory, string manifest)
    {
        ArgumentNullException.ThrowIfNull(projectDirectory);
        ArgumentNullException.ThrowIfNull(manifest);
        IReadOnlyList<PythonPin> requirements = manifest.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) ? PythonRequirements.Pins(manifest) : [];
        return [.. requirements.Concat(LockPins(projectDirectory)).DistinctBy(p => p.Name)];
    }

    /// <summary>The packages a <c>poetry.lock</c> or <c>uv.lock</c> file's text pins, as <see cref="LockPins"/> reads them.</summary>
    /// <param name="text">The lock file's text.</param>
    public static IReadOnlyList<PythonPin> LockPinsOf(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var result = new List<PythonPin>();

        // The table the line is in: "package" for a [[package]]'s own keys, "package.source" for poetry.lock's
        // [package.source], "package.other" for its other subtables (dependencies, extras, metadata), whose keys
        // are not the package's, and null outside packages.
        string? table = null;
        string? name = null;
        string? version = null;
        var local = false;
        var development = false;
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (line.StartsWith('['))
            {
                var header = Header(line);
                if (table is not null && header is not null && header.StartsWith("package.", StringComparison.Ordinal))
                {
                    table = string.Equals(header, "package.source", StringComparison.Ordinal) ? header : "package.other";
                    continue;
                }

                Add();
                table = line.StartsWith("[[", StringComparison.Ordinal) && string.Equals(header, "package", StringComparison.Ordinal) ? "package" : null;
                continue;
            }

            if (table is null or "package.other")
            {
                continue;
            }

            if (TomlString().Match(line) is not { Success: true } pair)
            {
                if (string.Equals(table, "package", StringComparison.Ordinal) && LocalSource().IsMatch(line))
                {
                    // uv.lock: source = { editable = "." }, { virtual = "." }, { git = "…" }, { path = "…" }, { url = "…" }.
                    local = true;
                }

                continue;
            }

            var key = pair.Groups["key"].Value;
            var value = pair.Groups["value"].Value;
            if (string.Equals(table, "package.source", StringComparison.Ordinal))
            {
                // poetry.lock: [package.source] type = "git", "directory", "file" or "url"; "legacy" is an index.
                local |= string.Equals(key, "type", StringComparison.Ordinal) && value is "git" or "directory" or "file" or "url";
            }
            else if (string.Equals(key, "name", StringComparison.Ordinal))
            {
                name = value;
            }
            else if (string.Equals(key, "version", StringComparison.Ordinal))
            {
                version = value;
            }
            else if (string.Equals(key, "category", StringComparison.Ordinal))
            {
                // Poetry 1's development packages, which installing the project leaves out.
                development = string.Equals(value, "dev", StringComparison.Ordinal);
            }
        }

        Add();
        return result;

        void Add()
        {
            if (name is { Length: > 0 } && version is { Length: > 0 } && !local && !development)
            {
                result.Add(new PythonPin(PythonRequirements.Normalize(name), version));
            }

            name = null;
            version = null;
            local = false;
            development = false;
        }
    }

    /// <summary>
    /// Whether a project depends on a package: one of its requirements files (<c>requirements*.txt</c> and
    /// <c>requirements/*.txt</c>), <c>pyproject.toml</c>, <c>Pipfile</c> or lock files names it.
    /// </summary>
    /// <param name="projectDirectory">The project folder.</param>
    /// <param name="package">The package, such as <c>uvicorn</c>; compared as PyPI compares names.</param>
    public static bool DependsOn(string projectDirectory, string package)
    {
        ArgumentNullException.ThrowIfNull(projectDirectory);
        ArgumentNullException.ThrowIfNull(package);
        return Names(projectDirectory).Contains(PythonRequirements.Normalize(package));
    }

    /// <summary>The normalized names of every package a project's manifests and lock files name, as <see cref="DependsOn"/> reads them.</summary>
    /// <param name="projectDirectory">The project folder.</param>
    public static IReadOnlySet<string> Names(string projectDirectory)
    {
        ArgumentNullException.ThrowIfNull(projectDirectory);
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in RequirementsFiles(projectDirectory))
        {
            if (Read(file) is { } text)
            {
                names.UnionWith(PythonRequirements.NamesOf(text));
            }
        }

        if (Read(Path.Combine(projectDirectory, "pyproject.toml")) is { } pyproject)
        {
            names.UnionWith(PyProjectNamesOf(pyproject));
        }

        if (Read(Path.Combine(projectDirectory, "Pipfile")) is { } pipfile)
        {
            names.UnionWith(PipfileNamesOf(pipfile));
        }

        foreach (var lockFile in LockFiles)
        {
            if (Read(Path.Combine(projectDirectory, lockFile)) is { } text)
            {
                names.UnionWith(LockPinsOf(text).Select(p => p.Name));
            }
        }

        return names;
    }

    /// <summary>
    /// The packages a <c>pyproject.toml</c>'s text names: PEP 621's <c>dependencies</c> and
    /// <c>optional-dependencies</c>, PEP 735's <c>[dependency-groups]</c>, uv's <c>dev-dependencies</c>, and the
    /// keys of Poetry's dependency tables (<c>python</c> aside).
    /// </summary>
    /// <param name="text">The file's text.</param>
    public static IReadOnlyList<string> PyProjectNamesOf(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var names = new List<string>();
        string? table = null;
        var inArray = false;
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (!inArray && line.StartsWith('['))
            {
                table = Header(line);
                continue;
            }

            if (table is null)
            {
                continue;
            }

            if (IsPoetryDependencies(table))
            {
                if (!inArray && TomlKey().Match(line) is { Success: true } key && !string.Equals(key.Groups["key"].Value, "python", StringComparison.Ordinal))
                {
                    names.Add(PythonRequirements.Normalize(key.Groups["key"].Value));
                }

                continue;
            }

            var lists = table is "project.optional-dependencies" or "dependency-groups"
                || (string.Equals(table, "project", StringComparison.Ordinal) && ArrayStart().Match(line) is { Success: true } projectKey && string.Equals(projectKey.Groups["key"].Value, "dependencies", StringComparison.Ordinal))
                || (string.Equals(table, "tool.uv", StringComparison.Ordinal) && ArrayStart().Match(line) is { Success: true } uvKey && string.Equals(uvKey.Groups["key"].Value, "dev-dependencies", StringComparison.Ordinal));
            if (!inArray && !(lists && ArrayStart().IsMatch(line)))
            {
                continue;
            }

            var start = inArray ? 0 : line.IndexOf('[', StringComparison.Ordinal) + 1;
            var rest = line[start..];
            foreach (Match quoted in Quoted().Matches(rest))
            {
                if (RequirementName().Match(quoted.Groups["value"].Value.Trim()) is { Success: true } requirement)
                {
                    names.Add(PythonRequirements.Normalize(requirement.Value));
                }
            }

            // Extras such as uvicorn[standard] are inside the strings, so only a bracket outside them ends the array.
            inArray = !Quoted().Replace(rest, string.Empty).Contains(']', StringComparison.Ordinal);
        }

        return [.. names.Distinct(StringComparer.Ordinal)];
    }

    /// <summary>The packages a <c>Pipfile</c>'s text names: the keys of <c>[packages]</c> and <c>[dev-packages]</c>.</summary>
    /// <param name="text">The file's text.</param>
    public static IReadOnlyList<string> PipfileNamesOf(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var names = new List<string>();
        string? table = null;
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (line.StartsWith('['))
            {
                table = Header(line);
                continue;
            }

            if (table is "packages" or "dev-packages" && TomlKey().Match(line) is { Success: true } key)
            {
                names.Add(PythonRequirements.Normalize(key.Groups["key"].Value));
            }
        }

        return [.. names.Distinct(StringComparer.Ordinal)];
    }

    /// <summary>Poetry's dependency tables: <c>tool.poetry.dependencies</c>, <c>tool.poetry.dev-dependencies</c> and <c>tool.poetry.group.*.dependencies</c>.</summary>
    private static bool IsPoetryDependencies(string table) =>
        table is "tool.poetry.dependencies" or "tool.poetry.dev-dependencies"
        || (table.StartsWith("tool.poetry.group.", StringComparison.Ordinal) && table.EndsWith(".dependencies", StringComparison.Ordinal));

    private static string[] RequirementsFiles(string projectDirectory)
    {
        try
        {
            var top = Directory.EnumerateFiles(projectDirectory, "requirements*.txt");
            var folder = Path.Combine(projectDirectory, "requirements");
            var nested = Directory.Exists(folder) ? Directory.EnumerateFiles(folder, "*.txt") : Enumerable.Empty<string>();
            return [.. top.Concat(nested).Order(StringComparer.Ordinal)];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    /// <summary>A table header's name without spaces, such as <c>package.source</c> for <c>[package.source]</c> or <c>package</c> for <c>[[package]]</c>.</summary>
    private static string? Header(string line) =>
        TableHeader().Match(line) is { Success: true } header ? header.Groups["name"].Value.Replace(" ", string.Empty, StringComparison.Ordinal) : null;

    private static string? Read(string path)
    {
        try
        {
            var info = new FileInfo(path);
            return info.Exists && info.Length <= MaxFileSize ? File.ReadAllText(path) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    [GeneratedRegex(@"^\[\[?\s*(?<name>[^\[\]]+?)\s*\]\]?", RegexOptions.CultureInvariant)]
    private static partial Regex TableHeader();

    [GeneratedRegex("""^(?<key>[A-Za-z0-9_.-]+)\s*=\s*(?:"(?<value>[^"]*)"|'(?<value>[^']*)')""", RegexOptions.CultureInvariant)]
    private static partial Regex TomlString();

    [GeneratedRegex("""^(?:"(?<key>[A-Za-z0-9][A-Za-z0-9._-]*)"|'(?<key>[A-Za-z0-9][A-Za-z0-9._-]*)'|(?<key>[A-Za-z0-9][A-Za-z0-9._-]*))\s*=""", RegexOptions.CultureInvariant)]
    private static partial Regex TomlKey();

    [GeneratedRegex("""^(?:"(?<key>[^"]+)"|'(?<key>[^']+)'|(?<key>[A-Za-z0-9_.-]+))\s*=\s*\[""", RegexOptions.CultureInvariant)]
    private static partial Regex ArrayStart();

    [GeneratedRegex("""^source\s*=\s*\{[^}]*\b(?:editable|virtual|git|path|directory|url)\s*=""", RegexOptions.CultureInvariant)]
    private static partial Regex LocalSource();

    [GeneratedRegex("""(?:"(?<value>[^"]*)"|'(?<value>[^']*)')""", RegexOptions.CultureInvariant)]
    private static partial Regex Quoted();

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9._-]*", RegexOptions.CultureInvariant)]
    private static partial Regex RequirementName();
}
