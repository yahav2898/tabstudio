using TabStudio.Compilation.Wpf;
using TabStudio.Core.Diagnostics;
using TabStudio.Testing;

namespace TabStudio.Wpf.Tests;

/// <summary>
/// A small business app written for WPF (TestData/Contacts): MVVM with bindings and commands, styles and resources,
/// a user control, a converter, a DataGrid of a list and a message box, built on OpenSilver and run in a browser.
/// </summary>
[Collection(NuGetTestGroup.Name)]
public sealed class BusinessAppTests(NuGetFixture nuget) : IDisposable
{
    private readonly TempDirectory _temp = new();

    [Fact]
    public async Task The_business_app_builds_on_OpenSilver()
    {
        Copy("Contacts");
        using var workspace = await WpfAppWorkspace.LoadAsync(nuget, _temp.Path);

        var build = await workspace.BuildAsync("Contacts");

        Assert.DoesNotContain(build.Diagnostics, d => d.Severity >= DiagnosticSeverityLevel.Warning);
        var note = Assert.Single(build.Diagnostics, d => d.Id == WpfDiagnosticIds.WindowSettingIgnored);
        Assert.Equal(_temp.Resolve("Contacts/MainWindow.xaml"), note.Location?.FilePath);
        foreach (var xaml in new[] { "App.xaml", "MainWindow.xaml", "ContactCard.xaml" })
        {
            Assert.True(File.Exists(Path.Combine(WpfProject.SecondPassDirectory(workspace.Project("Contacts")), xaml + ".g.cs")), xaml);
        }
    }

    [ChromeFact]
    public async Task Lists_the_contacts_in_a_data_grid_and_adds_one_with_a_command()
    {
        Copy("Contacts");
        using var workspace = await WpfAppWorkspace.LoadAsync(nuget, _temp.Path);
        await using var host = await workspace.StartAsync("Contacts");
        await using var chrome = await HeadlessChrome.StartAsync(1024, 700);

        await chrome.NavigateAsync(host.Address);
        await chrome.WaitUntilAsync("document.body.innerText.includes('Grace Hopper') && document.body.innerText.includes('3 contacts')", TimeSpan.FromSeconds(120));
        Assert.Equal("Contacts", await chrome.TextOfAsync("#tabstudio-wpf-title"));

        await chrome.ClickTextAsync("Add");
        await chrome.WaitUntilAsync("document.body.innerText.includes('4 contacts') && document.body.innerText.includes('new@example.com')", TimeSpan.FromSeconds(10));

        var directory = Path.Combine(Environment.GetEnvironmentVariable("GITHUB_WORKSPACE") ?? AppContext.BaseDirectory, "ci-out", "wpf-screenshots");
        Directory.CreateDirectory(directory);
        await File.WriteAllBytesAsync(Path.Combine(directory, "contacts-in-browser.png"), await chrome.ScreenshotAsync());
    }

    public void Dispose() => _temp.Dispose();

    /// <summary>Copies a sample from TestData into the test's folder.</summary>
    private void Copy(string sample)
    {
        var source = Path.Combine(AppContext.BaseDirectory, "TestData", sample);
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            _temp.Write($"{sample}/{Path.GetRelativePath(source, file).Replace('\\', '/')}", File.ReadAllText(file));
        }
    }
}
