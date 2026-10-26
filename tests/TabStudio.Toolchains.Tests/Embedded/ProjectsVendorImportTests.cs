using System.Xml;
using TabStudio.Testing;
using TabStudio.Toolchains.Embedded.Projects;

namespace TabStudio.Toolchains.Tests.Embedded;

public sealed class ProjectsVendorImportTests
{
    [Fact]
    public void Keil_import_retains_sources_includes_defines_and_reports_scatter_and_vendor_extensions()
    {
        using var folder = new TempDirectory();
        var source = folder.Write("original/main.c", "int main(void) { return 0; }\n");
        folder.Write("original/include/device.h", "// device\n");
        var original = """
            <Project><Targets><Target><TargetName>Debug</TargetName>
            <TargetOption><TargetCommonOption><Device>STM32F103C8</Device><Cpu>CPUTYPE("Cortex-M3")</Cpu></TargetCommonOption>
            <TargetArmAds><Cads><VariousControls><Define>USE_HAL_DRIVER;STM32F103xB</Define><IncludePath>include</IncludePath><MiscControls>--signed_chars</MiscControls></VariousControls></Cads>
            <LDads><ScatterFile>board.sct</ScatterFile></LDads></TargetArmAds></TargetOption>
            <Groups><Group><Files><File><FilePath>main.c</FilePath></File></Files></Group></Groups>
            </Target></Targets></Project>
            """;
        var project = folder.Write("original/firmware.uvprojx", original);
        var destination = Path.Combine(folder.Path, "converted");

        var result = VendorProjectImporter.Import(project, destination);

        var cmake = File.ReadAllText(result.CMakeFile);
        Assert.Contains("../original/main.c", cmake, StringComparison.Ordinal);
        Assert.Contains("../original/include", cmake, StringComparison.Ordinal);
        Assert.Contains("USE_HAL_DRIVER", cmake, StringComparison.Ordinal);
        Assert.Contains("-mcpu=cortex-m3", cmake, StringComparison.Ordinal);
        Assert.Contains(result.Notes, n => n.Code == "EMBIMPORT101" && n.Message.Contains("scatter", StringComparison.OrdinalIgnoreCase) && n.Line > 0);
        Assert.Contains(result.Notes, n => n.Code == "EMBIMPORT102");
        Assert.Equal(original, File.ReadAllText(project));
        Assert.Equal("int main(void) { return 0; }\n", File.ReadAllText(source));
        Assert.True(File.Exists(result.NotesFile));
        Assert.True(File.Exists(Path.Combine(destination, "IMPORT.md")));
    }

    [Fact]
    public void Multiple_targets_require_an_exact_configuration_instead_of_silently_choosing_one()
    {
        using var folder = new TempDirectory();
        var project = folder.Write("original/blink.uvprojx", "<Project><Targets><Target><TargetName>Debug</TargetName></Target><Target><TargetName>Release</TargetName></Target></Targets></Project>");
        var destination = Path.Combine(folder.Path, "converted");

        Assert.Throws<InvalidDataException>(() => VendorProjectImporter.Import(project, destination));
        Assert.False(Directory.Exists(destination));
        Assert.Throws<InvalidDataException>(() => VendorProjectImporter.Import(project, destination, "Missing"));
        Assert.Equal("Release", VendorProjectImporter.Import(project, destination, "Release").Configuration);
    }

    [Fact]
    public void Iar_import_resolves_project_relative_paths_and_reports_linker_configuration()
    {
        using var folder = new TempDirectory();
        folder.Write("original/main.c", "int main(void) { return 0; }");
        folder.Write("original/inc/board.h", "");
        var project = folder.Write("original/firmware.ewp", """
            <project><configuration><name>Debug</name><toolchain><name>ARM</name></toolchain><settings><name>ICCARM</name><data>
            <option><name>CCIncludePath2</name><state>$PROJ_DIR$/inc</state></option>
            <option><name>CCDefines</name><state>BOARD=1</state></option>
            <option><name>IlinkIcfFile</name><state>$PROJ_DIR$/memory.icf</state></option>
            </data></settings></configuration><group><name>App</name><file><name>$PROJ_DIR$/main.c</name></file></group></project>
            """);

        var result = VendorProjectImporter.Import(project, Path.Combine(folder.Path, "converted"));

        Assert.Contains("../original/main.c", File.ReadAllText(result.CMakeFile), StringComparison.Ordinal);
        Assert.Contains("BOARD=1", File.ReadAllText(result.CMakeFile), StringComparison.Ordinal);
        Assert.Contains(result.Notes, n => n.Code == "EMBIMPORT201");
        Assert.Contains(result.Notes, n => n.Code == "EMBIMPORT202");
    }

