namespace TabStudio.Core.Workspaces;

/// <summary>The part of reading a project that looks at its files: whether it has Razor components, views or pages.</summary>
public static partial class ProjectParser
{
    /// <summary>How many folders are looked through for a <c>.razor</c> file, so a huge folder cannot stall opening.</summary>
    private const int MaxFoldersScanned = 2_000;

    /// <summary>
    /// Records whether an app on the Web SDK has Razor components or Razor views and pages, and classifies it again:
    /// an app with <c>.razor</c> files is a Blazor Web App, as the Web SDK decides when it adds Blazor's script, and
    /// one with <c>.cshtml</c> files has a UI of its own rather than only an API.
    /// </summary>
    private static ProjectInfo WithRazorComponents(ProjectInfo project)
    {
        if (!string.Equals(project.Sdk, "Microsoft.NET.Sdk.Web", StringComparison.OrdinalIgnoreCase))
        {
            return project;
        }

        var (components, views) = FindRazorFiles(project.DirectoryPath);
        if (!components && !views)
        {
            return project;
        }

        var withComponents = project with { HasRazorComponents = components, HasRazorViews = views };
        var (kind, isTest) = ProjectClassifier.Classify(withComponents);
        return withComponents with { Kind = kind, IsTestProject = isTest };
    }

    /// <summary>Whether a project folder has a <c>.razor</c> file, and whether it has a <c>.cshtml</c> file, outside its build output.</summary>
    private static (bool Components, bool Views) FindRazorFiles(string directory)
    {
        var components = false;
        var views = false;
        var pending = new Stack<string>([directory]);
        var scanned = 0;
        while (pending.TryPop(out var current) && scanned++ < MaxFoldersScanned)
        {
            try
            {
                foreach (var file in Directory.EnumerateFiles(current))
                {
                    components = components || file.EndsWith(".razor", StringComparison.OrdinalIgnoreCase);
                    views = views || file.EndsWith(".cshtml", StringComparison.OrdinalIgnoreCase);
                }

                // Components make the app a Blazor Web App, whatever views it has besides, so the search can stop.
                if (components)
                {
                    break;
                }

                foreach (var subdirectory in Directory.EnumerateDirectories(current))
                {
                    if (!PathUtilities.IsIgnoredDirectory(Path.GetFileName(subdirectory)))
                    {
                        pending.Push(subdirectory);
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // A folder that cannot be read has no components to compile.
            }
        }

        return (components, views);
    }
}
