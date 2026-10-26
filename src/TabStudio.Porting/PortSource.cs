using TabStudio.Core.Workspaces;

namespace TabStudio.Porting;

/// <summary>
/// The project to port, read into memory: its settings and its files. The converter never writes to it.
/// </summary>
public sealed class PortSource
{
    /// <summary>File types copied to the new project as Avalonia resources, such as images and fonts.</summary>
    private static readonly HashSet<string> ResourceExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".ico", ".svg", ".ttf", ".otf", ".webp",
    };

    /// <summary>File types read as text, to convert or to copy.</summary>
    private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs", ".xaml", ".resx", ".config", ".json", ".txt", ".md", ".settings", ".manifest",
    };

    /// <summary>The project's name, such as <c>WpfApp1</c>.</summary>
    public required string Name { get; init; }

    /// <summary>Whether it is a WPF or a Windows Forms project.</summary>
    public required PortSourceKind Kind { get; init; }

    /// <summary>The project's folder, or <see langword="null"/> for a project that exists only in memory.</summary>
    public string? Directory { get; init; }

    /// <summary>The project's MSBuild properties, such as <c>Nullable</c> and <c>RootNamespace</c>.</summary>
    public IReadOnlyDictionary<string, string> Properties { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>The project's NuGet packages, which the new project keeps unless they are Windows-only.</summary>
    public IReadOnlyList<PackageReferenceInfo> Packages { get; init; } = [];

    /// <summary>The project's text files, relative to its folder with <c>/</c>, to their text.</summary>
    public IReadOnlyDictionary<string, string> Files { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>Other files to copy, such as images, relative to the folder with <c>/</c>.</summary>
    public IReadOnlyList<string> Resources { get; init; } = [];

    /// <summary>The namespace of the project's code: <c>RootNamespace</c>, or the project's name.</summary>
    public string RootNamespace =>
        Properties.TryGetValue("RootNamespace", out var root) && root.Length > 0 ? root : Name.Replace('-', '_').Replace(' ', '_');

    /// <summary>Whether the project enables nullable reference types.</summary>
    public bool NullableEnabled => IsEnabled("Nullable");

    /// <summary>Whether the project uses the SDK's implicit global usings.</summary>
    public bool ImplicitUsings => IsEnabled("ImplicitUsings");

    /// <summary>Reads a project from disk: its settings, its source files and its images.</summary>
    /// <param name="project">The project, which must be a WPF or Windows Forms project.</param>
    /// <param name="otherProjectDirectories">Folders of other projects of the solution, whose files are never read.</param>
    /// <exception cref="ArgumentException">The project is neither WPF nor Windows Forms.</exception>
    public static PortSource Load(ProjectInfo project, IEnumerable<string>? otherProjectDirectories = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        var kind = project.Kind switch
        {
            ProjectKind.Wpf => PortSourceKind.Wpf,
            ProjectKind.WinForms => PortSourceKind.WindowsForms,
            _ => throw new ArgumentException($"{project.Name} is not a WPF or Windows Forms project.", nameof(project)),
        };

        var root = project.DirectoryPath;
        var excluded = (otherProjectDirectories ?? [])
            .Where(d => !string.Equals(d, root, StringComparison.Ordinal))
            .Select(d => d.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar)
            .ToList();
        var files = new SortedDictionary<string, string>(StringComparer.Ordinal);
        var resources = new List<string>();
        foreach (var file in EnumerateFiles(root, excluded))
        {
            var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
            var extension = Path.GetExtension(file);
            if (TextExtensions.Contains(extension))
            {
                files[relative] = File.ReadAllText(file);
            }
            else if (ResourceExtensions.Contains(extension))
            {
                resources.Add(relative);
            }
        }

        resources.Sort(StringComparer.Ordinal);
        return new PortSource
        {
            Name = project.Name,
            Kind = kind,
            Directory = root,
            Properties = project.Properties,
            Packages = project.PackageReferences,
            Files = files,
            Resources = resources,
        };
    }

    /// <summary>The value of a property, or <see langword="null"/>.</summary>
    /// <param name="name">The property, such as <c>OutputType</c>.</param>
    public string? GetProperty(string name) => Properties.TryGetValue(name, out var value) ? value : null;

    private bool IsEnabled(string property) =>
        GetProperty(property) is { } value && (value.Equals("enable", StringComparison.OrdinalIgnoreCase) || value.Equals("true", StringComparison.OrdinalIgnoreCase));

    /// <summary>The project's files, without build output, hidden folders and other projects' folders.</summary>
    private static IEnumerable<string> EnumerateFiles(string root, List<string> excluded)
    {
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var directory = pending.Pop();
            foreach (var file in System.IO.Directory.EnumerateFiles(directory))
            {
                yield return file;
            }

            foreach (var child in System.IO.Directory.EnumerateDirectories(directory))
            {
                var name = Path.GetFileName(child);
                var path = child + Path.DirectorySeparatorChar;
                if (name is "bin" or "obj" || name.StartsWith('.') || excluded.Any(e => path.StartsWith(e, StringComparison.Ordinal)))
                {
                    continue;
                }

                pending.Push(child);
            }
        }
    }
}
