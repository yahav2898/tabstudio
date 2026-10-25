namespace TabStudio.Core.Workspaces;

/// <summary>The part of reading a project that looks at its files: whether it has Razor components.</summary>
public static partial class ProjectParser
{
    /// <summary>How many folders are looked through for a <c>.razor</c> file, so a huge folder cannot stall opening.</summary>
    private const int MaxFoldersScanned = 2_000;

    /// <summary>
    /// Records whether an app on the Web SDK has Razor components, and classifies it again: an app with
    /// <c>.razor</c> files is a Blazor Web App, as the Web SDK decides when it adds Blazor's script.
    /// </summary>
    private static ProjectInfo WithRazorComponents(ProjectInfo project)
    {
        if (!string.Equals(project.Sdk, "Microsoft.NET.Sdk.Web", StringComparison.OrdinalIgnoreCase) || !HasRazorFile(project.DirectoryPath))
        {
            return project;
        }

        var withComponents = project with { HasRazorComponents = true };
        var (kind, isTest) = ProjectClassifier.Classify(withComponents);
        return withComponents with { Kind = kind, IsTestProject = isTest };
    }

    /// <summary>Whether a project folder has a <c>.razor</c> file outside its build output.</summary>
    private static bool HasRazorFile(string directory)
    {
        var pending = new Stack<string>([directory]);
        var scanned = 0;
        while (pending.TryPop(out var current) && scanned++ < MaxFoldersScanned)
        {
            try
            {
                if (Directory.EnumerateFiles(current, "*.razor").Any())
                {
                    return true;
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

        return false;
    }
}
