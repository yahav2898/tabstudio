using TabStudio.Compilation.Wpf;
using TabStudio.Testing;
using TabStudio.Wpf.Templates;

namespace TabStudio.Wpf.Tests;

/// <summary>
/// The catalog of what WPF has and OpenSilver does not, which names unsupported API in the Error List, made again
/// from WPF's reference assemblies and OpenSilver on nuget.org and compared with the one TabStudio ships.
/// </summary>
[Collection(NuGetTestGroup.Name)]
public sealed class WpfApiCatalogTests(NuGetFixture nuget) : IDisposable
{
    private readonly TempDirectory _temp = new();

    [Fact]
    public async Task The_shipped_catalog_is_what_WPF_has_and_OpenSilver_does_not()
    {
        WpfTemplates.Create(_temp.Path, "WpfApp1");
        using var workspace = await WpfAppWorkspace.LoadAsync(nuget, _temp.Path);
        var xaml = await workspace.Host.GetWpfXamlFileAsync(_temp.Resolve("WpfApp1/MainWindow.xaml"));
        var wpf = await WpfApiCatalogBuilder.DownloadWpfAsync(nuget.Http, _temp.Resolve("wpf"));

        var computed = WpfApiCatalogBuilder.Build(wpf, xaml!.Compilation, OpenSilverPackages.Version);

        var directory = Path.Combine(Environment.GetEnvironmentVariable("GITHUB_WORKSPACE") ?? AppContext.BaseDirectory, "ci-out", "wpf-api");
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, WpfApiCatalog.FileName), computed);
        var catalog = WpfApiCatalog.Parse(computed);
        Assert.True(catalog.IsMissingType("System.Windows.Controls.DocumentViewer"));
        Assert.False(catalog.IsMissingType("System.Windows.Controls.Button"));
        Assert.True(catalog.IsMissingNamespace("System.Windows.Media.Media3D"));
    }

    public void Dispose() => _temp.Dispose();
}
