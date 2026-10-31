using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace TabStudio.Core.Workspaces;

/// <summary>The file format of a solution.</summary>
public enum SolutionFormat
{
    /// <summary>The classic text-based <c>.sln</c> format.</summary>
    Sln,

    /// <summary>The XML-based <c>.slnx</c> format introduced with Visual Studio 2022 17.13 and the .NET 9 SDK.</summary>
    Slnx,
}

/// <summary>
/// A project listed in a solution file.
/// </summary>
/// <param name="Name">The project name shown in the Solution Explorer.</param>
/// <param name="AbsolutePath">Full path of the project file.</param>
/// <param name="RelativePath">Path of the project file as written in the solution, with platform separators.</param>
/// <param name="FolderPath">The solution folder the project sits in, such as <c>src/Libraries</c>, or <see langword="null"/> at the root.</param>
public sealed record SolutionProjectEntry(string Name, string AbsolutePath, string RelativePath, string? FolderPath)
{
    /// <summary>
    /// Whether the solution's Debug configuration builds the project, as Visual Studio and <c>dotnet build</c> do: a
    /// <c>.sln</c> project mapped to it without <c>Build.0</c>, or a <c>.slnx</c> project with
    /// <c>&lt;Build Project="false" /&gt;</c>, is in the solution but not built.
    /// </summary>
    public bool IsBuilt { get; init; } = true;
}

/// <summary>
/// A parsed solution file.
/// </summary>
/// <param name="FilePath">Full path of the solution file.</param>
/// <param name="Format">Whether the solution is a <c>.sln</c> or a <c>.slnx</c>.</param>
/// <param name="Projects">The projects in the solution, in file order.</param>
public sealed record SolutionInfo(string FilePath, SolutionFormat Format, IReadOnlyList<SolutionProjectEntry> Projects)
{
    /// <summary>The solution name: the file name without its extension.</summary>
    public string Name => Path.GetFileNameWithoutExtension(FilePath);

    /// <summary>The directory that contains the solution file.</summary>
    public string DirectoryPath => Path.GetDirectoryName(FilePath) ?? string.Empty;
}

/// <summary>
/// Reads <c>.sln</c> and <c>.slnx</c> files.
/// </summary>
/// <remarks>
/// Only projects TabStudio can open are returned (see <see cref="ProjectParser.IsProjectFile"/>).
/// Solution folders are flattened into a <see cref="SolutionProjectEntry.FolderPath"/> on each project.
/// </remarks>
public static partial class SolutionParser
{
    private const string SolutionFolderTypeId = "2150E333-8FDC-42A3-9474-1A3956D46DE8";

    /// <summary>File extensions TabStudio treats as solution files.</summary>
    public static IReadOnlyList<string> SolutionExtensions { get; } = [".slnx", ".sln"];

    /// <summary>Whether a path looks like a solution file.</summary>
    /// <param name="path">A file path.</param>
    public static bool IsSolutionFile(string path) =>
        SolutionExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    /// <summary>Reads and parses a solution file from disk.</summary>
    /// <param name="filePath">Path of the <c>.sln</c> or <c>.slnx</c> file.</param>
    /// <exception cref="ProjectLoadException">The file does not exist or cannot be parsed.</exception>
    public static SolutionInfo Load(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var fullPath = Path.GetFullPath(filePath);
        if (!File.Exists(fullPath))
        {
            throw new ProjectLoadException(fullPath, "the file does not exist.", null);
        }

        return Parse(fullPath, PathUtilities.ReadAllTextBounded(fullPath));
    }

    /// <summary>Parses solution text that has already been read.</summary>
    /// <param name="filePath">The path the text came from. Its extension selects the format.</param>
    /// <param name="text">The contents of the solution file.</param>
    /// <exception cref="ProjectLoadException">The text cannot be parsed.</exception>
    public static SolutionInfo Parse(string filePath, string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(text);

        var directory = Path.GetDirectoryName(filePath) ?? string.Empty;
        return string.Equals(Path.GetExtension(filePath), ".slnx", StringComparison.OrdinalIgnoreCase)
            ? new SolutionInfo(filePath, SolutionFormat.Slnx, ParseSlnx(filePath, directory, text))
            : new SolutionInfo(filePath, SolutionFormat.Sln, ParseSln(directory, text));
    }

    private static List<SolutionProjectEntry> ParseSln(string directory, string text)
    {
        var entries = new List<(string Id, string TypeId, string Name, string Path)>();
        var parents = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var inNestedProjects = false;
        var solutionConfigurations = new List<string>();
        var mapped = new HashSet<(string Id, string Configuration)>();
        var built = new HashSet<(string Id, string Configuration)>();
        string? section = null;

        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.Trim();

            var project = SlnProjectLine().Match(line);
            if (project.Success)
            {
                entries.Add((
                    project.Groups["id"].Value,
                    project.Groups["type"].Value,
                    project.Groups["name"].Value,
                    project.Groups["path"].Value));
                continue;
            }

            if (line.StartsWith("GlobalSection(", StringComparison.OrdinalIgnoreCase))
            {
                section = line;
            }
            else if (line.StartsWith("EndGlobalSection", StringComparison.OrdinalIgnoreCase))
            {
                section = null;
            }
            else if (section is not null && line.Contains('=', StringComparison.Ordinal))
            {
                var key = line[..line.IndexOf('=', StringComparison.Ordinal)].Trim();
                if (section.StartsWith("GlobalSection(SolutionConfigurationPlatforms)", StringComparison.OrdinalIgnoreCase))
                {
                    solutionConfigurations.Add(key);
                }
                else if (section.StartsWith("GlobalSection(ProjectConfigurationPlatforms)", StringComparison.OrdinalIgnoreCase)
                    && key.IndexOf('.', StringComparison.Ordinal) is > 0 and var dot)
                {
                    // {id}.Debug|Any CPU.ActiveCfg, and {id}.Debug|Any CPU.Build.0 when the configuration builds it.
                    var id = key[..dot].Trim('{', '}');
                    var rest = key[(dot + 1)..];
                    if (rest.EndsWith(".ActiveCfg", StringComparison.OrdinalIgnoreCase))
                    {
                        mapped.Add((id, rest[..^".ActiveCfg".Length]));
                    }
                    else if (rest.EndsWith(".Build.0", StringComparison.OrdinalIgnoreCase))
                    {
                        built.Add((id, rest[..^".Build.0".Length]));
                    }
                }
            }

            if (line.StartsWith("GlobalSection(NestedProjects)", StringComparison.OrdinalIgnoreCase))
            {
                inNestedProjects = true;
                continue;
            }

            if (inNestedProjects)
            {
                if (line.StartsWith("EndGlobalSection", StringComparison.OrdinalIgnoreCase))
                {
                    inNestedProjects = false;
                    continue;
                }

                var nesting = SlnNestingLine().Match(line);
                if (nesting.Success)
                {
                    parents[nesting.Groups["child"].Value] = nesting.Groups["parent"].Value;
                }
            }
        }

