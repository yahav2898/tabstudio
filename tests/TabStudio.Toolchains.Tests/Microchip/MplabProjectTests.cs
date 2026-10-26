using System.Xml;
using TabStudio.Testing;
using TabStudio.Toolchains.Microchip;

namespace TabStudio.Toolchains.Tests.Microchip;

public sealed class MplabProjectTests
{
    [Fact]
    public void Reads_configurations_packs_exclusions_and_per_file_options_without_losing_tool_options()
    {
        using var folder = new TempDirectory();
        var file = folder.Write(MplabProject.Marker, """
            <configurationDescriptor version="65">
              <logicalFolder name="root" projectFiles="true">
                <logicalFolder name="SourceFiles"><itemPath>main.c</itemPath><itemPath>src\other.c</itemPath></logicalFolder>
                <logicalFolder name="ExternalFiles" projectFiles="false"><itemPath>Makefile</itemPath></logicalFolder>
              </logicalFolder>
              <projectmakefile>Makefile</projectmakefile>
              <confs>
                <conf name="production" type="2">
                  <toolsSet><targetDevice>PIC18F45K22</targetDevice><languageToolchain>XC8</languageToolchain><languageToolchainVersion>4.00</languageToolchainVersion></toolsSet>
                  <packs><pack vendor="Microchip" name="PIC18F-K_DFP" version="1.17.312"/></packs>
                  <HI-TECH-COMP><property key="define-macros" value="BOARD=1;LED=0"/><property key="extra-include-directories" value="include;../shared"/></HI-TECH-COMP>
                  <HI-TECH-LINK><property key="define-macros" value="link-value"/></HI-TECH-LINK>
                  <item path="src\other.c" ex="true"/>
                  <item path="main.c" ex="false"><HI-TECH-COMP><property key="optimization-level" value="-O0"/></HI-TECH-COMP></item>
                </conf>
              </confs>
            </configurationDescriptor>
            """);

        var project = MplabProject.Load(file);
        var configuration = Assert.Single(project.Configurations);

        Assert.Equal(folder.Path, project.Directory);
        Assert.Equal("production", configuration.Name);
        Assert.Equal("PIC18F45K22", configuration.Device);
        Assert.Equal("XC8", configuration.Compiler);
        Assert.Equal("4.00", configuration.CompilerVersion);
        Assert.Equal(["main.c"], configuration.Files);
        Assert.Equal(new MplabPackReference("Microchip", "PIC18F-K_DFP", "1.17.312"), Assert.Single(configuration.Packs));
        Assert.Equal("BOARD=1;LED=0", configuration.Options["HI-TECH-COMP"]["define-macros"]);
        Assert.Equal("link-value", configuration.Options["HI-TECH-LINK"]["define-macros"]);
        Assert.Equal("-O0", configuration.FileOptions["main.c"]["HI-TECH-COMP"]["optimization-level"]);
    }

    [Fact]
    public void Refuses_external_entities_before_reading_a_project()
    {
        using var folder = new TempDirectory();
        Assert.Throws<XmlException>(() => MplabProject.Parse("""
            <!DOCTYPE configurationDescriptor [<!ENTITY secret SYSTEM "file:///etc/passwd">]>
            <configurationDescriptor>&secret;</configurationDescriptor>
            """, folder.Path));
    }

    [Fact]
    public void Missing_device_is_an_error_instead_of_choosing_a_different_part()
    {
        using var folder = new TempDirectory();
        Assert.Throws<InvalidDataException>(() => MplabProject.Parse("""
            <configurationDescriptor><confs><conf name="default"><toolsSet><languageToolchain>XC32</languageToolchain></toolsSet></conf></confs></configurationDescriptor>
            """, folder.Path));
    }
}
