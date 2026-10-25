namespace TabStudio.Toolchains.Projects;

/// <summary>Builds Go commands as debuggable executables; module boundaries stay separate.</summary>
public static partial class RunPlanner
{
    private static RunPlan? PlanGoProject(ToolchainProject project)
    {
        if (HasGoMain(project.Directory))
        {
            return GoCommand(project, ".", "go.mod");
        }

        // The conventional cmd/name layout. With several commands, the active file selects which to run.
        var commands = Path.Combine(project.Directory, "cmd");
        var candidates = Directory.Exists(commands)
            ? Directory.EnumerateDirectories(commands).Where(HasGoMain).Order(StringComparer.Ordinal).Take(2).ToList()
            : [];
        return candidates.Count == 1 ? GoCommand(project, "./cmd/" + Path.GetFileName(candidates[0]), "cmd/" + Path.GetFileName(candidates[0])) : null;
    }

    private static RunPlan PlanGoFile(ToolchainProject project, string file)
    {
        var relative = Path.GetRelativePath(project.Directory, file).Replace('\\', '/');
        var folder = Path.GetDirectoryName(relative)?.Replace('\\', '/');
        // Build the whole package in a module, including sibling files, rather than only the active source.
        var target = File.Exists(Path.Combine(project.Directory, "go.mod"))
            ? string.IsNullOrEmpty(folder) ? "." : "./" + folder
            : relative;
        return GoCommand(project, target, relative);
    }

    private static RunPlan GoCommand(ToolchainProject project, string target, string entry)
    {
        var variables = Variables(project, file: null, json: null);
        var executable = OperatingSystem.IsWindows() ? ".tabstudio/bin/app.exe" : ".tabstudio/bin/app";
        return new RunPlan(
            Spec(project, ["go", "build", "-gcflags=all=-N -l", "-o", executable, target], variables),
            Spec(project, [executable], variables), entry);
    }

    private static bool HasGoMain(string folder) => Directory.EnumerateFiles(folder, "*.go")
        .Where(f => !f.EndsWith("_test.go", StringComparison.Ordinal))
        .Any(f => File.ReadLines(f).Any(l => l.Trim() is "package main" or "package main;"));
}