        var folderNames = entries
            .Where(e => string.Equals(e.TypeId, SolutionFolderTypeId, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(e => e.Id, e => e.Name, StringComparer.OrdinalIgnoreCase);

        // The configuration Visual Studio opens a solution in: Debug|Any CPU, else its first Debug one, else its first.
        var configuration = solutionConfigurations.FirstOrDefault(c => c.Equals("Debug|Any CPU", StringComparison.OrdinalIgnoreCase))
            ?? solutionConfigurations.FirstOrDefault(c => c.StartsWith("Debug|", StringComparison.OrdinalIgnoreCase))
            ?? solutionConfigurations.FirstOrDefault();
        bool IsBuilt(string id)
        {
            var key = id.Trim('{', '}');
            return configuration is null
                || !mapped.Contains((key, configuration))
                || built.Contains((key, configuration));
        }

        return entries
            .Where(e => !folderNames.ContainsKey(e.Id) && ProjectParser.IsProjectFile(e.Path))
            .Select(e => new SolutionProjectEntry(
                e.Name,
                PathUtilities.ResolveRelative(directory, e.Path),
                PathUtilities.NormalizeSeparators(e.Path),
                BuildFolderPath(e.Id, parents, folderNames))
            {
                IsBuilt = IsBuilt(e.Id),
            })
            .ToList();
    }

    /// <summary>Walks the NestedProjects chain from a project up to the root and joins the folder names.</summary>
    private static string? BuildFolderPath(string id, Dictionary<string, string> parents, Dictionary<string, string> folderNames)
    {
        var segments = new List<string>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var current = id;

        while (parents.TryGetValue(current, out var parent) && visited.Add(parent) && folderNames.TryGetValue(parent, out var name))
        {
            segments.Insert(0, name);
            current = parent;
        }

        return segments.Count == 0 ? null : string.Join('/', segments);
    }

    private static List<SolutionProjectEntry> ParseSlnx(string filePath, string directory, string text)
    {
        XElement root;
        try
        {
            root = XDocument.Parse(text).Root ?? throw new ProjectLoadException(filePath, "the file is empty.", null);
        }
        catch (XmlException ex)
        {
            throw new ProjectLoadException(filePath, $"the XML is not valid ({ex.Message}).", ex);
        }

        var result = new List<SolutionProjectEntry>();
        foreach (var project in root.Descendants().Where(e => e.Name.LocalName == "Project"))
        {
            var path = project.Attribute("Path")?.Value;
            if (string.IsNullOrWhiteSpace(path) || !ProjectParser.IsProjectFile(path))
            {
                continue;
            }

            var folder = project.Ancestors()
                .FirstOrDefault(a => a.Name.LocalName == "Folder")?
                .Attribute("Name")?.Value
                .Trim('/');

            // <Build Project="false" />, for every configuration or for the Debug ones (Solution="Debug|*").
            var notBuilt = project.Elements().Any(e => e.Name.LocalName == "Build"
                && string.Equals(e.Attribute("Project")?.Value.Trim(), "false", StringComparison.OrdinalIgnoreCase)
                && (e.Attribute("Solution")?.Value.Trim() is not { Length: > 0 } solution
                    || solution.StartsWith("Debug|", StringComparison.OrdinalIgnoreCase)
                    || solution.StartsWith("*|", StringComparison.Ordinal)));
            var normalized = PathUtilities.NormalizeSeparators(path);
            result.Add(new SolutionProjectEntry(
                project.Attribute("DisplayName")?.Value ?? Path.GetFileNameWithoutExtension(normalized),
                PathUtilities.ResolveRelative(directory, path),
                normalized,
                string.IsNullOrEmpty(folder) ? null : folder)
            {
                IsBuilt = !notBuilt,
            });
        }

        return result;
    }

    [GeneratedRegex("""^Project\("\{(?<type>[0-9A-Fa-f\-]+)\}"\)\s*=\s*"(?<name>[^"]*)"\s*,\s*"(?<path>[^"]*)"\s*,\s*"\{(?<id>[0-9A-Fa-f\-]+)\}"\s*$""")]
    private static partial Regex SlnProjectLine();

    [GeneratedRegex(@"^\{(?<child>[0-9A-Fa-f\-]+)\}\s*=\s*\{(?<parent>[0-9A-Fa-f\-]+)\}$")]
    private static partial Regex SlnNestingLine();
}
