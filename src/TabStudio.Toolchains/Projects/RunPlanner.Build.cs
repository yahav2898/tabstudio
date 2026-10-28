using TabStudio.Toolchains.Processes;

namespace TabStudio.Toolchains.Projects;

/// <summary>
/// Ctrl+Shift+B for PHP, Kotlin and Swift projects: a build without running anything, whose errors go to the Error
/// List. PHP is not compiled, so its build checks the syntax of the project's own files with <c>php -l</c>.
/// </summary>
public static partial class RunPlanner
{
    /// <summary>The command that builds a project without running it, or <see langword="null"/> when it has none.</summary>
    /// <param name="project">The project.</param>
    public static ProcessStartSpec? PlanBuild(ToolchainProject project)
    {
        ArgumentNullException.ThrowIfNull(project);
        switch (project.Toolchain.Id)
        {
            case "php":
                // php -l takes several files from PHP 8.3; vendor and node_modules are other people's code.
                return CommandSpec(project, ["sh", "-c",
                    "find . \\( -name vendor -o -name node_modules -o -name .git -o -name .tabstudio -o -name storage \\) -prune -o -name '*.php' -print0 | xargs -0 -r -n 64 php -d display_errors=stderr -d error_reporting=-1 -l > /dev/null"]);
            case "swift":
                return File.Exists(Path.Combine(project.Directory, "Package.swift")) ? CommandSpec(project, ["swift", "build"]) : null;
            case "kotlin":
                return PlanProject(project)?.Build;
            default:
                return null;
        }
    }
}
