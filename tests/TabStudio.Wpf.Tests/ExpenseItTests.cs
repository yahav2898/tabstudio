using TabStudio.Compilation.Wpf;
using TabStudio.Core.Diagnostics;
using TabStudio.Testing;
using Xunit.Abstractions;

namespace TabStudio.Wpf.Tests;

/// <summary>
/// Microsoft's ExpenseIt walkthrough (TestData/ExpenseIt), in C# and in Visual Basic: a NavigationWindow that shows
/// pages, a page that navigates to another one it makes with <c>new</c>, and live regions raised with
/// <c>AutomationEvents.LiveRegionChanged</c>. OpenSilver has none of these, so the C# app had 6 errors (TS7004 and
/// CS1503) and the Visual Basic one could not be built on OpenSilver at all.
/// </summary>
[Collection(NuGetTestGroup.Name)]
public sealed class ExpenseItTests(NuGetFixture nuget, ITestOutputHelper output) : IDisposable
{
    private readonly TempDirectory _temp = new();

    [Theory]
    [InlineData("csharp", "ExpenseItIntro")]
    [InlineData("vb", "ExpenseItIntro2")]
    public async Task ExpenseIt_builds_on_OpenSilver(string language, string project)
    {
        Copy(language);
        using var workspace = await WpfAppWorkspace.LoadAsync(nuget, _temp.Path);

        var build = await workspace.TryBuildAsync(project);

        foreach (var diagnostic in build.Diagnostics)
        {
            output.WriteLine(diagnostic.ToString());
        }

        Assert.True(build.Success, string.Join('\n', build.Diagnostics.Where(d => d.Severity == DiagnosticSeverityLevel.Error)));
        Assert.DoesNotContain(build.Diagnostics, d => d.Id == WpfDiagnosticIds.UnsupportedApi);
        var extension = language == "vb" ? ".g.vb" : ".g.cs";
        foreach (var xaml in new[] { "MainWindow.xaml", "ExpenseItHome.xaml", "ExpenseReportPage.xaml" })
        {
            Assert.True(File.Exists(Path.Combine(WpfProject.SecondPassDirectory(workspace.Project(project)), xaml + extension)), xaml);
        }
    }

    [ChromeFact]
    public Task The_CSharp_app_shows_its_navigation_window_and_goes_to_the_report_page() => ShowsWindowAsync("csharp", "ExpenseItIntro");

    [ChromeFact]
    public Task The_Visual_Basic_app_shows_its_navigation_window_and_goes_to_the_report_page() => ShowsWindowAsync("vb", "ExpenseItIntro2");

    public void Dispose() => _temp.Dispose();

    /// <summary>The window shows the home page from its Source, and View goes to the report page made with <c>new</c>.</summary>
    private async Task ShowsWindowAsync(string language, string project)
    {
        Copy(language);
        using var workspace = await WpfAppWorkspace.LoadAsync(nuget, _temp.Path);
        await using var host = await workspace.StartAsync(project);
        var messages = new System.Collections.Concurrent.ConcurrentQueue<TabStudio.Web.Serving.PostedMessage>();
        host.MessageReceived += (_, message) => messages.Enqueue(message);
        await using var chrome = await HeadlessChrome.StartAsync(1024, 700);

        await chrome.NavigateAsync(host.Address);
        try
        {
            await chrome.WaitUntilAsync("document.body.innerText.includes('View Expense Report')", TimeSpan.FromSeconds(120));
            Assert.Equal("ExpenseIt", await chrome.TextOfAsync("#tabstudio-wpf-title"));

            await chrome.ClickTextAsync("View");
            await chrome.WaitUntilAsync("document.body.innerText.includes('Expense Report For')", TimeSpan.FromSeconds(20));
        }
        finally
        {
            foreach (var message in messages)
            {
                output.WriteLine($"{message.Route}: {message.Body}");
            }

            var directory = Path.Combine(Environment.GetEnvironmentVariable("GITHUB_WORKSPACE") ?? AppContext.BaseDirectory, "ci-out", "wpf-screenshots");
            Directory.CreateDirectory(directory);
            await File.WriteAllBytesAsync(Path.Combine(directory, $"expenseit-{language}.png"), await chrome.ScreenshotAsync());
        }
    }

    /// <summary>Copies one language's app from TestData into the test's folder.</summary>
    private void Copy(string language)
    {
        var source = Path.Combine(AppContext.BaseDirectory, "TestData", "ExpenseIt", language);
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var target = _temp.Resolve(Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
    }
}
