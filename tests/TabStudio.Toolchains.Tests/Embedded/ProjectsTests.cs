using System.Text.Json;
using TabStudio.Testing;
using TabStudio.Toolchains.Catalog;
using TabStudio.Toolchains.Embedded.Projects;
using TabStudio.Toolchains.Projects;

namespace TabStudio.Toolchains.Tests.Embedded;

public sealed class ProjectsTests
{
    [Fact]
    public void PlatformIO_shared_environment_and_default_selection_are_preserved()
    {
        using var temp = new TempDirectory();
        temp.Write("platformio.ini", "[platformio]\ndefault_envs = debug\n[env]\nboard = esp32dev\nframework = arduino\n[env:debug]\nbuild_type = debug\n[env:release]\nboard = esp32-s3-devkitc-1\n");
        var project = Assert.Single(EmbeddedProjectDetector.Detect(temp.Path));
        Assert.Equal(EmbeddedProjectKind.PlatformIO, project.Kind);
        Assert.Equal("debug", project.DefaultEnvironment);
        Assert.Equal("esp32dev", project.Environments[0].Board);
        Assert.Equal("arduino", project.Environments[1].Framework);
        var plan = EmbeddedBuildPlanner.Plan(project, new EmbeddedBuildOptions { Port = "/dev/ttyUSB0" });
        Assert.Equal(2, plan.Build.Count);
        Assert.All(plan.Build, command => Assert.Equal("pio", command.Program));
        Assert.Equal(["run", "--environment", "debug", "--target", "upload", "--upload-port", "/dev/ttyUSB0"], Assert.Single(plan.Upload).Arguments);
        Assert.Equal(temp.Path, plan.Build[0].WorkingDirectory);
    }

    [Fact]
    public void Multiple_default_environments_are_buildable_but_cannot_silently_upload_multiple_boards()
    {
        using var temp = new TempDirectory();
        temp.Write("platformio.ini", "[platformio]\ndefault_envs =\n  uno,\n  esp\n[env:uno]\nboard = uno\n[env:esp]\nboard = esp32dev\n");
        var project = EmbeddedProjectDetector.Read(temp.Path)!;
        Assert.Null(project.DefaultEnvironment);
        var plan = EmbeddedBuildPlanner.Plan(project, new EmbeddedBuildOptions { Port = "/dev/ttyUSB0" });
        Assert.Equal(2, plan.Build.Count);
        Assert.All(plan.Build, command => Assert.DoesNotContain("--environment", command.Arguments));
        Assert.Empty(plan.Upload);
        Assert.Contains(plan.Notes, n => n.Contains("multiple default", StringComparison.Ordinal));
    }

    [Fact]
    public void An_environment_from_extra_configs_is_left_for_PlatformIO_to_resolve()
    {
        using var temp = new TempDirectory();
        temp.Write("platformio.ini", "[env:uno]\nboard=uno");
        var plan = EmbeddedBuildPlanner.Plan(EmbeddedProjectDetector.Read(temp.Path)!,
            new EmbeddedBuildOptions { Environment = "imported-debug" });
        Assert.Equal(2, plan.Build.Count);
        Assert.All(plan.Build, command => Assert.Contains("imported-debug", command.Arguments));
        Assert.Contains(plan.Notes, n => n.Contains("extra_configs", StringComparison.Ordinal));
    }

