using TabStudio.Toolchains.Catalog;
using TabStudio.Toolchains.Embedded.Flashing;
using TabStudio.Toolchains.Embedded.Installation;
using TabStudio.Toolchains.Runtimes;

namespace TabStudio.Toolchains.Tests.Embedded;

public sealed class FlashToolInstallationTests
{
    [Theory]
    [InlineData("embedded-esptool", EmbeddedFlashProtocol.Esptool)]
    [InlineData("embedded-picotool", EmbeddedFlashProtocol.Picotool)]
    [InlineData("embedded-bossac", EmbeddedFlashProtocol.Bossac)]
    [InlineData("embedded-teensy", EmbeddedFlashProtocol.Teensy)]
    [InlineData("embedded-wchisp", EmbeddedFlashProtocol.WchIsp)]
    [InlineData("embedded-mspdebug", EmbeddedFlashProtocol.MspDebug)]
    public void Every_installable_flash_route_has_pins_validation_and_path_aliases(string id, EmbeddedFlashProtocol protocol)
    {
        var definition = ToolchainCatalog.Default.Find(id)!;
        Assert.True(definition.Listed);
        Assert.True(definition.Auxiliary);
        Assert.Empty(definition.FileExtensions);
        Assert.Empty(definition.ProjectFiles);
        Assert.NotNull(definition.PinnedVersion);
        Assert.Equal(id, EmbeddedFlashToolInstallation.ToolchainId(protocol));
        Assert.All(definition.Setup.Where(step => step.Archive is not null), step =>
        {
            Assert.Matches("^[a-f0-9]{64}$", step.Archive!.Sha256);
            Assert.True(step.Archive.Size > 0);
            Assert.Equal("https", step.Archive.Url.Scheme);
            Assert.Equal("github.com", step.Archive.Url.Host);
        });
        Assert.Contains(definition.Setup, step => step.File is { } file && file.Path.EndsWith("tabstudio-install.py", StringComparison.Ordinal));
        var validate = Assert.Single(definition.Setup, step => step.Command.Count > 0);
        Assert.Equal("python3", validate.Command[0]);
        Assert.Contains(definition.PinnedVersion!, validate.Command);
        Assert.Contains(validate.Removes, path => path.StartsWith("/usr/local/bin/", StringComparison.Ordinal));
        Assert.True(validate.RunAfterSetupChanges);
    }

    [Fact]
    public void Binary_clients_cover_both_glibc_native_architectures_and_route_their_aliases()
    {
        foreach (var id in new[] { "embedded-esptool", "embedded-picotool" })
        {
            var definition = ToolchainCatalog.Default.Find(id)!;
            Assert.Single(definition.Setup, step => step.Archive is not null && step.Architectures.Contains("aarch64"));
            Assert.Single(definition.Setup, step => step.Archive is not null && step.Architectures.Contains("x86_64"));
        }
        Assert.Equal("embedded-esptool", EmbeddedGlibcRuntime.ToolchainForCommand("esptool.py"));
        Assert.Equal("embedded-esptool", EmbeddedGlibcRuntime.ToolchainForCommand("esptool"));
        Assert.Equal("embedded-picotool", EmbeddedGlibcRuntime.ToolchainForCommand("picotool"));
    }

    [Fact]
    public void Source_recipes_preserve_locked_dependencies_and_signed_legacy_libusb()
    {
        Assert.Contains("--locked", EmbeddedFlashToolInstallation.SourceInstallerSource, StringComparison.Ordinal);
        Assert.Contains("--no-default-features", EmbeddedFlashToolInstallation.SourceInstallerSource, StringComparison.Ordinal);
        Assert.Contains("VERSION=1.9.1", EmbeddedFlashToolInstallation.SourceInstallerSource, StringComparison.Ordinal);
        Assert.Contains("libusb-compat-dev", ToolchainCatalog.Default.Find("embedded-teensy")!.Packages);
        Assert.Contains("libusb-compat-dev", ToolchainCatalog.Default.Find("embedded-mspdebug")!.Packages);
        Assert.Contains("rust", ToolchainCatalog.Default.Find("embedded-wchisp")!.Packages);
    }
}
