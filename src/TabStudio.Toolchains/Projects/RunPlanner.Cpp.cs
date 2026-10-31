namespace TabStudio.Toolchains.Projects;

/// <summary>CMake, Make, Autotools and standalone C/C++ build plans, without shell quoting or binary name guesses.</summary>
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

        var makefile = project.Toolchain.ProjectFiles.FirstOrDefault(f => ToolchainProjectDetector.IsMakefile(f) && File.Exists(Path.Combine(project.Directory, f)));
        if (makefile is null && project.Toolchain.ProjectFiles.FirstOrDefault(f => ToolchainProjectDetector.IsAutotools(f) && File.Exists(Path.Combine(project.Directory, f))) is { } autotools)
        {
            // An Autotools project is configured first, as its README says: autoreconf makes configure from
            // configure.ac when the repository does not ship it, and configure writes the Makefile make builds.
            return new(Spec(project, ["sh", "-c", AutotoolsBuild], variables), Spec(project, [executable], variables), autotools);
        }

        return makefile is null ? null : new(Spec(project, ["make", "-f", makefile], variables), Spec(project, [executable], variables), makefile);
    }

    /// <summary>The build of a GNU Autotools project: <c>autoreconf -fi</c> when there is no <c>configure</c>, <c>./configure</c> when there is no Makefile, then <c>make</c>.</summary>
    internal const string AutotoolsBuild =
        "if [ ! -x ./configure ]; then echo 'Running autoreconf -fi'; autoreconf -fi || exit 1; fi; if [ ! -f Makefile ]; then echo 'Running ./configure'; ./configure || exit 1; fi; exec make";

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
