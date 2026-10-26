using TabStudio.Testing;
using TabStudio.Toolchains.Catalog;
using TabStudio.Toolchains.Projects;

namespace TabStudio.Toolchains.Tests.Microchip;

public sealed class PicProjectDetectionTests
{
    [Theory]
    [InlineData("XC8", "PIC18F45K22", "xc8")]
    [InlineData("XC32", "PIC32MX250F128B", "xc32")]
    public void Nested_mplab_marker_selects_the_configuration_compiler_once(string compiler, string device, string id)
    {
        using var folder = new TempDirectory();
        folder.Write("Blink.X/nbproject/configurations.xml", $"""
            <configurationDescriptor><logicalFolder><itemPath>main.c</itemPath></logicalFolder>
            <confs><conf name="default"><toolsSet><targetDevice>{device}</targetDevice>
            <languageToolchain>{compiler}</languageToolchain><languageToolchainVersion>6.00</languageToolchainVersion>
            </toolsSet></conf></confs></configurationDescriptor>
            """);
        var catalog = ToolchainCatalog.Default with
        {
            Toolchains = [.. ToolchainCatalog.Default.Toolchains.Where(t => t.Id is "xc8" or "xc32").Select(t => t with { Listed = true })],
        };

        var project = Assert.Single(ToolchainProjectDetector.Detect(folder.Path, catalog));

        Assert.Equal(id, project.Toolchain.Id);
        Assert.Equal("nbproject/configurations.xml", project.Marker);
        Assert.Equal("Blink.X", project.Name);
    }
}
