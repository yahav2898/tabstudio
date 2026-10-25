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
        Assert.True(catalog.IsMissingNamespace("System.Windows.Xps"));
        Assert.Equal(["System.Windows.Controls.GroupBox"], catalog.MissingTypesNamed("GroupBox"));
        Assert.Equal("System.Windows.Controls.Button", catalog.FindMissingMember("System.Windows.Controls.Button", "IsDefault"));
        Assert.Equal("System.Windows.UIElement", catalog.FindMissingMember("System.Windows.Controls.Button", "BitmapEffect"));
        Assert.Null(catalog.FindMissingMember("System.Windows.Controls.Button", "Content"));

        // The catalog TabStudio ships is this one: a new OpenSilver or WPF needs it made again.
        using var stream = typeof(WpfApiCatalog).Assembly.GetManifestResourceStream(WpfApiCatalog.ResourceName)!;
        using var reader = new StreamReader(stream);
        var shipped = (await reader.ReadToEndAsync()).ReplaceLineEndings("\n");
        var difference = shipped.Split('\n').Except(computed.Split('\n'), StringComparer.Ordinal)
            .Concat(computed.Split('\n').Except(shipped.Split('\n'), StringComparer.Ordinal)).Take(10).ToList();
        Assert.True(
            shipped == computed,
            $"src/TabStudio.Compilation/Wpf/{WpfApiCatalog.FileName} is not what WPF and OpenSilver have now: copy ci-out/wpf-api/{WpfApiCatalog.FileName} over it. Lines that differ:\n{string.Join('\n', difference)}");
        Assert.False(WpfApiCatalog.Shipped.IsEmpty);
    }

    public void Dispose() => _temp.Dispose();
}
