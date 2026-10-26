using TabStudio.Testing;
using TabStudio.Toolchains.Microchip;

namespace TabStudio.Toolchains.Tests.Microchip;

public sealed class PicFlashProfileTests
{
    [Fact]
    public void Board_settings_cannot_silently_change_the_part_or_open_an_unrelated_hex_file()
    {
        using var folder = new TempDirectory();
        var project = MplabProject.Parse("<configurationDescriptor><confs><conf name=\"default\"><toolsSet><targetDevice>PIC18F45K22</targetDevice><languageToolchain>XC8</languageToolchain></toolsSet></conf></confs></configurationDescriptor>", folder.Path);
        foreach (var json in new[]
        {
            """{"protocol":"mcc-uart","device":"PIC16F877A","firmware":"dist/blink.hex"}""",
            """{"protocol":"mcc-uart","device":"PIC18F45K22","firmware":"../other.hex"}""",
            """{"protocol":"pymcuprog","device":"PIC18F45K22","firmware":"dist/blink.hex","tool":"pickit5"}""",
            """{"protocol":"pic32prog","device":"PIC18F45K22","firmware":"dist/blink.hex"}""",
        })
        {
            folder.Write(PicFlashProfile.FileName, json);
            Assert.Throws<InvalidDataException>(() => PicFlashProfile.Load(project));
        }
        folder.Write(PicFlashProfile.FileName, """{"protocol":"mcc-uart","device":"PIC18F45K22","firmware":"dist/blink.hex"}""");
        Assert.True(PicFlashProfile.Load(project).IsLocal);
    }

    [Fact]
    public void Explicit_configuration_selection_keeps_the_selected_device_and_options()
    {
        using var folder = new TempDirectory();
        var project = MplabProject.Parse("<configurationDescriptor><confs><conf name=\"default\"><toolsSet><targetDevice>PIC18F45K22</targetDevice><languageToolchain>XC8</languageToolchain></toolsSet></conf><conf name=\"sim\"><toolsSet><targetDevice>PIC18F14K22</targetDevice><languageToolchain>XC8</languageToolchain></toolsSet></conf></confs></configurationDescriptor>", folder.Path);
        Assert.Equal("PIC18F45K22", project.SelectedConfiguration().Device);
        folder.Write(".tabstudio/pic-build.json", """{"configuration":"sim"}""");
        Assert.Equal("PIC18F14K22", project.SelectedConfiguration().Device);
        folder.Write(".tabstudio/pic-build.json", """{"configuration":"missing"}""");
        Assert.Throws<InvalidDataException>(() => project.SelectedConfiguration());
    }
}
