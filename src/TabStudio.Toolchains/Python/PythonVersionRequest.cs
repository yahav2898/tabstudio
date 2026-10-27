using System.Text.RegularExpressions;

namespace TabStudio.Toolchains.Python;

/// <summary>The Python version a project asks for, and the file that asks for it.</summary>
/// <param name="Range">The versions it accepts.</param>
/// <param name="Source">The file, relative to the project folder, such as <c>.python-version</c> or <c>Dockerfile</c>.</param>
public sealed partial record PythonVersionRequest(PythonVersionRange Range, string Source)
{
    /// <summary>
    /// Reads the Python version a project asks for, from the first of these that names one: <c>.python-version</c>
    /// (pyenv and uv), <c>runtime.txt</c> (Heroku), <c>pyproject.toml</c>'s <c>requires-python</c> or Poetry's
    /// <c>python</c> dependency, <c>Pipfile</c>'s <c>[requires]</c>, and the Python image a <c>Dockerfile</c>
    /// starts <c>FROM</c>.
    /// </summary>
    /// <param name="projectDirectory">The project folder.</param>
    /// <returns>The request, or <see langword="null"/> when the project does not name a Python 3 version.</returns>
    public static PythonVersionRequest? Find(string projectDirectory)
    {
        ArgumentNullException.ThrowIfNull(projectDirectory);
        return FromFile(projectDirectory, ".python-version", PythonVersionFile)
            ?? FromFile(projectDirectory, "runtime.txt", RuntimeTxt)
            ?? FromFile(projectDirectory, "pyproject.toml", PyProject)
            ?? FromFile(projectDirectory, "Pipfile", Pipfile)
            ?? FromFile(projectDirectory, "Dockerfile", Dockerfile);
    }

    /// <summary>The version in a <c>.python-version</c> file: its first version line, such as <c>3.11.4</c>.</summary>
    /// <param name="text">The file's text.</param>
    public static PythonVersionRange? PythonVersionFile(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        foreach (var line in Lines(text))
        {
            // pyenv also accepts other interpreters, such as pypy3.10, and names of virtual environments.
            if (PlainVersion().Match(line) is { Success: true } match)
            {
                return PythonVersionRange.Parse(match.Groups["version"].Value);
            }
        }

        return null;
    }

    /// <summary>The version in Heroku's <c>runtime.txt</c>, such as <c>python-3.11.4</c>.</summary>
    /// <param name="text">The file's text.</param>
    public static PythonVersionRange? RuntimeTxt(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return Lines(text).Select(l => RuntimeVersion().Match(l)).FirstOrDefault(m => m.Success) is { } match
            ? PythonVersionRange.Parse(match.Groups["version"].Value)
            : null;
    }

    /// <summary>
    /// The versions in a <c>pyproject.toml</c>: <c>requires-python</c> in <c>[project]</c>, or <c>python</c> in
    /// Poetry's <c>[tool.poetry.dependencies]</c>.
    /// </summary>
    /// <param name="text">The file's text.</param>
    public static PythonVersionRange? PyProject(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return TomlValue(text, "project", "requires-python") is { } requires
            ? PythonVersionRange.Parse(requires)
            : TomlValue(text, "tool.poetry.dependencies", "python") is { } poetry ? PythonVersionRange.Parse(poetry) : null;
    }

    /// <summary>The version in a <c>Pipfile</c>'s <c>[requires]</c>: <c>python_full_version</c> or <c>python_version</c>.</summary>
    /// <param name="text">The file's text.</param>
    public static PythonVersionRange? Pipfile(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return PythonVersionRange.Parse(TomlValue(text, "requires", "python_full_version") ?? TomlValue(text, "requires", "python_version"));
    }

    /// <summary>
    /// The version of the official Python image a <c>Dockerfile</c> builds from, such as <c>3.11</c> in
    /// <c>FROM python:3.11-slim AS base</c>; tags without a minor version, such as <c>slim</c> or <c>3</c>, name none.
    /// </summary>
    /// <param name="text">The file's text.</param>
    public static PythonVersionRange? Dockerfile(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        foreach (var line in Lines(text))
        {
            if (DockerFrom().Match(line) is { Success: true } match)
            {
                return match.Groups["version"].Success ? PythonVersionRange.Parse(match.Groups["version"].Value) : null;
            }
        }

        return null;
    }

    private static PythonVersionRequest? FromFile(string directory, string name, Func<string, PythonVersionRange?> read)
    {
        var path = Path.Combine(directory, name);
        try
        {
            if (!File.Exists(path) || new FileInfo(path).Length > 1024 * 1024)
            {
                return null;
            }

            return read(File.ReadAllText(path)) is { } range ? new PythonVersionRequest(range, name) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static IEnumerable<string> Lines(string text) =>
        text.Split('\n').Select(l => l.Split('#', 2)[0].Trim()).Where(l => l.Length > 0);

    /// <summary>A string value in a TOML table, such as <c>requires-python = "&gt;=3.9"</c> under <c>[project]</c>.</summary>
    internal static string? TomlValue(string text, string table, string key)
    {
        var inTable = false;
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (line.StartsWith('['))
            {
                inTable = TomlTable().Match(line) is { Success: true } header && header.Groups["name"].Value.Replace(" ", string.Empty, StringComparison.Ordinal) == table;
                continue;
            }

            if (inTable && TomlString().Match(line) is { Success: true } pair && pair.Groups["key"].Value.Trim('"', '\'') == key)
            {
                return pair.Groups["value"].Value;
            }
        }

        return null;
    }

    [GeneratedRegex(@"^(cpython-|python)?(?<version>3(\.\d+){1,2}t?)(\s|$|/)", RegexOptions.CultureInvariant)]
    private static partial Regex PlainVersion();

    [GeneratedRegex(@"^python-(?<version>3(\.\d+){1,2})", RegexOptions.CultureInvariant)]
    private static partial Regex RuntimeVersion();

    [GeneratedRegex(@"^FROM\s+(--\S+\s+)*(\S+/)?(library/)?python:(?<version>3\.\d+(\.\d+)?)?\S*(\s|$)", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex DockerFrom();

    [GeneratedRegex(@"^\[\s*(?<name>[^\[\]]+?)\s*\]", RegexOptions.CultureInvariant)]
    private static partial Regex TomlTable();

    [GeneratedRegex("""^(?<key>[A-Za-z0-9_.-]+|"[^"]+"|'[^']+')\s*=\s*(?:"(?<value>[^"]*)"|'(?<value>[^']*)')""", RegexOptions.CultureInvariant)]
    private static partial Regex TomlString();
}
