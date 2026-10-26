using System.Text.Json;
using TabStudio.Testing;
using TabStudio.Toolchains.Catalog;
using TabStudio.Toolchains.Downloads;
using TabStudio.Toolchains.Installation;
using TabStudio.Toolchains.Linux;
using TabStudio.Toolchains.Microchip;

namespace TabStudio.Toolchains.Tests.Microchip;

public sealed class MicrochipInstallationTests
{
    [Fact]
    public void Consent_is_required_for_each_exact_compiler_download_before_installing()
    {
        using var folder = new TempDirectory();
        using var http = new HttpClient();
        var environment = new AlpineEnvironment(folder.Path, ToolchainCatalog.Default.Linux,
            new ToolchainPlatform { NativeLibraryDirectory = folder.Path, Architecture = "aarch64" }, new Downloader(http), []);

        Assert.Throws<ToolchainInstallException>(() => MicrochipInstallation.CheckConsent(environment, "xc8"));
        MicrochipInstallation.AcceptLicense(environment, "xc8", "Terms the user read and accepted");
        MicrochipInstallation.CheckConsent(environment, "xc8");
        Assert.Throws<ToolchainInstallException>(() => MicrochipInstallation.CheckConsent(environment, "xc32"));
        Assert.False(Directory.Exists(environment.Rootfs));
    }

    [Fact]
    public void Pack_catalog_pins_bytes_versions_and_actual_devices_for_all_eight_bit_families_and_pic32mx()
    {
        using var document = JsonDocument.Parse(MicrochipInstallation.Resource("packs.lock.json"));
        var devices = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pack in document.RootElement.GetProperty("packs").EnumerateArray())
        {
            Assert.Matches("^[a-f0-9]{64}$", pack.GetProperty("sha256").GetString()!);
            Assert.True(Version.TryParse(pack.GetProperty("version").GetString(), out _));
            Assert.StartsWith("https://packs.download.microchip.com/", pack.GetProperty("url").GetString(), StringComparison.Ordinal);
            foreach (var device in pack.GetProperty("devices").EnumerateArray())
            {
                devices.Add(device.GetString()!);
            }
        }

        Assert.Contains("PIC10F200", devices);
        Assert.Contains("PIC12F675", devices);
        Assert.Contains("PIC16F877A", devices);
        Assert.Contains("PIC18F45K22", devices);
        Assert.Contains("PIC32MX250F128B", devices);
    }

    [Fact]
    public void Store_setup_contains_the_pins_and_runtime_scripts_without_proprietary_binaries()
    {
        foreach (var id in new[] { "xc8", "xc32" })
        {
            var definition = ToolchainCatalog.Default.Find(id)!;
            Assert.NotNull(definition.PinnedVersion);
            Assert.Equal(MicrochipInstallation.SupportFiles.Count, definition.Setup.Count(s => s.File is not null));
            Assert.All(definition.Setup.Where(s => s.File is not null), s => Assert.StartsWith(MicrochipInstallation.ToolsDirectory + "/", s.File!.Path, StringComparison.Ordinal));
            Assert.Contains("--accept-microchip-license", definition.Setup[^1].Command);
            Assert.DoesNotContain(definition.Setup, s => s.Archive is not null);
        }
    }
}
