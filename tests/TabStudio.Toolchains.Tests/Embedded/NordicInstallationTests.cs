using TabStudio.Toolchains.Catalog;
using TabStudio.Toolchains.Embedded.Flashing;
using TabStudio.Toolchains.Embedded.Installation;

namespace TabStudio.Toolchains.Tests.Embedded;

/// <summary>The official legacy bundle remains pinned and auxiliary, with no Python or vendor-source patching.</summary>
public sealed class NordicInstallationTests
{
    [Fact]
    public void Nordic_route_uses_one_complete_pinned_vendor_archive_and_its_original_cli()
    {
        var definition = ToolchainCatalog.Default.Find(EmbeddedNordicInstallation.Id)!;
        Assert.True(definition.Auxiliary);
        Assert.Equal("6.1.7", definition.PinnedVersion);
        Assert.Equal("LicenseRef-Nordic-1-Clause", definition.License);
        Assert.Equal(EmbeddedNordicInstallation.Id, EmbeddedFlashToolInstallation.ToolchainId(EmbeddedFlashProtocol.NrfDfu));
        var archive = Assert.Single(definition.Setup.Select(step => step.Archive).OfType<ArchiveDefinition>());
        Assert.Equal("files.nordicsemi.com", archive.Url.Host);
        Assert.EndsWith("nrfutil-nrf5sdk-tools-x86_64-unknown-linux-gnu-1.1.0.tar.gz", archive.Url.AbsolutePath, StringComparison.Ordinal);
        Assert.Equal(32_471_565, archive.Size);
        Assert.Equal("84b4214c46b917696b171a692a656b0f93e7200dc93dc3ff33fafd6d88b31e4f", archive.Sha256);
        Assert.DoesNotContain(definition.Setup.SelectMany(step => step.Command), argument => argument is "pip" or "--ignore-requires-python");
    }
}
