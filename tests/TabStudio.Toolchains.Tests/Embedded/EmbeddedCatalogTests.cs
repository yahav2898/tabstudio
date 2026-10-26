using TabStudio.Toolchains.Catalog;
using TabStudio.Toolchains.Embedded;

namespace TabStudio.Toolchains.Tests.Embedded;

public sealed class EmbeddedCatalogTests
{
    [Theory]
    [InlineData("platformio")]
    [InlineData("micropython")]
    public void Python_frontends_pin_every_dependency_and_require_hashes(string id)
    {
        var toolchain = ToolchainCatalog.Default.Find(id)!;
        var install = Assert.Single(toolchain.Setup, step => step.Command.Contains("pip", StringComparer.Ordinal));
        Assert.Contains("--require-hashes", install.Command);
        Assert.Contains("--only-binary=:all:", install.Command);
        var requirements = Assert.Single(toolchain.Setup, step => step.File?.Path.EndsWith(".requirements.txt", StringComparison.Ordinal) == true).File!.Content;
        foreach (var line in requirements.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            Assert.Matches("^[A-Za-z0-9_.-]+==[0-9A-Za-z_.+-]+ --hash=sha256:[0-9a-f]{64}$", line);
        Assert.DoesNotContain("*", requirements, StringComparison.Ordinal);
    }

    [Fact]
    public void Usb_support_builds_the_packaged_bridge_and_keeps_probe_rs_unclaimed()
    {
        var toolchain = ToolchainCatalog.Default.Find("embedded-support")!;
        Assert.Contains("libusb-dev", toolchain.Packages);
        Assert.Contains(toolchain.Setup, step => step.File?.Path == UsbBridgeInstallation.ToolsDirectory + "/serial_posix.c");
        Assert.Contains(toolchain.Setup, step => step.Command.Contains("-fPIC", StringComparer.Ordinal));
        Assert.Contains("nusb", toolchain.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void Embedded_debugging_uses_the_multitarget_gdb_build()
    {
        var debugger = ToolchainCatalog.Default.Find("embedded-debug")!;
        Assert.Contains("gdb-multiarch", debugger.Packages);
        Assert.Contains("gdb-multiarch", debugger.Host!.Commands);
    }
}
