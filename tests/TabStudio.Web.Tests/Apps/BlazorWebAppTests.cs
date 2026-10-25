using System.Net;
using TabStudio.Compilation.Razor;
using TabStudio.Core.Diagnostics;
using TabStudio.Testing;

namespace TabStudio.Web.Tests.Apps;

/// <summary>
/// A Blazor Web App with Interactive Server components, end to end against nuget.org: its Razor components compile
/// with the SDK's Razor compiler, Razor errors land on their line, and the app serves its pages, its scoped CSS and
/// Blazor's script, and counts clicks over a live circuit.
/// </summary>
[Collection(NuGetTestGroup.Name)]
public sealed class BlazorWebAppTests(NuGetFixture nuget) : IDisposable
{
    private readonly TempDirectory _temp = new();

    [Fact]
    public async Task Serves_its_pages_its_scoped_CSS_and_Blazors_script()
    {
        using var workspace = await WebAppWorkspace.LoadAsync(nuget, CountersApp.Write(_temp));
        var project = workspace.Project("Counters");
        var scope = CssScope.Compute(project, _temp.Resolve("Counters/Components/Counter.razor.css"));

        await using var app = await workspace.StartServerAppAsync("Counters");

        var page = await nuget.Http.GetStringAsync(app.Address);
        Assert.Contains($"<button class=\"counter\" {scope}>Clicks: 3</button>", page, StringComparison.Ordinal);
        Assert.Contains("Total: 3", page, StringComparison.Ordinal);
        Assert.Contains("<!--Blazor:", page, StringComparison.Ordinal);
        Assert.Contains("href=\"Counters.styles.css\"", page, StringComparison.Ordinal);
        Assert.Contains("src=\"_framework/blazor.web.js\"", page, StringComparison.Ordinal);

        var styles = await nuget.Http.GetStringAsync(new Uri(app.Address, "Counters.styles.css"));
        Assert.Contains($".counter[{scope}]", styles, StringComparison.Ordinal);

        using var script = await nuget.Http.GetAsync(new Uri(app.Address, "_framework/blazor.web.js"));
        Assert.Equal(HttpStatusCode.OK, script.StatusCode);
        Assert.Equal("text/javascript", script.Content.Headers.ContentType?.MediaType);
        Assert.True((await script.Content.ReadAsByteArrayAsync()).Length > 100_000);
    }

    [ChromeFact]
    public async Task An_Interactive_Server_component_counts_over_its_circuit()
    {
        using var workspace = await WebAppWorkspace.LoadAsync(nuget, CountersApp.Write(_temp));
        await using var app = await workspace.StartServerAppAsync("Counters");
        await using var chrome = await HeadlessChrome.StartAsync();

        await chrome.NavigateAsync(app.Address);

        // The prerendered button does nothing until the circuit connects; then each click counts once.
        var total = await ClickUntilCountedAsync(chrome);
        await chrome.ClickAsync("button.counter");
        await chrome.ClickAsync("button.counter");
        await chrome.WaitUntilAsync($"document.querySelector('p[role=status]').textContent === 'Total: {total + 2}'", TimeSpan.FromSeconds(10));
        Assert.Equal($"Clicks: {total + 2}", await chrome.TextOfAsync("button.counter"));
    }

    [Fact]
    public async Task A_CSharp_error_in_a_component_is_on_its_line_of_the_razor_file()
    {
        using var workspace = await WebAppWorkspace.LoadAsync(nuget, CountersApp.Write(_temp));
        var counter = _temp.Resolve("Counters/Components/Counter.razor");

        workspace.Host.UpdateText(counter, CountersApp.Counter.Replace("Count++;", "Count += \"one\";", StringComparison.Ordinal));
        var live = await workspace.Host.GetDocumentDiagnosticsAsync(counter, CancellationToken.None);
        var project = await workspace.Host.GetProjectDiagnosticsAsync(workspace.Project("Counters").FilePath, CancellationToken.None);

        var error = Assert.Single(live, d => d.Severity == DiagnosticSeverityLevel.Error);
        Assert.Equal("CS0029", error.Id);
        Assert.Equal(counter, error.Location!.FilePath);
        Assert.Equal(12, error.Location.Line);
        Assert.Equal(9, error.Location.Column);
        Assert.Equal("Count += \"one\"", CountersApp.Counter.Replace("Count++;", "Count += \"one\";", StringComparison.Ordinal).ReplaceLineEndings("\n").Substring(error.Location.Start, error.Location.Length));
        Assert.Contains(project, d => d.Id == "CS0029" && d.Location?.FilePath == counter && d.Location.Line == 12);
    }

    [Fact]
    public async Task Razor_errors_and_warnings_are_on_their_line()
    {
        using var workspace = await WebAppWorkspace.LoadAsync(nuget, CountersApp.Write(_temp));
        var home = _temp.Resolve("Counters/Components/Home.razor");

        workspace.Host.UpdateText(home, CountersApp.Home.Replace("<h1>Clicks</h1>", "<h1>Clicks</h1>\n<Countr />", StringComparison.Ordinal));
        var unknown = await workspace.Host.GetDocumentDiagnosticsAsync(home, CancellationToken.None);
        workspace.Host.UpdateText(home, CountersApp.Home.Replace("</p>", string.Empty, StringComparison.Ordinal));
        var unclosed = await workspace.Host.GetDocumentDiagnosticsAsync(home, CancellationToken.None);

        var warning = Assert.Single(unknown, d => d.Id == "RZ10012");
        Assert.Equal(DiagnosticSeverityLevel.Warning, warning.Severity);
        Assert.Equal(home, warning.Location!.FilePath);
        Assert.Equal(5, warning.Location.Line);
        Assert.Contains(unclosed, d => d.Id == "RZ9980" && d.Severity == DiagnosticSeverityLevel.Error && d.Location?.FilePath == home && d.Location.Line == 6);
    }

    public void Dispose() => _temp.Dispose();

    private static async Task<int> ClickUntilCountedAsync(HeadlessChrome chrome)
    {
        var waited = System.Diagnostics.Stopwatch.StartNew();
        while (waited.Elapsed < TimeSpan.FromSeconds(30))
        {
            await chrome.ClickAsync("button.counter");
            await Task.Delay(250);
            var status = await chrome.TextOfAsync("p[role=status]") ?? string.Empty;
            if (int.TryParse(status.Replace("Total: ", string.Empty, StringComparison.Ordinal), out var total) && total > 3)
            {
                return total;
            }
        }

        throw new TimeoutException("The counter never counted: the circuit did not connect.");
    }
}