    [Fact]
    public void PlatformIO_generates_native_compilation_database_after_build_with_the_same_policy_and_environment()
    {
        using var temp = new TempDirectory();
        var configuration = "[env:uno]\nboard=uno\nextra_scripts=pre:prepare.py\n";
        temp.Write("platformio.ini", configuration);
        temp.Write("prepare.py", "Import('env')\nenv.Append(CPPDEFINES=['FROM_PROJECT_SCRIPT'])\n");
        var plan = EmbeddedBuildPlanner.Plan(EmbeddedProjectDetector.Read(temp.Path)!,
            new EmbeddedBuildOptions { Environment = "uno", Jobs = 3, HostPackageMode = "native" });
        Assert.Equal(2, plan.Build.Count);
        Assert.Equal(["run", "--environment", "uno", "--jobs", "3"], plan.Build[0].Arguments);
        Assert.Equal(["run", "--environment", "uno", "--target", "compiledb", "--jobs", "3"], plan.Build[1].Arguments);
        Assert.Equal(plan.Build[0].Environment, plan.Build[1].Environment);
        Assert.Equal(Path.Combine(temp.Path, "compile_commands.json"), plan.CompilationDatabase);
        Assert.Equal(configuration, File.ReadAllText(Path.Combine(temp.Path, "platformio.ini")));
        Assert.DoesNotContain(plan.Build.SelectMany(command => command.Arguments), argument => argument.Contains("extra_scripts", StringComparison.Ordinal));
    }

    [Fact]
    public void PlatformIO_preserves_an_existing_user_compilation_database()
    {
        using var temp = new TempDirectory();
        temp.Write("platformio.ini", "[env:uno]\nboard=uno\n");
        const string database = "[{\"directory\":\"/reviewed\",\"command\":\"custom-gcc -DKEEP_USER_FLAGS\",\"file\":\"main.c\"}]\n";
        var path = temp.Write("compile_commands.json", database);
        var plan = EmbeddedBuildPlanner.Plan(EmbeddedProjectDetector.Read(temp.Path)!);
        Assert.Single(plan.Build);
        Assert.DoesNotContain("compiledb", plan.Build[0].Arguments);
        Assert.Equal(path, plan.CompilationDatabase);
        Assert.Equal(database, File.ReadAllText(path));
    }

    [Theory]
    [InlineData("main.c", "int main(){}", null)]
    [InlineData("main.py", "print('desktop Python')", null)]
    [InlineData("Cargo.toml", "[package]\nname='desktop'", null)]
    [InlineData("app.uvprojx", "<Project/>", EmbeddedProjectKind.VendorImport)]
    [InlineData("app.ewp", "<project/>", EmbeddedProjectKind.VendorImport)]
    [InlineData("app.cproj", "<Project/>", EmbeddedProjectKind.VendorImport)]
    public void Detection_does_not_assume_ordinary_sources_are_firmware(string file, string content, EmbeddedProjectKind? kind)
    {
        using var temp = new TempDirectory();
        temp.Write(file, content);
        Assert.Equal(kind, EmbeddedProjectDetector.Read(temp.Path)?.Kind);
    }

    /// <summary>
    /// anyhow and serde_json build without std (<c>#![no_std]</c> in lib.rs) for firmware that uses them, but are
    /// libraries for computers too: 0.20.4 took them for embedded Rust firmware, so their tests did not run.
    /// </summary>
    [Fact]
    public void A_no_std_library_is_not_firmware_but_a_no_std_program_or_a_memory_map_is()
    {
        using var temp = new TempDirectory();
        temp.Write("Cargo.toml", "[package]\nname = \"anyhow\"\n");
        temp.Write("src/lib.rs", "#![no_std]\n#[cfg(feature = \"std\")]\nextern crate std;\n");
        Assert.Null(EmbeddedProjectDetector.Read(temp.Path));

        temp.Write("memory.x", "MEMORY { FLASH : ORIGIN = 0x08000000, LENGTH = 512K }");
        Assert.Equal(EmbeddedProjectKind.Rust, EmbeddedProjectDetector.Read(temp.Path)?.Kind);

        using var firmware = new TempDirectory();
        firmware.Write("Cargo.toml", "[package]\nname = \"blinky\"\n");
        firmware.Write("src/main.rs", "#![no_std]\n#![no_main]\n");
        Assert.Equal(EmbeddedProjectKind.Rust, EmbeddedProjectDetector.Read(firmware.Path)?.Kind);
    }

