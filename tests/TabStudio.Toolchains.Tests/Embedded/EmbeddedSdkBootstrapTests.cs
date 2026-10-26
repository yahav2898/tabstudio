using TabStudio.Toolchains.Catalog;
using TabStudio.Toolchains.Embedded.Installation;

namespace TabStudio.Toolchains.Tests.Embedded;

public sealed class EmbeddedSdkBootstrapTests
{
    [Theory]
    [InlineData("platformio", "pio", "--version")]
    [InlineData("arduino", "arduino-cli", "version")]
    [InlineData("probe-rs", "probe-rs", "--version")]
    public void Validation_uses_the_real_frontend_without_opening_a_device(string id, string program, string argument)
    {
        var command = EmbeddedSdkBootstrap.ValidationCommand(id);
        Assert.Equal(program, command.Program);
        Assert.Equal([argument], command.Arguments);
        Assert.Equal("no", command.Environment["PLATFORMIO_SETTING_ENABLE_TELEMETRY"]);
        Assert.Equal("1", command.Environment["PYTHONSAFEPATH"]);
    }

    [Fact]
    public void Pinned_catalog_launcher_disables_telemetry_even_when_called_from_an_arbitrary_project()
    {
        var platformio = ToolchainCatalog.Default.Find("platformio") ?? throw new InvalidOperationException("Missing PlatformIO catalog entry.");
        var launcher = Assert.Single(platformio.Setup.Select(s => s.File).OfType<FileDefinition>(), f => f.Path == "/usr/local/bin/pio");
        Assert.Contains("export PLATFORMIO_SETTING_ENABLE_TELEMETRY=no", launcher.Content, StringComparison.Ordinal);
        Assert.Contains("python -P -m platformio", launcher.Content, StringComparison.Ordinal);
        Assert.Contains("python3-venv", EmbeddedSdkBootstrap.BasePackages);
        Assert.Contains("libusb-1.0-0-dev", EmbeddedSdkBootstrap.BasePackages);
    }
}
