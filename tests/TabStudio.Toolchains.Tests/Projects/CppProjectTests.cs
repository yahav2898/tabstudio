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
    [InlineData("configure.ac")]
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
    public void CMake_targets_know_their_name_and_the_folder_whose_CMakeLists_defines_them()
    {
        WriteCJsonLikeCodemodel();
        var targets = CMakeProject.Executables(_folder.Resolve("build"), _folder.Path);
        Assert.Equal(["cJSON_test", "parse_examples", "print_value", "fuzz_main"], targets.Select(t => t.Name));
        Assert.Equal(_folder.Resolve("."), targets[0].SourceDirectory);
        Assert.Equal(_folder.Resolve("tests"), targets[1].SourceDirectory);
        Assert.Equal(_folder.Resolve("tests"), targets[2].SourceDirectory);
        Assert.Equal(_folder.Resolve("fuzzing"), targets[3].SourceDirectory);
        Assert.Equal(_folder.Resolve("build/cJSON_test"), targets[0].Path);
    }

    [Fact]
    public void Of_several_CMake_executables_the_only_one_the_top_level_CMakeLists_defines_runs()
    {
        WriteCJsonLikeCodemodel();
        var targets = CMakeProject.Executables(_folder.Resolve("build"), _folder.Path);
        Assert.Equal("cJSON_test", CMakeProject.Choose(targets, _folder.Path, null, [], _folder.Path)?.Name);

        // A library source, which no executable compiles, does not change the choice.
        Assert.Equal("cJSON_test", CMakeProject.Choose(targets, _folder.Path, _folder.Resolve("cJSON.c"), [], _folder.Path)?.Name);
    }

    [Fact]
    public void A_CMake_executable_is_chosen_by_the_file_being_edited_or_a_launch_configuration()
    {
        WriteCJsonLikeCodemodel();
        var targets = CMakeProject.Executables(_folder.Resolve("build"), _folder.Path);
        Assert.Equal("print_value", CMakeProject.Choose(targets, _folder.Path, _folder.Resolve("tests/print_value.c"), [], _folder.Path)?.Name);
        Assert.Equal("parse_examples", CMakeProject.Choose(targets, _folder.Path, null, ["parse_examples"], _folder.Path)?.Name);
        Assert.Equal("fuzz_main", CMakeProject.Choose(targets, _folder.Path, null, [_folder.Resolve("build/fuzzing/fuzz_main")], _folder.Path)?.Name);
        Assert.Equal("fuzz_main", CMakeProject.Choose(targets, _folder.Path, null, ["build/fuzzing/fuzz_main"], _folder.Path)?.Name);

        // The launch configuration's choice comes before the file being edited; a program it names that was not built is passed over.
        Assert.Equal("parse_examples", CMakeProject.Choose(targets, _folder.Path, _folder.Resolve("tests/print_value.c"), ["missing", "parse_examples"], _folder.Path)?.Name);
    }

    [Fact]
    public void Several_CMake_executables_in_subfolders_only_leave_nothing_chosen()
    {
        _folder.Write("build/.cmake/api/v1/reply/index-1.json", """{"reply":{"codemodel-v2":{"jsonFile":"model.json"}}}""");
        _folder.Write("build/.cmake/api/v1/reply/model.json", """
            {"configurations":[{"name":"Debug",
              "directories":[{"source":"."},{"source":"apps/one"},{"source":"apps/two"}],
              "targets":[{"name":"one","directoryIndex":1,"jsonFile":"one.json"},{"name":"two","directoryIndex":2,"jsonFile":"two.json"}]}]}
            """);
        _folder.Write("build/.cmake/api/v1/reply/one.json", """{"name":"one","type":"EXECUTABLE","artifacts":[{"path":"apps/one/one"}],"sources":[{"path":"apps/one/main.c"}]}""");
        _folder.Write("build/.cmake/api/v1/reply/two.json", """{"name":"two","type":"EXECUTABLE","artifacts":[{"path":"apps/two/two"}],"sources":[{"path":"apps/two/main.c"}]}""");
        var targets = CMakeProject.Executables(_folder.Resolve("build"), _folder.Path);

        // Without "paths" in the target, the configuration's directories give its folder.
        Assert.Equal([_folder.Resolve("apps/one"), _folder.Resolve("apps/two")], targets.Select(t => t.SourceDirectory));
        Assert.Null(CMakeProject.Choose(targets, _folder.Path, null, [], _folder.Path));
        Assert.Null(CMakeProject.Choose(targets, _folder.Path, null, ["three"], _folder.Path));
        Assert.Null(CMakeProject.Choose([], _folder.Path, null, [], _folder.Path));
        Assert.Equal("two", CMakeProject.Choose(targets, _folder.Path, _folder.Resolve("apps/two/main.c"), [], _folder.Path)?.Name);
    }

    [Fact]
    public void Of_several_Makefile_programs_a_launch_configuration_or_the_only_one_at_the_root_runs()
    {
        string[] programs = [_folder.Resolve("demo"), _folder.Resolve("bin/tool"), _folder.Resolve("bin/other")];
        Assert.Equal(_folder.Resolve("demo"), NativePrograms.Choose(programs, _folder.Path, [], _folder.Path));
        Assert.Equal(_folder.Resolve("bin/tool"), NativePrograms.Choose(programs, _folder.Path, ["tool"], _folder.Path));
        Assert.Equal(_folder.Resolve("bin/other"), NativePrograms.Choose(programs, _folder.Path, ["bin/other"], _folder.Path));
        Assert.Equal(_folder.Resolve("bin/tool"), NativePrograms.Choose([_folder.Resolve("bin/tool")], _folder.Path, [], _folder.Path));
        Assert.Null(NativePrograms.Choose([_folder.Resolve("bin/tool"), _folder.Resolve("bin/other")], _folder.Path, [], _folder.Path));
        Assert.Null(NativePrograms.Choose([], _folder.Path, ["tool"], _folder.Path));
    }

    [Theory]
    [InlineData("cJSON_test", true)]
    [InlineData("cJSON_test.exe", false)]
    [InlineData("build/cJSON_test", true)]
    [InlineData("tests/cJSON_test", false)]
    [InlineData("readme_examples", false)]
    [InlineData("", false)]
    public void A_launch_configuration_names_a_program_by_file_name_or_path(string program, bool names) =>
        Assert.Equal(names, NativePrograms.Names(program, _folder.Resolve("build/cJSON_test"), _folder.Path));

    /// <summary>
    /// A codemodel shaped like DaveGamble/cJSON's: <c>cJSON_test</c> from the top-level CMakeLists.txt, tests from
    /// <c>tests/</c> and a fuzzer from <c>fuzzing/</c>, besides the <c>cjson</c> library.
    /// </summary>
    private void WriteCJsonLikeCodemodel()
    {
        _folder.Write("build/.cmake/api/v1/reply/index-2024.json", """{"reply":{"codemodel-v2":{"jsonFile":"codemodel-v2-1.json"}}}""");
        _folder.Write("build/.cmake/api/v1/reply/codemodel-v2-1.json", """
            {"configurations":[{"name":"Debug",
              "directories":[{"source":"."},{"source":"tests"},{"source":"fuzzing"}],
              "targets":[
                {"name":"cjson","directoryIndex":0,"jsonFile":"target-cjson.json"},
                {"name":"cJSON_test","directoryIndex":0,"jsonFile":"target-cJSON_test.json"},
                {"name":"parse_examples","directoryIndex":1,"jsonFile":"target-parse_examples.json"},
                {"name":"print_value","directoryIndex":1,"jsonFile":"target-print_value.json"},
                {"name":"fuzz_main","directoryIndex":2,"jsonFile":"target-fuzz_main.json"}]}]}
            """);
        _folder.Write("build/.cmake/api/v1/reply/target-cjson.json", """{"name":"cjson","type":"SHARED_LIBRARY","paths":{"source":".","build":"."},"artifacts":[{"path":"libcjson.so"}],"sources":[{"path":"cJSON.c"}]}""");
        _folder.Write("build/.cmake/api/v1/reply/target-cJSON_test.json", """{"name":"cJSON_test","type":"EXECUTABLE","paths":{"source":".","build":"."},"artifacts":[{"path":"cJSON_test"}],"sources":[{"path":"test.c"}]}""");
        _folder.Write("build/.cmake/api/v1/reply/target-parse_examples.json", """{"name":"parse_examples","type":"EXECUTABLE","paths":{"source":"tests","build":"tests"},"artifacts":[{"path":"tests/parse_examples"}],"sources":[{"path":"tests/parse_examples.c"}]}""");
        _folder.Write("build/.cmake/api/v1/reply/target-print_value.json", """{"name":"print_value","type":"EXECUTABLE","paths":{"source":"tests","build":"tests"},"artifacts":[{"path":"tests/print_value"}],"sources":[{"path":"tests/print_value.c"}]}""");
        _folder.Write("build/.cmake/api/v1/reply/target-fuzz_main.json", """{"name":"fuzz_main","type":"EXECUTABLE","paths":{"source":"fuzzing","build":"fuzzing"},"artifacts":[{"path":"fuzzing/fuzz_main"}],"sources":[{"path":"fuzzing/fuzz_main.c"}]}""");
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
