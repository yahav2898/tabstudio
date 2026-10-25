using TabStudio.Core.Workspaces;
using TabStudio.Porting.Output;
using TabStudio.Porting.Wpf;

namespace TabStudio.Porting;

/// <summary>
/// Ports WPF and Windows Forms projects to Avalonia: a deterministic converter rewrites everything that maps
/// exactly, and lists the rest as port notes. The original project is never changed.
/// </summary>
public static class AvaloniaPorter
{
    /// <summary>Whether a project can be ported: a C# WPF or Windows Forms project.</summary>
    /// <param name="project">The project.</param>
    public static bool CanPort(ProjectInfo project)
    {
        ArgumentNullException.ThrowIfNull(project);
        return project.Language == ProjectLanguage.CSharp && project.Kind is ProjectKind.Wpf or ProjectKind.WinForms;
    }

    /// <summary>The new project's name the port suggests: the original's with <c>.Avalonia</c>.</summary>
    /// <param name="project">The original project.</param>
    public static string SuggestName(ProjectInfo project)
    {
        ArgumentNullException.ThrowIfNull(project);
        return project.Name + ".Avalonia";
    }

    /// <summary>The new project's folder the port suggests: beside the original's.</summary>
    /// <param name="project">The original project.</param>
    /// <param name="name">The new project's name.</param>
    public static string SuggestDirectory(ProjectInfo project, string name)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var parent = Path.GetDirectoryName(project.DirectoryPath.TrimEnd(Path.DirectorySeparatorChar)) ?? project.DirectoryPath;
        return Path.Combine(parent, name);
    }

    /// <summary>Checks a name for the new project.</summary>
    /// <param name="name">The name.</param>
    /// <returns>What is wrong with it, or <see langword="null"/> when it is a valid name.</returns>
    public static string? ValidateName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "Enter a name for the new project.";
        }

        if (name.Length > 80)
        {
            return "Use a name of 80 characters or fewer.";
        }

        if (!char.IsLetter(name[0]) || !name.All(c => char.IsLetterOrDigit(c) || c is '.' or '_') || name.EndsWith('.') || name.Contains("..", StringComparison.Ordinal))
        {
            return "Use letters, digits, dots and underscores, starting with a letter.";
        }

        return null;
    }

    /// <summary>Ports a project in memory; <see cref="PortResult.Write"/> writes it.</summary>
    /// <param name="project">The project.</param>
    /// <param name="options">The new project's name, folder and Avalonia version.</param>
    /// <param name="otherProjectDirectories">Folders of the solution's other projects, whose files are not the project's.</param>
    public static PortResult Port(ProjectInfo project, PortOptions options, IEnumerable<string>? otherProjectDirectories = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        return Port(PortSource.Load(project, otherProjectDirectories), options);
    }

    /// <summary>Ports a project read into memory.</summary>
    /// <param name="source">The project.</param>
    /// <param name="options">The new project's name, folder and Avalonia version.</param>
    public static PortResult Port(PortSource source, PortOptions options)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(options);
        var context = new PortContext(source, options);
        var files = source.Kind == PortSourceKind.Wpf ? WpfPorter.Port(context) : Forms.FormsPorter.Port(context);
        if (!files.Any(f => string.Equals(f.Path, "Program.cs", StringComparison.OrdinalIgnoreCase)))
        {
            files.Add(ProjectFiles.Program(context));
        }

        files.AddRange(ProjectFiles.Support(context));
        files.AddRange(ProjectFiles.Resources(context));
        files.Insert(0, ProjectFiles.ProjectFile(context));

        var notes = context.Reports
            .SelectMany(r => r.Notes.Select(n => new PortNote(r.Path, Math.Max(1, n.Line ?? 1), n.What, n.Why, AreaOf(r.Path))))
            .OrderBy(n => n.File, StringComparer.Ordinal)
            .ThenBy(n => n.Line)
            .ToList();
        return new PortResult(source, options, files, notes);
    }

    private static PortNoteArea AreaOf(string path) =>
        path.EndsWith(".axaml", StringComparison.OrdinalIgnoreCase) ? PortNoteArea.Xaml
        : path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ? PortNoteArea.Code
        : PortNoteArea.Project;
}
