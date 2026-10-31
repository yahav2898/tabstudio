using TabStudio.Testing;
using TabStudio.Toolchains.Catalog;
using TabStudio.Toolchains.Projects;

namespace TabStudio.Toolchains.Tests.Projects;

/// <summary>
/// C and C++ repositories as the 0.20.5 sweep opened them: zlib listed 21 projects, one per CMakeLists.txt its build adds;
/// tinyxml2's F5 ran a Python script beside its CMakeLists.txt; jq, an Autotools project, was three source folders with
/// its <c>scripts</c> folder first.
/// </summary>
public sealed class NativeProjectLayoutTests : IDisposable
{
    private readonly TempDirectory _folder = new();

    [Fact]
    public void Build_files_inside_a_C_project_are_part_of_its_build()
    {
        _folder.Write("CMakeLists.txt", "add_subdirectory(test)\n");
        _folder.Write("test/CMakeLists.txt");
        _folder.Write("contrib/minizip/CMakeLists.txt");
        _folder.Write("deps/lua/Makefile");
        _folder.Write("examples/app/main.c");

        var projects = ToolchainProjectDetector.Detect(_folder.Path, ToolchainCatalog.Default);

        var project = Assert.Single(projects);
        Assert.Equal((_folder.Path, "CMakeLists.txt"), (project.Directory, project.Marker));
    }

    [Fact]
    public void Separate_C_projects_without_a_build_around_them_stay_separate()
    {
        _folder.Write("examples/example_glfw/Makefile");
        _folder.Write("examples/example_sdl/CMakeLists.txt");

        var projects = ToolchainProjectDetector.Detect(_folder.Path, ToolchainCatalog.Default);

        Assert.Equal([_folder.Resolve("examples/example_glfw"), _folder.Resolve("examples/example_sdl")], projects.Select(p => p.Directory).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void A_build_file_comes_before_loose_scripts_in_the_same_folder_and_tools_folders_come_last()
    {
        _folder.Write("CMakeLists.txt");
        _folder.Write("setup_docs.py");
        _folder.Write("scripts/release.py");
        _folder.Write("app/main.go");
        _folder.Write("app/go.mod", "module example.com/app\n");

        var projects = ToolchainProjectDetector.Detect(_folder.Path, ToolchainCatalog.Default);

        Assert.Equal(
            [("cpp", _folder.Path), ("go", _folder.Resolve("app")), ("python", _folder.Path), ("python", _folder.Resolve("scripts"))],
            projects.Select(p => (p.Toolchain.Id, p.Directory)));
    }

    [Fact]
    public void An_Autotools_project_is_configured_and_built_with_make()
    {
        _folder.Write("configure.ac", "AC_INIT([jq], [1.8])\n");
        _folder.Write("Makefile.am");
        _folder.Write("src/main.c");
        _folder.Write("tests/jqtest.c");
        _folder.Write("scripts/version.py");

        var projects = ToolchainProjectDetector.Detect(_folder.Path, ToolchainCatalog.Default);
        var plan = RunPlanner.PlanProject(projects[0])!;

        Assert.Equal(("cpp", _folder.Path, "configure.ac"), (projects[0].Toolchain.Id, projects[0].Directory, projects[0].Marker));
        Assert.DoesNotContain(projects, p => p.Directory == _folder.Resolve("src") || p.Directory == _folder.Resolve("tests"));
        Assert.Equal(["sh", "-c", RunPlanner.AutotoolsBuild], new[] { plan.Build!.Program }.Concat(plan.Build.Arguments));
        Assert.Contains("autoreconf -fi", RunPlanner.AutotoolsBuild, StringComparison.Ordinal);
        Assert.Equal(_folder.Path, projects[0].Directory);
        Assert.True(ToolchainProjectDetector.IsAutotools("configure.ac"));
        Assert.NotEqual("configure.ac", plan.ProgramName);
    }

    public void Dispose() => _folder.Dispose();
}