    [Fact]
    public void Embedded_roots_own_their_sources_in_the_existing_detector()
    {
        using var temp = new TempDirectory();
        temp.Write("platformio.ini", "[env:uno]\nboard=uno");
        temp.Write("src/main.cpp", "void setup(){} void loop(){}");
        temp.Write("scripts/build.py", "print('helper')");
        var catalog = ToolchainCatalog.Default with
        {
            Toolchains = [.. ToolchainCatalog.Default.Toolchains.Where(t => t.Id != "platformio"), new ToolchainDefinition
            {
                Id = "platformio", Name = "PlatformIO", Description = "test", Listed = true,
                FileExtensions = [".c", ".cpp", ".h"], ProjectFiles = ["platformio.ini"],
            }],
        };
        var project = Assert.Single(ToolchainProjectDetector.Detect(temp.Path, catalog));
        Assert.Equal("platformio", project.Toolchain.Id);
        Assert.Equal(temp.Path, project.Directory);
        var safeRun = RunPlanner.PlanProject(project)!;
        Assert.Null(safeRun.Build);
        Assert.Equal("pio", safeRun.Run.Program);
        Assert.DoesNotContain("upload", safeRun.Run.Arguments);
        Assert.Contains("compiledb", safeRun.Run.Arguments);
        var firmwareBuild = Assert.Single(safeRun.Preparation!);
        Assert.DoesNotContain("compiledb", firmwareBuild.Arguments);
        Assert.DoesNotContain("upload", firmwareBuild.Arguments);
    }

