using TabStudio.Testing;
using TabStudio.Toolchains.Catalog;
using TabStudio.Toolchains.Embedded.Installation;
using TabStudio.Toolchains.Embedded.Projects;

namespace TabStudio.Toolchains.Tests.Embedded;

/// <summary>
/// The SDK version a firmware project asks for, read from its own files, and the one clear message when the SDK on the
/// tablet does not suit it. raspberrypi/pico-examples requires Pico SDK 2.3.0, which TabStudio 0.19 (Pico SDK 2.1.1)
/// only learned from CMake's fatal error.
/// </summary>
public sealed class EmbeddedSdkRequirementsTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    /// <summary>The top of raspberrypi/pico-examples' CMakeLists.txt at 0d62f75 (September 2026).</summary>
    private const string PicoExamples = """
        cmake_minimum_required(VERSION 3.12)

        # Pull in SDK (must be before project)
        include(pico_sdk_import.cmake)

        include(pico_extras_import_optional.cmake)

        project(pico_examples C CXX ASM)
        set(CMAKE_C_STANDARD 11)
        set(CMAKE_CXX_STANDARD 17)

        if (PICO_SDK_VERSION_STRING VERSION_LESS "2.3.0")
            message(FATAL_ERROR "Raspberry Pi Pico SDK version 2.3.0 (or later) required. Your version is ${PICO_SDK_VERSION_STRING}")
        endif()
        """;

    [Fact]
    public void Pico_examples_needs_Pico_SDK_2_3_0_and_the_message_offers_the_pinned_release()
    {
        var project = Project(EmbeddedProjectKind.PicoSdk, "CMakeLists.txt", PicoExamples);

        var requirement = Assert.IsType<EmbeddedSdkRequirement>(EmbeddedSdkRequirements.Read(project));
        Assert.Equal("pico-sdk", requirement.ToolchainId);
        Assert.Equal("2.3.0", requirement.Minimum);
        Assert.Equal("CMakeLists.txt:12", requirement.Source);

        var old = EmbeddedSdkRequirements.Check(requirement, "2.1.1", "2.3.1");
        Assert.False(old.Satisfied);
        Assert.Equal("This project needs Pico SDK ≥ 2.3.0 (CMakeLists.txt:12); 2.1.1 is installed — Install 2.3.1.", old.Message);
        Assert.Equal("2.3.1", old.InstallVersion);
        Assert.True(EmbeddedSdkRequirements.Check(requirement, "2.3.1", "2.3.1").Satisfied);
        Assert.True(EmbeddedSdkRequirements.Check(requirement, "2.3", "2.3.1").Satisfied);
        Assert.Equal("pico_examples", EmbeddedSdkRequirements.ProgramName(project));
        Assert.Equal("blink", EmbeddedSdkRequirements.ProgramName(project, "blink"));
    }

    [Fact]
    public void A_requirement_newer_than_anything_pinned_says_so_without_offering_an_install()
    {
        var requirement = new EmbeddedSdkRequirement("pico-sdk", "Pico SDK", "2.9.0", null, "CMakeLists.txt:3");

        var verdict = EmbeddedSdkRequirements.Check(requirement, null, "2.3.1");

        Assert.False(verdict.Satisfied);
        Assert.Null(verdict.InstallVersion);
        Assert.Contains("no Pico SDK is installed", verdict.Message, StringComparison.Ordinal);
        Assert.Contains("the newest Pico SDK TabStudio has verified is 2.3.1", verdict.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_pinned_Pico_SDK_is_the_newest_release_and_satisfies_pico_examples()
    {
        Assert.Equal(EmbeddedSdkRequirements.PicoSdkReleases[0].Commit, EmbeddedSdkBootstrap.Recipes.Get("pico-sdk")!.Revision);
        Assert.Equal(EmbeddedSdkRequirements.PicoSdkReleases[0].Version, ToolchainCatalog.Default.Find("pico-sdk")!.PinnedVersion);
        Assert.All(EmbeddedSdkRequirements.PicoSdkReleases, release => Assert.Matches("^[0-9a-f]{40}$", release.Commit));
        var requirement = EmbeddedSdkRequirements.Read(Project(EmbeddedProjectKind.PicoSdk, "CMakeLists.txt", PicoExamples))!;
        Assert.True(EmbeddedSdkRequirements.Check(requirement, ToolchainCatalog.Default.Find("pico-sdk")!.PinnedVersion, null).Satisfied);
    }

    [Fact]
    public void CMake_s_version_error_becomes_the_requirement_it_states()
    {
        var output = """
            -- The C compiler identification is GNU 13.2.1
            CMake Error at CMakeLists.txt:13 (message):
              Raspberry Pi Pico SDK version 2.3.0 (or later) required.  Your version is
              2.1.1
            """;

        Assert.Equal(("2.3.0", "2.1.1"), EmbeddedSdkRequirements.FromCMakeOutput(output));
        Assert.Null(EmbeddedSdkRequirements.FromCMakeOutput("-- Configuring done"));
    }

    [Fact]
    public void The_installed_SDK_s_version_comes_from_its_own_version_file()
    {
        _temp.Write("pico-sdk/pico_sdk_version.cmake", """
            set(PICO_SDK_VERSION_MAJOR 2)
            set(PICO_SDK_VERSION_MINOR 1)
            set(PICO_SDK_VERSION_REVISION 1)
            """);
        _temp.Write("esp-idf/tools/cmake/version.cmake", "set(IDF_VERSION_MAJOR 5)\nset(IDF_VERSION_MINOR 4)\nset(IDF_VERSION_PATCH 1)\n");
        _temp.Write("zephyr/VERSION", "VERSION_MAJOR = 4\nVERSION_MINOR = 1\nPATCHLEVEL = 0\nVERSION_TWEAK = 0\nEXTRAVERSION =\n");

        Assert.Equal("2.1.1", EmbeddedSdkRequirements.InstalledVersion("pico-sdk", _temp.Resolve("pico-sdk")));
        Assert.Equal("5.4.1", EmbeddedSdkRequirements.InstalledVersion("esp-idf", _temp.Resolve("esp-idf")));
        Assert.Equal("4.1.0", EmbeddedSdkRequirements.InstalledVersion("zephyr", _temp.Resolve("zephyr")));
        Assert.Null(EmbeddedSdkRequirements.InstalledVersion("pico-sdk", _temp.Resolve("missing")));
    }

    [Theory]
    [InlineData("main/idf_component.yml", "dependencies:\n  idf: \">=5.0\"\n  espressif/led_strip: \"^2.5.0\"\n", "5.0.0", null)]
    [InlineData("main/idf_component.yml", "dependencies:\n  idf:\n    version: \">=5.3.0\"\n", "5.3.0", null)]
    [InlineData("dependencies.lock", "dependencies:\n  idf:\n    source:\n      type: idf\n    version: 5.4.1\ndirect_dependencies:\n- idf\n", null, "5.4.1")]
    public void ESP_IDF_projects_ask_for_a_version_in_their_component_manifest_or_lock(string file, string text, string? minimum, string? exact)
    {
        var requirement = EmbeddedSdkRequirements.Read(Project(EmbeddedProjectKind.EspIdf, file, text));

        Assert.NotNull(requirement);
        Assert.Equal("esp-idf", requirement.ToolchainId);
        Assert.Equal(minimum, requirement.Minimum);
        Assert.Equal(exact, requirement.Exact);
    }

    [Fact]
    public void A_Zephyr_application_pins_Zephyr_in_its_west_manifest()
    {
        var project = Project(EmbeddedProjectKind.Zephyr, "west.yml", """
            manifest:
              remotes:
                - name: zephyrproject-rtos
                  url-base: https://github.com/zephyrproject-rtos
              projects:
                - name: zephyr
                  remote: zephyrproject-rtos
                  revision: v4.4.2
                  import: true
            """);

        var requirement = Assert.IsType<EmbeddedSdkRequirement>(EmbeddedSdkRequirements.Read(project));

        Assert.Equal("v4.4.2", requirement.Exact);
        Assert.Equal("4.4.2", requirement.Minimum);
        Assert.Equal("west.yml:8", requirement.Source);
        Assert.Contains("Zephyr ≥ 4.4.2", EmbeddedSdkRequirements.Check(requirement, "4.1.0", "4.1.0").Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_PlatformIO_environment_names_its_platform_version()
    {
        var project = Project(EmbeddedProjectKind.PlatformIO, "platformio.ini", "[env:esp32dev]\nplatform = espressif32 @ 6.4.0\nboard = esp32dev\nframework = arduino\n");

        var requirement = Assert.IsType<EmbeddedSdkRequirement>(EmbeddedSdkRequirements.Read(project));

        Assert.Equal("6.4.0", requirement.Exact);
        Assert.Equal("PlatformIO platform espressif32", requirement.SdkName);
        Assert.True(EmbeddedSdkRequirements.Check(requirement, null, null).Satisfied);
    }

    [Fact]
    public void A_firmware_build_is_never_named_after_a_build_file()
    {
        var cmake = Project(EmbeddedProjectKind.GccCMakeArm, "CMakeLists.txt", "cmake_minimum_required(VERSION 3.20)\n");
        var ini = Project(EmbeddedProjectKind.PlatformIO, "platformio.ini", "[env:uno]\nplatform = atmelavr\n");

        Assert.Equal(cmake.Name, EmbeddedSdkRequirements.ProgramName(cmake));
        Assert.Equal(ini.Name, EmbeddedSdkRequirements.ProgramName(ini));
        Assert.NotEqual("CMakeLists.txt", EmbeddedSdkRequirements.ProgramName(cmake));
    }

    private EmbeddedProject Project(EmbeddedProjectKind kind, string file, string text)
    {
        var folder = _temp.Resolve(kind + "-" + Guid.NewGuid().ToString("N")[..6]);
        _temp.Write(Path.Combine(Path.GetFileName(folder), file), text);
        var marker = kind switch
        {
            EmbeddedProjectKind.PlatformIO => "platformio.ini",
            EmbeddedProjectKind.EspIdf => "CMakeLists.txt",
            EmbeddedProjectKind.Zephyr => "prj.conf",
            _ => "CMakeLists.txt",
        };
        return new EmbeddedProject(kind, folder, marker, [], null, null, []);
    }

    public void Dispose() => _temp.Dispose();
}
