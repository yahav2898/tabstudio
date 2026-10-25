using TabStudio.Core.Compatibility;
using TabStudio.Core.Workspaces;
using TabStudio.Testing;
using TabStudio.Toolchains.Catalog;
using TabStudio.Toolchains.Projects;

namespace TabStudio.Toolchains.Tests.Projects;

/// <summary>CMake, Makefile and single-source projects, with real compiler locations.</summary>
public sealed class CppProjectTests : IDisposable
{
    private readonly TempDirectory _folder = new();
    private static ToolchainDefinition Cpp => ToolchainCatalog.Default.Find("cpp")!;

    [Fact]
    public void Portable_C_and_Cpp_projects_use_the_toolchain_while_Visual_Cpp_keeps_the_Windows_Bridge()
    {
        var portable = new ProjectInfo { Name = "Portable", FilePath = _folder.Resolve("CMakeLists.txt"), Kind = ProjectKind.Cpp, OutputType = "Exe" };
        var visual = portable with { FilePath = _folder.Resolve("App.vcxproj"), Kind = ProjectKind.NativeCpp };
        var report = CompatibilityDoctor.Diagnose(portable);

        Assert.Equal(RunHost.Toolchain, report.Host);
        Assert.Equal(CompatibilityStatus.NeedsToolchain, report.Status);
        Assert.False(WindowsBridgeCompatibility.IsNativeApplication(portable));
        Assert.False(WindowsBridgeCompatibility.IsWindowsOnly(portable));
        Assert.True(WindowsBridgeCompatibility.IsNativeApplication(visual));
        Assert.True(WindowsBridgeCompatibility.IsWindowsOnly(visual));
    }

    [Theory]
    [InlineData("CMakeLists.txt")]
    [InlineData("Makefile")]
    [InlineData("makefile")]
    [InlineData("GNUmakefile")]
    [InlineData("main.c")]
    [InlineData("main.cpp")]
    public void Detects_supported_native_project_layouts(string marker)
    {
        _folder.Write(marker);
        var project = Assert.Single(ToolchainProjectDetector.Detect(_folder.Path, ToolchainCatalog.Default));
        Assert.Equal("cpp", project.Toolchain.Id);
    }

    [Fact]
    public void Headers_and_build_output_do_not_create_source_projects()
    {
        _folder.Write("main.hpp");
        _folder.Write("build/generated.cpp");
        Assert.Empty(ToolchainProjectDetector.Detect(_folder.Path, ToolchainCatalog.Default));
    }

    [Fact]
    public void CMake_configuration_exports_a_database_and_asks_for_the_codemodel()
    {
        _folder.Write("CMakeLists.txt");
        var plan = RunPlanner.PlanProject(new(Cpp, _folder.Path, "CMakeLists.txt"))!;
        var configure = Assert.Single(plan.Preparation!);
        Assert.Contains("-DCMAKE_EXPORT_COMPILE_COMMANDS=ON", configure.Arguments);
        Assert.Equal("cmake", plan.Build!.Program);
        Assert.Equal("--build", plan.Build.Arguments[0]);
        Assert.True(File.Exists(_folder.Resolve(".tabstudio/cmake/.cmake/api/v1/query/codemodel-v2")));
    }

    [Theory]
    [InlineData("main.c", "cc", "-std=c17")]
    [InlineData("main.cpp", "c++", "-std=c++20")]
    public void Single_files_use_the_correct_compiler_with_debug_symbols(string name, string compiler, string standard)
    {
        var file = _folder.Write(name);
        var plan = RunPlanner.PlanFile(new(Cpp, _folder.Path, null), file);
        Assert.Equal(compiler, plan.Build!.Program);
        Assert.Contains(standard, plan.Build.Arguments);
        Assert.Contains("-g", plan.Build.Arguments);
        Assert.Contains("-O0", plan.Build.Arguments);
        Assert.Equal(_folder.Resolve(".tabstudio/bin/app"), plan.Run.Program);
    }

    [Fact]
    public void CMake_artifact_paths_are_read_from_the_file_API_including_subdirectories()
    {
        _folder.Write("build/.cmake/api/v1/reply/index-1.json", """{"reply":{"codemodel-v2":{"jsonFile":"model.json"}}}""");
        _folder.Write("build/.cmake/api/v1/reply/model.json", """{"configurations":[{"name":"Debug","targets":[{"jsonFile":"app.json"},{"jsonFile":"lib.json"}]}]}""");
        _folder.Write("build/.cmake/api/v1/reply/app.json", """{"type":"EXECUTABLE","artifacts":[{"path":"tools/Debug/my-app"}],"sources":[{"path":"src/main.cpp"}]}""");
        _folder.Write("build/.cmake/api/v1/reply/lib.json", """{"type":"STATIC_LIBRARY","artifacts":[{"path":"lib.a"}]}""");
        var target = Assert.Single(CMakeProject.Executables(_folder.Resolve("build"), _folder.Path));
        Assert.Equal(_folder.Resolve("build/tools/Debug/my-app"), target.Path);
        Assert.Equal([_folder.Resolve("src/main.cpp")], target.Sources);
        Assert.Empty(CMakeProject.Executables(_folder.Resolve("missing"), _folder.Path));
    }

    [Fact]
    public void Make_builds_without_requiring_a_run_target()
    {
        _folder.Write("Makefile", "app: main.c\n\tcc -g -o app main.c\n");
        var plan = RunPlanner.PlanProject(new(Cpp, _folder.Path, "Makefile"))!;
        Assert.Equal("make", plan.Build!.Program);
        Assert.Equal(["-f", "Makefile"], plan.Build.Arguments);
        Assert.Empty(NativePrograms.Find(_folder.Path));
    }

    [Theory]
    [InlineData("/work/my app/main.cpp:4:3: error: unknown name")]
    [InlineData("main.c:4:3: fatal error: missing header")]
    public void Native_errors_keep_their_source_locations(string output)
    {
        var error = Assert.Single(new ProblemMatcher(Cpp).FindErrors([output], _ => true));
        Assert.Equal(4, error.Location!.Line);
        Assert.Equal(3, error.Location.Column);
    }

    [Theory]
    [InlineData("main.cpp:4:3: warning: unused value")]
    [InlineData("[100%] Built target greeter")]
    [InlineData("main.cpp:0:3: error: invalid line")]
    public void Warnings_and_progress_are_not_errors(string output) =>
        Assert.Empty(new ProblemMatcher(Cpp).FindErrors([output], _ => true));

    [Fact]
    public void A_32_bit_shared_library_is_not_mistaken_for_a_Makefile_program()
    {
        var library = new byte[32];
        library[0] = 0x7f; library[1] = (byte)'E'; library[2] = (byte)'L'; library[3] = (byte)'F';
        library[4] = 1; library[5] = 1; library[16] = 3; library[28] = 52;
        File.WriteAllBytes(_folder.Resolve("lib.so"), library);
        Assert.Empty(NativePrograms.Find(_folder.Path));
        library[24] = 1;
        File.WriteAllBytes(_folder.Resolve("app"), library);
        Assert.Equal([_folder.Resolve("app")], NativePrograms.Find(_folder.Path));
    }

    public void Dispose() => _folder.Dispose();
}
