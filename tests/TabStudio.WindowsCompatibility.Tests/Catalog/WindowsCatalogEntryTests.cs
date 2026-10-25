using System.Text.RegularExpressions;
using TabStudio.Toolchains.Catalog;
using TabStudio.WindowsCompatibility.Sessions;

namespace TabStudio.WindowsCompatibility.Tests.Catalog;

/// <summary>Windows Compatibility's entry in the Toolchain Store's catalog.</summary>
public sealed partial class WindowsCatalogEntryTests
{
    private static readonly ToolchainDefinition Windows = ToolchainCatalog.Default.Toolchains.Single(t => t.Id == "windows");

    [Fact]
    public void Is_a_listed_platform_for_csharp_apps()
    {
        Assert.Equal(ToolchainKind.Platform, Windows.Kind);
        Assert.True(Windows.Listed);
        Assert.Equal("Windows Compatibility", Windows.Name);
        Assert.Contains("C#", Windows.Languages);
        Assert.EndsWith(".", Windows.Description, StringComparison.Ordinal);
        Assert.Contains("xvfb", Windows.Packages);
        Assert.NotNull(Windows.Host);
        Assert.Contains("wine", Windows.Host.Commands);
        Assert.Matches(Windows.Host.VersionPattern!, "wine-11.0");
        Assert.True(Windows.EstimatedDownloadSize > 0 && Windows.EstimatedInstalledSize > Windows.EstimatedDownloadSize);
    }

    [Fact]
    public void Is_only_for_the_processors_it_has_wine_and_dotnet_for()
    {
        Assert.True(Windows.SupportsArchitecture("x86_64"));
        Assert.False(Windows.SupportsArchitecture("armv7"));
        Assert.False(Windows.SupportsArchitecture("x86"));
        foreach (var architecture in Windows.Architectures.Keys)
        {
            var specialized = Windows.ForArchitecture(architecture);
            var wine = specialized.Packages.Contains("wine") || specialized.Setup.Any(s => s.Archive?.Destination == "/opt/tabstudio/wine");
            Assert.True(wine, architecture);
            Assert.Contains("xvfb", specialized.Packages);
        }
    }

    [Fact]
    public void Installs_microsofts_dotnet_runtimes_where_the_session_looks_for_them()
    {
        var layoutRoots = new[] { WindowsLayout.LinuxRoot + "/dotnet", WindowsLayout.LinuxRoot + "/desktop" };
        var version = DotNetVersion().Match(Windows.DisplayVersion!).Groups["version"].Value;
        Assert.NotEmpty(version);
        foreach (var (architecture, definition) in Windows.Architectures)
        {
            var archives = definition.Setup.Select(s => s.Archive).OfType<ArchiveDefinition>().Where(a => a.Destination.StartsWith(WindowsLayout.LinuxRoot + "/", StringComparison.Ordinal)).ToList();
            Assert.Equal(layoutRoots, archives.Select(a => a.Destination));
            Assert.All(archives, a =>
            {
                Assert.Equal("builds.dotnet.microsoft.com", a.Url.Host);
                Assert.Contains($"/{version}/", a.Url.AbsolutePath, StringComparison.Ordinal);
                Assert.Contains(architecture == "x86_64" ? "-win-x64.zip" : "-win-arm64.zip", a.Url.AbsolutePath, StringComparison.Ordinal);
                Assert.Matches("^[0-9a-f]{64}$", a.Sha256);
                Assert.True(a.Size > 0);
            });
        }
    }

    [GeneratedRegex(@"\.NET (?<version>\d+\.\d+\.\d+)")]
    private static partial Regex DotNetVersion();
}
