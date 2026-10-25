using System.Text.RegularExpressions;
using TabStudio.App.Services;
using TabStudio.Core.Workspaces;

namespace TabStudio.App.Swarm.Recording;

/// <summary>Where a recorded test was saved.</summary>
/// <param name="TestFilePath">The test's file.</param>
/// <param name="ProjectFilePath">The test project.</param>
/// <param name="CreatedProject">Whether the test project was created for it, and added to the solution.</param>
public sealed record UiTestSaved(string TestFilePath, string ProjectFilePath, bool CreatedProject);

/// <summary>
/// Puts recorded UI tests where the solution keeps its tests: the xUnit test project that references the app, which
/// gets Avalonia's headless platform and the <c>RecordedApp</c> driver the first time; or, when the app has no such
/// project, a new <c>App.UITests</c> project beside it, added to the solution.
/// </summary>
public static partial class UiTestProjects
{
    /// <summary>The xUnit test project of the workspace that references the app, if there is one.</summary>
    /// <param name="workspace">The workspace.</param>
    /// <param name="app">The app's project.</param>
    public static ProjectInfo? Find(Workspace workspace, ProjectInfo app)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(app);
        return workspace.Projects
            .Where(p => p.IsTestProject && p.Language == ProjectLanguage.CSharp && p.HasPackage("xunit"))
            .Where(p => p.ProjectReferences.Any(r => SamePath(r, app.FilePath)))
            .OrderByDescending(p => p.HasPackage("Avalonia.Headless.XUnit"))
            .FirstOrDefault();
    }

    /// <summary>Saves a recorded test into the app's test project, creating the project when there is none.</summary>
    /// <param name="workspace">The workspace.</param>
    /// <param name="app">The app's project.</param>
    /// <param name="recording">The recording.</param>
    public static UiTestSaved Save(Workspace workspace, ProjectInfo app, UiTestRecording recording)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(recording);
        var existing = Find(workspace, app);
        var projectPath = existing?.FilePath ?? Create(workspace, app);
        var directory = Path.GetDirectoryName(projectPath)!;
        var namespaceName = existing is null ? NamespaceOf(Path.GetFileNameWithoutExtension(projectPath)) : NamespaceOf(existing);

        foreach (var (id, version) in UiTestWriter.Packages(ProjectTemplates.AvaloniaVersion))
        {
            if (existing?.HasPackage(id) != true)
            {
                ProjectFileEditor.SetPackageReference(projectPath, id, version);
            }
        }

        if (!AnySourceContains(directory, "class RecordedApp"))
        {
            File.WriteAllText(Path.Combine(directory, UiTestWriter.DriverFileName), UiTestWriter.Driver(namespaceName, AppClassOf(app)));
        }

        if (!AnySourceContains(directory, "AvaloniaTestApplication"))
        {
            File.WriteAllText(Path.Combine(directory, UiTestWriter.AppBuilderFileName), UiTestWriter.AppBuilder(namespaceName));
        }

        var folder = Path.Combine(directory, UiTestWriter.Folder);
        Directory.CreateDirectory(folder);
        var named = recording;
        for (var i = 2; File.Exists(Path.Combine(folder, UiTestWriter.ClassName(named.Name) + ".cs")); i++)
        {
            named = recording with { Name = $"{recording.Name} {i}" };
        }

        var file = Path.Combine(folder, UiTestWriter.ClassName(named.Name) + ".cs");
        File.WriteAllText(file, UiTestWriter.Test(namespaceName + "." + UiTestWriter.Folder, named));
        return new UiTestSaved(file, projectPath, existing is null);
    }

    /// <summary>
    /// The full name of the app's <c>Application</c> class: the <c>x:Class</c> of its <c>App.axaml</c>, or the class
    /// deriving from <c>Application</c> in its C#.
    /// </summary>
    /// <param name="app">The app's project.</param>
    public static string AppClassOf(ProjectInfo app)
    {
        ArgumentNullException.ThrowIfNull(app);
        foreach (var file in SourceFiles(app.DirectoryPath, "*.axaml"))
        {
            var text = File.ReadAllText(file);
            if (text.Contains("<Application", StringComparison.Ordinal) && ClassAttribute().Match(text) is { Success: true } match)
            {
                return match.Groups[1].Value;
            }
        }

        foreach (var file in SourceFiles(app.DirectoryPath, "*.cs"))
        {
            var text = File.ReadAllText(file);
            if (ApplicationClass().Match(text) is { Success: true } match)
            {
                var ns = NamespaceDeclaration().Match(text);
                return ns.Success ? ns.Groups[1].Value + "." + match.Groups[1].Value : match.Groups[1].Value;
            }
        }

        return NamespaceOf(app) + ".App";
    }

    /// <summary>Creates <c>App.UITests</c> beside the app and adds it to the solution.</summary>
    private static string Create(Workspace workspace, ProjectInfo app)
    {
        var parent = Path.GetDirectoryName(app.DirectoryPath) ?? workspace.RootDirectory;
        var name = app.Name + ".UITests";
        for (var i = 2; Directory.Exists(Path.Combine(parent, name)); i++)
        {
            name = $"{app.Name}.UITests{i}";
        }

        var directory = Path.Combine(parent, name);
        Directory.CreateDirectory(directory);
        var projectPath = Path.Combine(directory, name + ".csproj");
        File.WriteAllText(projectPath, UiTestWriter.Project(Path.GetRelativePath(directory, app.FilePath)));
        if (workspace.Solution is { } solution)
        {
            SolutionFileEditor.AddProject(solution.FilePath, projectPath);
        }

        return projectPath;
    }

    private static string NamespaceOf(ProjectInfo project) => NamespaceOf(project.GetProperty("RootNamespace") ?? project.Name);

    private static string NamespaceOf(string name)
    {
        var parts = name.Split('.', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => string.Concat(p.Select(c => char.IsLetterOrDigit(c) ? c : '_')))
            .Select(p => char.IsDigit(p[0]) ? "_" + p : p);
        return string.Join('.', parts);
    }

    private static bool AnySourceContains(string directory, string text) =>
        SourceFiles(directory, "*.cs").Any(f => File.ReadAllText(f).Contains(text, StringComparison.Ordinal));

    private static IEnumerable<string> SourceFiles(string directory, string pattern) =>
        Directory.Exists(directory)
            ? Directory.EnumerateFiles(directory, pattern, SearchOption.AllDirectories).Where(f => !IsBuildOutput(directory, f))
            : [];

    private static bool IsBuildOutput(string directory, string file)
    {
        var relative = Path.GetRelativePath(directory, file).Replace('\\', '/');
        return relative.StartsWith("bin/", StringComparison.Ordinal) || relative.StartsWith("obj/", StringComparison.Ordinal);
    }

    private static bool SamePath(string a, string b) =>
        string.Equals(Path.GetFullPath(a.Replace('\\', '/')), Path.GetFullPath(b.Replace('\\', '/')), StringComparison.Ordinal);

    [GeneratedRegex("""x:Class\s*=\s*"([^"]+)"\s*""")]
    private static partial Regex ClassAttribute();

    [GeneratedRegex(@"class\s+(\w+)\s*:\s*(?:global::)?(?:Avalonia\.)?Application\b")]
    private static partial Regex ApplicationClass();

    [GeneratedRegex(@"namespace\s+([\w.]+)")]
    private static partial Regex NamespaceDeclaration();
}