    [Fact]
    public void Arduino_requires_an_explicit_fqbn_and_emits_compile_database_commands()
    {
        using var temp = new TempDirectory();
        temp.Write(Path.GetFileName(temp.Path) + ".ino", "void setup(){} void loop(){}");
        var project = EmbeddedProjectDetector.Read(temp.Path)!;
        Assert.Empty(EmbeddedBuildPlanner.Plan(project).Build);
        var options = new EmbeddedBuildOptions { Board = "arduino:avr:uno", Port = "/dev/ttyUSB0" };
        var plan = EmbeddedBuildPlanner.Plan(project, options);
        Assert.Equal("arduino-cli", Assert.Single(plan.Build).Program);
        Assert.Contains("--jobs", Assert.Single(plan.Build).Arguments);
        Assert.Contains("--only-compilation-database", Assert.Single(plan.Preparation).Arguments);
        Assert.Contains("--input-dir", Assert.Single(plan.Upload).Arguments);
        Assert.EndsWith("compile_commands.json", plan.CompilationDatabase, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("include($ENV{IDF_PATH}/tools/cmake/project.cmake)\ninclude(idf.cmake)", EmbeddedProjectKind.EspIdf)]
    [InlineData("find_package(Zephyr REQUIRED HINTS $ENV{ZEPHYR_BASE})", EmbeddedProjectKind.Zephyr)]
    [InlineData("include(pico_sdk_import.cmake)\npico_sdk_init()", EmbeddedProjectKind.PicoSdk)]
    public void Native_CMake_frameworks_are_classified_before_generic_cpp(string cmake, EmbeddedProjectKind kind)
    {
        using var temp = new TempDirectory();
        temp.Write("CMakeLists.txt", cmake);
        Assert.Equal(kind, EmbeddedProjectDetector.Read(temp.Path)!.Kind);
    }

    [Fact]
    public void EspIdf_reads_target_without_running_reconfiguration_or_generating_efuse_commands()
    {
        using var temp = new TempDirectory();
        temp.Write("CMakeLists.txt", "include(idf.cmake)");
        temp.Write("sdkconfig", "CONFIG_IDF_TARGET=\"esp32c3\"\n");
        var project = EmbeddedProjectDetector.Read(temp.Path)!;
        Assert.Equal("esp32c3", project.Target);
        var plan = EmbeddedBuildPlanner.Plan(project, new EmbeddedBuildOptions { Port = "/dev/ttyACM0", Jobs = 3 });
        Assert.Contains("-DIDF_TARGET=esp32c3", Assert.Single(plan.Build).Arguments);
        Assert.Equal("3", Assert.Single(plan.Build).Environment["TABSTUDIO_EMBEDDED_JOBS"]);
        Assert.Equal("flash", Assert.Single(plan.Upload).Arguments[^1]);
        Assert.Empty(plan.Preparation);
    }

    [Fact]
    public void Cube_ioc_is_not_mistaken_for_a_compilable_build_script()
    {
        using var temp = new TempDirectory();
        temp.Write("sensor.ioc", "Mcu.CPN=STM32F411CEU6\n");
        var project = EmbeddedProjectDetector.Read(temp.Path)!;
        Assert.Equal("STM32F411CEU6", project.Target);
        Assert.Empty(EmbeddedBuildPlanner.Plan(project).Build);
        temp.Write("Makefile", "all:\n\tarm-none-eabi-gcc main.c\n");
        var plan = EmbeddedBuildPlanner.Plan(EmbeddedProjectDetector.Read(temp.Path)!);
        Assert.Equal("make", Assert.Single(plan.Build).Program);
        Assert.Empty(plan.Upload);
    }

    [Fact]
    public void Zephyr_keeps_qualified_board_names_as_one_argument()
    {
        using var temp = new TempDirectory();
        temp.Write("CMakeLists.txt", "find_package(Zephyr REQUIRED)");
        var project = EmbeddedProjectDetector.Read(temp.Path)!;
        Assert.Empty(EmbeddedBuildPlanner.Plan(project).Build);
        var plan = EmbeddedBuildPlanner.Plan(project, new EmbeddedBuildOptions
        {
            Board = "nrf5340dk/nrf5340/cpuapp", Runner = "openocd", Port = "granted-probe:1",
        });
        Assert.Contains("nrf5340dk/nrf5340/cpuapp", Assert.Single(plan.Build).Arguments);
        Assert.Contains("--build-opt=-j2", Assert.Single(plan.Build).Arguments);
        Assert.Contains("--pristine=auto", Assert.Single(plan.Build).Arguments);
        Assert.Equal(["flash", "--build-dir", Path.Combine(temp.Path, ".tabstudio", "embedded", "build"), "--runner", "openocd"], Assert.Single(plan.Upload).Arguments);
    }

    [Fact]
    public void Pico_respects_declared_board_and_never_guesses_the_firmware_filename()
    {
        using var temp = new TempDirectory();
        temp.Write("CMakeLists.txt", "include(pico_sdk_import.cmake)\nset(PICO_BOARD pico2_w)\n");
        var project = EmbeddedProjectDetector.Read(temp.Path)!;
        Assert.Equal("pico2_w", project.Target);
        var plan = EmbeddedBuildPlanner.Plan(project);
        Assert.Contains("-DPICO_BOARD=pico2_w", Assert.Single(plan.Preparation).Arguments);
        Assert.Single(plan.Build);
        Assert.Empty(plan.Upload);
    }

    [Fact]
    public void Rust_embedded_target_preserves_cargo_runner_and_does_not_run_host_binary()
    {
        using var temp = new TempDirectory();
        temp.Write("Cargo.toml", "[package]\nname='firmware'");
        temp.Write(".cargo/config.toml", "[build]\ntarget = 'thumbv7em-none-eabihf'\n[target.thumbv7em-none-eabihf]\nrunner='probe-rs run --chip nRF52840_xxAA'\n");
        var project = EmbeddedProjectDetector.Read(temp.Path)!;
        Assert.Equal(EmbeddedProjectKind.Rust, project.Kind);
        Assert.Equal("thumbv7em-none-eabihf", project.Target);
        var plan = EmbeddedBuildPlanner.Plan(project);
        Assert.Equal("embedded-rust", plan.ToolchainId);
        Assert.Contains("thumbv7em-none-eabihf", Assert.Single(plan.Build).Arguments);
        Assert.Empty(plan.Upload);
        Assert.DoesNotContain("run", plan.Build[0].Arguments);
    }

    [Fact]
    public void MicroPython_sync_excludes_credentials_and_build_folders()
    {
        using var temp = new TempDirectory();
        temp.Write(".micropython", "");
        temp.Write("main.py", "print('board')");
        temp.Write("secrets.json", "{}");
        temp.Write(".venv/leak.py", "secret='hidden'");
        var plan = EmbeddedBuildPlanner.Plan(EmbeddedProjectDetector.Read(temp.Path)!, new EmbeddedBuildOptions { Port = "/dev/ttyACM0" });
        var command = Assert.Single(plan.Upload);
        Assert.Equal("mpremote", command.Program);
        Assert.Equal(":main.py", command.Arguments[^1]);
        Assert.DoesNotContain(command.Arguments, a => a.Contains("secrets", StringComparison.Ordinal));
    }

    [Fact]
    public void Build_and_dependency_folders_and_symbolic_links_are_not_recursed()
    {
        using var temp = new TempDirectory();
        temp.Write("app/platformio.ini", "[env:uno]");
        temp.Write("app/.pio/libdeps/library/platformio.ini", "[env:wrong]");
        temp.Write("build/generated/platformio.ini", "[env:wrong]");
        temp.Write("vendor/dependency/platformio.ini", "[env:wrong]");
        Assert.Single(EmbeddedProjectDetector.Detect(temp.Path, 8));
    }

    [Fact]
    public void Config_is_case_insensitive_but_invalid_json_and_option_injection_fail_visibly()
    {
        using var temp = new TempDirectory();
        temp.Write("platformio.ini", "[env:uno]");
        temp.Write(".tabstudio/embedded.json", "{\"environment\":\"uno\",\"port\":\"/dev/ttyUSB0\",\"jobs\":3}");
        Assert.Equal(3, EmbeddedBuildPlanner.LoadOptions(temp.Path).Jobs);
        Assert.Single(EmbeddedBuildPlanner.Plan(EmbeddedProjectDetector.Read(temp.Path)!).Upload);
        temp.Write(".tabstudio/embedded.json", "{broken");
        Assert.Throws<JsonException>(() => EmbeddedBuildPlanner.LoadOptions(temp.Path));
        Assert.Throws<ArgumentException>(() => EmbeddedBuildPlanner.Plan(EmbeddedProjectDetector.Read(temp.Path)!,
            new EmbeddedBuildOptions { Environment = "--target=erase" }));
    }

    [Theory]
    [InlineData("arm-none-eabi-gcc", EmbeddedProjectKind.GccCMakeArm, "embedded-arm")]
    [InlineData("avr-gcc", EmbeddedProjectKind.GccCMakeAvr, "embedded-avr")]
    public void Imported_CMake_firmware_is_built_as_firmware_and_never_executed_as_a_tablet_program(string compiler, EmbeddedProjectKind kind, string toolchainId)
    {
        using var temp = new TempDirectory();
        temp.Write("CMakeLists.txt", "set(CMAKE_TOOLCHAIN_FILE tabstudio-gcc.cmake)\nproject(firmware C)\n");
        temp.Write("tabstudio-gcc.cmake", "set(CMAKE_SYSTEM_NAME Generic)\nset(CMAKE_C_COMPILER " + compiler + ")\n");
        var project = EmbeddedProjectDetector.Read(temp.Path)!;
        Assert.Equal(kind, project.Kind);
        var plan = EmbeddedBuildPlanner.Plan(project);
        Assert.Equal(toolchainId, plan.ToolchainId);
        Assert.Equal("cmake", Assert.Single(plan.Build).Program);
        Assert.Empty(plan.Upload);
        var run = EmbeddedBuildPlanner.RunPlan(project);
        Assert.Null(run.Build);
        Assert.Equal("cmake", run.Run.Program);
    }

    [Fact]
    public void An_Arduino_library_builds_one_explicit_example_with_its_own_library_path()
    {
        using var temp = new TempDirectory();
        temp.Write("library.properties", "name=SensorDriver\narchitectures=*");
        temp.Write("examples/ReadSensor/ReadSensor.ino", "void setup(){} void loop(){}");
        var project = EmbeddedProjectDetector.Read(temp.Path)!;
        Assert.Equal("library.properties", project.Marker);
        Assert.Empty(EmbeddedBuildPlanner.Plan(project, new EmbeddedBuildOptions { Board = "arduino:avr:uno" }).Build);
        var plan = EmbeddedBuildPlanner.Plan(project, new EmbeddedBuildOptions { Board = "arduino:avr:uno", Sketch = "examples/ReadSensor" });
        var build = Assert.Single(plan.Build);
        Assert.Contains("--library", build.Arguments);
        Assert.Contains(temp.Path, build.Arguments);
        Assert.Equal(temp.Resolve("examples/ReadSensor"), build.Arguments[^1]);
        Assert.Throws<ArgumentException>(() => EmbeddedBuildPlanner.Plan(project,
            new EmbeddedBuildOptions { Board = "arduino:avr:uno", Sketch = "../outside" }));
    }

    [Fact]
    public void PlatformIO_interpolated_defaults_are_resolved_by_the_native_tool_without_selecting_another_environment()
    {
        using var temp = new TempDirectory();
        temp.Write("platformio.ini", "[platformio]\ndefault_envs=${local.selected}\nextra_configs=local.ini\n[env:uno]\nboard=uno\n");
        var project = EmbeddedProjectDetector.Read(temp.Path)!;
        Assert.True(project.HasNativeEnvironmentSelection);
        Assert.Null(project.DefaultEnvironment);
        var plan = EmbeddedBuildPlanner.Plan(project, new EmbeddedBuildOptions { Port = "/dev/ttyUSB0" });
        Assert.Equal(2, plan.Build.Count);
        Assert.All(plan.Build, command => Assert.DoesNotContain("--environment", command.Arguments));
        Assert.Empty(plan.Upload);
    }

    [Fact]
    public void Cube_presets_preserve_vendor_toolchain_settings_and_ignore_checked_in_host_caches()
    {
        using var temp = new TempDirectory();
        temp.Write("blink.ioc", "Mcu.Name=STM32F401RE\n");
        temp.Write("CMakeLists.txt", "project(blink C)\n");
        temp.Write("CMakePresets.json", """
            {"version":3,"configurePresets":[
              {"name":"default","hidden":true,"toolchainFile":"${sourceDir}/cmake/gcc-arm-none-eabi.cmake"},
              {"name":"Debug","inherits":"default"},{"name":"Release","inherits":"default"}]}
            """);
        temp.Write("build/Debug/CMakeCache.txt", "CMAKE_HOME_DIRECTORY:INTERNAL=C:/old-machine/blink\n");
        var plan = EmbeddedBuildPlanner.Plan(EmbeddedProjectDetector.Read(temp.Path)!);
        var configure = Assert.Single(plan.Preparation);
        Assert.Equal(["--preset", "Debug", "-S", temp.Path, "-B", temp.Resolve(".tabstudio/embedded/build"), "-DCMAKE_EXPORT_COMPILE_COMMANDS=ON"], configure.Arguments);
        Assert.Equal("cmake", Assert.Single(plan.Build).Program);
    }

    [Fact]
    public void Multiple_board_specific_CMake_presets_require_explicit_selection_and_keep_native_inheritance()
    {
        using var temp = new TempDirectory();
        temp.Write("CMakeLists.txt", "include(pico_sdk_import.cmake)\n");
        temp.Write("CMakePresets.json", """{"version":3,"configurePresets":[{"name":"pico"},{"name":"pico2"}]}""");
        var project = EmbeddedProjectDetector.Read(temp.Path)!;
        Assert.Empty(EmbeddedBuildPlanner.Plan(project).Build);
        var plan = EmbeddedBuildPlanner.Plan(project, new EmbeddedBuildOptions { Environment = "pico2" });
        Assert.Equal("pico2", Assert.Single(plan.Preparation).Arguments[1]);
        Assert.DoesNotContain(Assert.Single(plan.Preparation).Arguments, argument => argument.StartsWith("-DPICO_BOARD=", StringComparison.Ordinal));
    }
}