    [Fact]
    public void Atmel_avr_import_reads_namespaced_xml_and_selects_the_avr_gcc_toolchain()
    {
        using var folder = new TempDirectory();
        folder.Write("original/main.c", "int main(void) { for (;;) {} }");
        var project = folder.Write("original/blink.cproj", """
            <Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003"><PropertyGroup><ToolchainName>com.Atmel.AVRGCC8.C</ToolchainName><avrdevice>ATmega328P</avrdevice></PropertyGroup>
            <PropertyGroup Condition="'$(Configuration)|$(Platform)' == 'Debug|AVR'"><ToolchainSettings><avr-gcc><avrgcc.compiler.symbols.DefinedSymbols><ListValues><Value>F_CPU=16000000UL</Value></ListValues></avrgcc.compiler.symbols.DefinedSymbols></avr-gcc></ToolchainSettings></PropertyGroup>
            <ItemGroup><Compile Include="main.c"/><Compile Include="ignore.c"><ExcludedFromBuild>true</ExcludedFromBuild></Compile></ItemGroup></Project>
            """);

        var result = VendorProjectImporter.Import(project, Path.Combine(folder.Path, "converted"));

        Assert.Equal("Debug", result.Configuration);
        Assert.Contains("avr-gcc", File.ReadAllText(Path.Combine(result.Directory, "tabstudio-gcc.cmake")), StringComparison.Ordinal);
        Assert.Contains("-mmcu=atmega328p", File.ReadAllText(result.CMakeFile), StringComparison.Ordinal);
        Assert.Contains("F_CPU=16000000UL", File.ReadAllText(result.CMakeFile), StringComparison.Ordinal);
        Assert.DoesNotContain("ignore.c", File.ReadAllText(result.CMakeFile), StringComparison.Ordinal);
    }

    [Fact]
    public void Xml_entities_are_refused_before_any_destination_is_created()
    {
        using var folder = new TempDirectory();
        var project = folder.Write("original/unsafe.uvprojx", "<!DOCTYPE Project [<!ENTITY secret SYSTEM 'file:///etc/passwd'>]><Project>&secret;</Project>");
        var destination = Path.Combine(folder.Path, "converted");

        Assert.Throws<XmlException>(() => VendorProjectImporter.Import(project, destination));
        Assert.False(Directory.Exists(destination));
    }

    [Fact]
    public void Import_refuses_existing_destinations_and_any_folder_under_the_original()
    {
        using var folder = new TempDirectory();
        var project = folder.Write("original/blink.uvprojx", SingleTarget("main.c"));
        folder.Write("converted/keep.txt", "unchanged");

        Assert.Throws<InvalidDataException>(() => VendorProjectImporter.Import(project, Path.Combine(folder.Path, "converted")));
        Assert.Throws<InvalidDataException>(() => VendorProjectImporter.Import(project, Path.Combine(folder.Path, "original", "converted")));
        Assert.Equal("unchanged", File.ReadAllText(Path.Combine(folder.Path, "converted", "keep.txt")));
    }

    [Fact]
    public void Unresolved_windows_and_vendor_paths_are_reported_and_excluded()
    {
        using var folder = new TempDirectory();
        var project = folder.Write("original/blink.uvprojx", """
            <Project><Targets><Target><TargetName>Debug</TargetName><Groups><Group><Files>
            <File><FilePath>C:\Vendor\startup.c</FilePath></File><File><FilePath>$(SDK_ROOT)/driver.c</FilePath></File>
            </Files></Group></Groups></Target></Targets></Project>
            """);

        var result = VendorProjectImporter.Import(project, Path.Combine(folder.Path, "converted"));

        Assert.Equal(2, result.Notes.Count(n => n.Code == "EMBIMPORT401"));
        Assert.Contains("No source files were imported", File.ReadAllText(result.CMakeFile), StringComparison.Ordinal);
        Assert.DoesNotContain("SDK_ROOT", File.ReadAllText(result.CMakeFile), StringComparison.Ordinal);
    }

    [Fact]
    public void Cmake_paths_use_nonexpanding_bracket_arguments_with_collision_safe_delimiters()
    {
        using var folder = new TempDirectory();
        folder.Write("original/a]]b.c", "int main(void) { return 0; }");
        var project = folder.Write("original/blink.uvprojx", SingleTarget("a]]b.c"));

        var result = VendorProjectImporter.Import(project, Path.Combine(folder.Path, "converted"));

        Assert.Contains("[=[../original/a]]b.c]=]", File.ReadAllText(result.CMakeFile), StringComparison.Ordinal);
    }

    [Fact]
    public void Unknown_iar_architectures_are_refused_instead_of_using_arm_gcc()
    {
        using var folder = new TempDirectory();
        var project = folder.Write("original/blink.ewp", "<project><configuration><name>Debug</name><toolchain><name>MSP430</name></toolchain></configuration></project>");

        Assert.Throws<InvalidDataException>(() => VendorProjectImporter.Import(project, Path.Combine(folder.Path, "converted")));
    }

    [Fact]
    public void Symlinked_destination_ancestors_cannot_redirect_an_import_into_the_original()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var folder = new TempDirectory();
        var project = folder.Write("original/blink.uvprojx", SingleTarget("main.c"));
        Directory.CreateSymbolicLink(Path.Combine(folder.Path, "alias"), Path.Combine(folder.Path, "original"));

        Assert.Throws<InvalidDataException>(() => VendorProjectImporter.Import(project, Path.Combine(folder.Path, "alias", "converted")));
        Assert.False(Directory.Exists(Path.Combine(folder.Path, "original", "converted")));
    }

    private static string SingleTarget(string source) => "<Project><Targets><Target><TargetName>Debug</TargetName><Groups><Group><Files><File><FilePath>"
        + source + "</FilePath></File></Files></Group></Groups></Target></Targets></Project>";
}
