namespace TabStudio.Toolchains.Projects;

/// <summary>CMake, Make and standalone C/C++ build plans, without shell quoting or binary name guesses.</summary>
public static partial class RunPlanner
{
    private static RunPlan? PlanCppProject(ToolchainProject project)
    {
        var variables = Variables(project, null, null);
        var executable = Path.Combine(project.Directory, ".tabstudio", "bin", OperatingSystem.IsWindows() ? "app.exe" : "app");
        if (File.Exists(Path.Combine(project.Directory, "CMakeLists.txt")))
        {
            var build = Path.Combine(project.Directory, ".tabstudio", "cmake");
            CMakeProject.PrepareQuery(build);
            return new(
                Spec(project, ["cmake", "--build", build, "--config", "Debug"], variables),
                Spec(project, [executable], variables), "CMakeLists.txt",
                [Spec(project, ["cmake", "-S", project.Directory, "-B", build, "-DCMAKE_BUILD_TYPE=Debug", "-DCMAKE_EXPORT_COMPILE_COMMANDS=ON"], variables)]);
        }

        var makefile = project.Toolchain.ProjectFiles.FirstOrDefault(f => f != "CMakeLists.txt" && File.Exists(Path.Combine(project.Directory, f)));
        return makefile is null ? null : new(Spec(project, ["make", "-f", makefile], variables), Spec(project, [executable], variables), makefile);
    }

    private static RunPlan PlanCppFile(ToolchainProject project, string file)
    {
        var relative = Path.GetRelativePath(project.Directory, file).Replace('\\', '/');
        var executable = Path.Combine(project.Directory, ".tabstudio", "bin", OperatingSystem.IsWindows() ? "app.exe" : "app");
        Directory.CreateDirectory(Path.GetDirectoryName(executable)!);
        var compiler = Path.GetExtension(file).Equals(".c", StringComparison.OrdinalIgnoreCase) ? "cc" : "c++";
        var standard = compiler == "cc" ? "-std=c17" : "-std=c++20";
        var variables = Variables(project, relative, null);
        return new(Spec(project, [compiler, standard, "-g", "-O0", "-o", executable, relative], variables), Spec(project, [executable], variables), relative);
    }
}
