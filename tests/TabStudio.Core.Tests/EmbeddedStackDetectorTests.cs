using TabStudio.Core.Compatibility;
using TabStudio.Core.Detection;
using TabStudio.Core.Workspaces;
using TabStudio.Testing;

namespace TabStudio.Core.Tests;

public sealed class EmbeddedStackDetectorTests
{
    [Theory]
    [InlineData("platformio.ini", "[env:uno]", StackKind.PlatformIO)]
    [InlineData("blink.ino", "void setup(){}", StackKind.Arduino)]
    [InlineData("library.properties", "name=Example", StackKind.Arduino)]
    [InlineData("board.ioc", "Mcu.Name=STM32F411", StackKind.Stm32Cube)]
    [InlineData("board.uvprojx", "<Project/>", StackKind.EmbeddedVendor)]
    [InlineData("board.ewp", "<project/>", StackKind.EmbeddedVendor)]
    [InlineData("board.cproj", "<Project/>", StackKind.EmbeddedVendor)]
    [InlineData(".micropython", "", StackKind.MicroPython)]
    public void Embedded_markers_are_visible_in_the_repository_stack(string file, string content, StackKind expected)
    {
        using var temp = new TempDirectory();
        temp.Write(file, content);
        Assert.Equal(file, StackDetector.Detect(temp.Path).Get(expected)!.Evidence);
    }

    [Theory]
    [InlineData("include($ENV{IDF_PATH}/tools/cmake/project.cmake)", StackKind.EspIdf)]
    [InlineData("find_package(Zephyr REQUIRED HINTS $ENV{ZEPHYR_BASE})", StackKind.Zephyr)]
    [InlineData("include(pico_sdk_import.cmake)", StackKind.PicoSdk)]
    public void Embedded_CMake_frameworks_keep_cpp_and_framework_evidence(string text, StackKind expected)
    {
        using var temp = new TempDirectory();
        temp.Write("CMakeLists.txt", text);
        var stack = StackDetector.Detect(temp.Path);
        Assert.True(stack.Has(StackKind.Cpp));
        Assert.True(stack.Has(expected));
    }

    [Fact]
    public void Commented_framework_examples_do_not_classify_a_desktop_CMake_project_as_firmware()
    {
        using var temp = new TempDirectory();
        temp.Write("CMakeLists.txt", "cmake_minimum_required(VERSION 3.28)\n# find_package(Zephyr)\n# include(pico_sdk_import.cmake)\n");
        Assert.False(StackDetector.Detect(temp.Path).Has(StackKind.Zephyr));
        Assert.False(StackDetector.Detect(temp.Path).Has(StackKind.PicoSdk));
    }

    [Fact]
    public void Embedded_Rust_is_distinguished_by_explicit_linker_memory_configuration()
    {
        using var temp = new TempDirectory();
        temp.Write("Cargo.toml", "[package]");
        temp.Write("memory.x", "MEMORY { FLASH : ORIGIN = 0, LENGTH = 64K }");
        Assert.True(StackDetector.Detect(temp.Path).Has(StackKind.EmbeddedRust));
    }

    [Fact]
    public void Embedded_firmware_is_never_reported_as_an_in_process_tablet_program()
    {
        var project = new ProjectInfo { Name = "Firmware", FilePath = "/firmware/platformio.ini", Kind = ProjectKind.EmbeddedFirmware };
        var report = CompatibilityDoctor.Diagnose(project);
        Assert.Equal(RunHost.Toolchain, report.Host);
        Assert.Equal(CompatibilityStatus.NeedsToolchain, report.Status);
        Assert.False(report.CanRunToday);
        Assert.Contains("granted device", report.Explanation, StringComparison.Ordinal);
    }
}
