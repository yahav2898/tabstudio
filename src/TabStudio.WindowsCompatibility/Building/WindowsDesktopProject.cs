using System.Xml.Linq;
using TabStudio.Core.Workspaces;

namespace TabStudio.WindowsCompatibility.Building;

/// <summary>
/// A Windows Forms or WPF project as the Windows Desktop SDK (Microsoft.NET.Sdk.WindowsDesktop) sees it: which
/// frameworks it uses, its XAML items, and what its build is called and produces.
/// </summary>
public sealed class WindowsDesktopProject
{
    private WindowsDesktopProject(ProjectInfo project, bool usesWpf, bool usesWindowsForms)
    {
        Project = project;
        UsesWpf = usesWpf;
        UsesWindowsForms = usesWindowsForms;
        AssemblyName = project.GetProperty("AssemblyName") is { Length: > 0 } assembly ? assembly : project.Name;
        RootNamespace = project.GetProperty("RootNamespace") is { Length: > 0 } ns ? ns : AssemblyName.Replace(' ', '_');
        OutputType = project.OutputType is { Length: > 0 } type ? type : "Library";
        TargetFramework = project.TargetFrameworks.FirstOrDefault(f => f.Contains("-windows", StringComparison.OrdinalIgnoreCase))
            ?? project.TargetFrameworks.FirstOrDefault() ?? "net10.0-windows";
    }

    /// <summary>The project.</summary>
    public ProjectInfo Project { get; }

    /// <summary>Whether it sets <c>UseWPF</c>.</summary>
    public bool UsesWpf { get; }

    /// <summary>Whether it sets <c>UseWindowsForms</c>.</summary>
    public bool UsesWindowsForms { get; }

    /// <summary>The assembly's name.</summary>
    public string AssemblyName { get; }

    /// <summary>The root namespace, which WPF's generated code and resources use.</summary>
    public string RootNamespace { get; }

    /// <summary>The output type, such as <c>WinExe</c>.</summary>
    public string OutputType { get; }

    /// <summary>Whether it builds a program rather than a library.</summary>
    public bool IsApp => Project.IsExecutable;

    /// <summary>The target framework the build uses, such as <c>net10.0-windows</c>.</summary>
    public string TargetFramework { get; }

    /// <summary>The project's folder.</summary>
    public string Directory => Project.DirectoryPath;

    /// <summary>
    /// The Windows Desktop project a project is, or <see langword="null"/> when it uses neither Windows Forms nor WPF.
    /// </summary>
    /// <param name="project">The project.</param>
    public static WindowsDesktopProject? From(ProjectInfo project)
    {
        ArgumentNullException.ThrowIfNull(project);
        var wpf = IsTrue(project.GetProperty("UseWPF"));
        var forms = IsTrue(project.GetProperty("UseWindowsForms"));
        return wpf || forms ? new WindowsDesktopProject(project, wpf, forms) : null;
    }

    /// <summary>The intermediate folder of a configuration, relative to the project, as the SDK names it: <c>obj/Debug/net10.0-windows/</c>.</summary>
    /// <param name="configuration">The configuration, such as <c>Debug</c>.</param>
    public string IntermediateFolder(string configuration) => $"obj/{configuration}/{TargetFramework}/";

    /// <summary>The output folder of a configuration, as a full path: <c>bin/Debug/net10.0-windows</c> in the project's folder.</summary>
    /// <param name="configuration">The configuration, such as <c>Debug</c>.</param>
    public string OutputFolder(string configuration) => Path.Combine(Directory, "bin", configuration, TargetFramework);

    /// <summary>The XAML of a WPF project, relative to its folder: its application definition, pages and resources.</summary>
    public WpfItems ReadWpfItems()
    {
        if (!UsesWpf)
        {
            return new WpfItems(null, [], []);
        }

        var root = LoadProject();
        var defaults = !IsFalse(Project.GetProperty("EnableDefaultItems"));
        var application = Explicit(root, "ApplicationDefinition", "Include").FirstOrDefault();
        if (application is null && defaults && !IsFalse(Project.GetProperty("EnableDefaultApplicationDefinition")))
        {
            application = new[] { "App.xaml", "Application.xaml" }.FirstOrDefault(name => File.Exists(Path.Combine(Directory, name)));
        }

        var pages = new List<string>();
        if (defaults && !IsFalse(Project.GetProperty("EnableDefaultPageItems")))
        {
            pages.AddRange(ProjectSourceFiles.EnumerateItems(Project, ["**/*.xaml"], Explicit(root, "Page", "Remove")).Select(Relative));
        }

        pages.AddRange(ProjectSourceFiles.EnumerateItems(Project, Explicit(root, "Page", "Include"), []).Select(Relative));
        var resources = ProjectSourceFiles.EnumerateItems(Project, Explicit(root, "Resource", "Include"), Explicit(root, "Resource", "Remove")).Select(Relative).ToList();
        return new WpfItems(
            application,
            [.. pages.Where(p => !string.Equals(p, application, StringComparison.OrdinalIgnoreCase)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)],
            [.. resources.Distinct(StringComparer.Ordinal)]);
    }

    private XElement LoadProject()
    {
        try
        {
            return XDocument.Load(Project.FilePath).Root ?? new XElement("Project");
        }
        catch (Exception ex) when (ex is IOException or System.Xml.XmlException or UnauthorizedAccessException)
        {
            return new XElement("Project");
        }
    }

    private string Relative(string path) => Path.GetRelativePath(Directory, path).Replace('\\', '/');

    private static List<string> Explicit(XElement root, string item, string attribute) =>
        root.Descendants()
            .Where(e => e.Name.LocalName == item && e.Attribute(attribute) is not null)
            .SelectMany(e => e.Attribute(attribute)!.Value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Select(p => p.Replace('\\', '/'))
            .ToList();

    private static bool IsTrue(string? value) => string.Equals(value?.Trim(), "true", StringComparison.OrdinalIgnoreCase);

    private static bool IsFalse(string? value) => string.Equals(value?.Trim(), "false", StringComparison.OrdinalIgnoreCase);
}

/// <summary>A WPF project's XAML and resources, relative to its folder.</summary>
/// <param name="ApplicationDefinition">The application definition, such as <c>App.xaml</c>, if it has one.</param>
/// <param name="Pages">The other XAML files.</param>
/// <param name="Resources">The <c>Resource</c> items, such as images.</param>
public sealed record WpfItems(string? ApplicationDefinition, IReadOnlyList<string> Pages, IReadOnlyList<string> Resources);
