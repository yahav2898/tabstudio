using System.Globalization;

namespace TabStudio.Toolchains.Projects;

/// <summary>
/// Swift run plans: a SwiftPM package's executable, built with <c>swift build</c> and run from
/// <c>.build/debug</c> (a Vapor app with <c>serve</c> on a free local port), or a loose file compiled with
/// <c>swiftc -g</c>.
/// </summary>
public static partial class RunPlanner
{
    private static RunPlan? PlanSwiftProject(ToolchainProject project, string? activeFile = null)
    {
        var directory = project.Directory;
        if (!File.Exists(Path.Combine(directory, "Package.swift")))
        {
            return null;
        }

        var executables = SwiftProject.Executables(directory);
        var executable = SwiftProject.ExecutableOf(directory, activeFile) ?? (executables.Count > 0 ? executables[0] : null);
        if (executable is null)
        {
            return null;
        }

        var variables = Variables(project, null, null);
        string[] arguments = SwiftProject.IsVapor(directory)
            ? ["serve", "--hostname", "127.0.0.1", "--port", FreeLoopbackPort().ToString(CultureInfo.InvariantCulture)]
            : [];
        var run = Spec(project, [SwiftProject.DebugExecutable(directory, executable), .. arguments], variables);
        return new RunPlan(Spec(project, ["swift", "build", "--product", executable], variables), run, "Package.swift") { ProgramName = executable };
    }

    private static RunPlan PlanSwiftFile(ToolchainProject project, string file)
    {
        var relative = Path.GetRelativePath(project.Directory, file).Replace('\\', '/');
        var variables = Variables(project, relative, null);

        // Under .build, which TabStudio's Swift runtime runs in its glibc environment, as SwiftPM's own programs.
        var output = Path.Combine(project.Directory, ".build", "tabstudio", Path.GetFileNameWithoutExtension(file));
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        return new RunPlan(
            Spec(project, ["swiftc", "-g", "-Onone", relative, "-o", output], variables),
            Spec(project, [output], variables), relative);
    }
}
