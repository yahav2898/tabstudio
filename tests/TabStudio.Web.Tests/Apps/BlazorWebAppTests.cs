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
    private const string Counter = """
        <button class="counter" @onclick="IncrementAsync">@Label: @Count</button>

        @code {
            [Parameter] public string Label { get; set; } = "Count";

            [Parameter] public int Count { get; set; }

            [Parameter] public EventCallback<int> CountChanged { get; set; }

            private async Task IncrementAsync()
            {
                Count++;
                await CountChanged.InvokeAsync(Count);
            }
        }
        """;

    private const string Home = """
        @page "/"
        @rendermode InteractiveServer

        <h1>Clicks</h1>
        <Counter Label="Clicks" @bind-Count="clicks" />
        <p role="status">Total: @clicks</p>

        @code {
            private int clicks = 3;
        }
        """;

    private readonly TempDirectory _temp = new();

    [Fact]
    public async Task Serves_its_pages_its_scoped_CSS_and_Blazors_script()
    {
        using var workspace = await WebAppWorkspace.LoadAsync(nuget, WriteApp());
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
        using var workspace = await WebAppWorkspace.LoadAsync(nuget, WriteApp());
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
        using var workspace = await WebAppWorkspace.LoadAsync(nuget, WriteApp());
        var counter = _temp.Resolve("Counters/Components/Counter.razor");

        workspace.Host.UpdateText(counter, Counter.Replace("Count++;", "Count += \"one\";", StringComparison.Ordinal));
        var live = await workspace.Host.GetDocumentDiagnosticsAsync(counter, CancellationToken.None);
        var project = await workspace.Host.GetProjectDiagnosticsAsync(workspace.Project("Counters").FilePath, CancellationToken.None);

        var error = Assert.Single(live, d => d.Severity == DiagnosticSeverityLevel.Error);
        Assert.Equal("CS0029", error.Id);
        Assert.Equal(counter, error.Location!.FilePath);
        Assert.Equal(12, error.Location.Line);
        Assert.Equal(9, error.Location.Column);
        Assert.Equal("Count += \"one\"", Counter.Replace("Count++;", "Count += \"one\";", StringComparison.Ordinal).ReplaceLineEndings("\n").Substring(error.Location.Start, error.Location.Length));
        Assert.Contains(project, d => d.Id == "CS0029" && d.Location?.FilePath == counter && d.Location.Line == 12);
    }

    [Fact]
    public async Task Razor_errors_and_warnings_are_on_their_line()
    {
        using var workspace = await WebAppWorkspace.LoadAsync(nuget, WriteApp());
        var home = _temp.Resolve("Counters/Components/Home.razor");

        workspace.Host.UpdateText(home, Home.Replace("<h1>Clicks</h1>", "<h1>Clicks</h1>\n<Countr />", StringComparison.Ordinal));
        var unknown = await workspace.Host.GetDocumentDiagnosticsAsync(home, CancellationToken.None);
        workspace.Host.UpdateText(home, Home.Replace("</p>", string.Empty, StringComparison.Ordinal));
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

    /// <summary>A Blazor Web App with an Interactive Server page and a component with parameters and an EventCallback.</summary>
    private string WriteApp()
    {
        var project = _temp.Write("Counters/Counters.csproj", """
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <Nullable>enable</Nullable>
                <ImplicitUsings>enable</ImplicitUsings>
                <BlazorDisableThrowNavigationException>true</BlazorDisableThrowNavigationException>
              </PropertyGroup>
            </Project>
            """);
        _temp.Write("Counters/Program.cs", """
            using Counters.Components;

            var builder = WebApplication.CreateBuilder(args);
            builder.Services.AddRazorComponents().AddInteractiveServerComponents();

            var app = builder.Build();
            app.UseAntiforgery();
            app.MapStaticAssets();
            app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
            app.Run();
            """);
        _temp.Write("Counters/Components/_Imports.razor", """
            @using Microsoft.AspNetCore.Components.Routing
            @using Microsoft.AspNetCore.Components.Web
            @using static Microsoft.AspNetCore.Components.Web.RenderMode
            @using Counters.Components
            """);
        _temp.Write("Counters/Components/App.razor", """
            <!DOCTYPE html>
            <html lang="en">
            <head>
                <meta charset="utf-8" />
                <base href="/" />
                <link rel="stylesheet" href="@Assets["Counters.styles.css"]" />
                <ImportMap />
                <HeadOutlet />
            </head>
            <body>
                <Routes />
                <script src="@Assets["_framework/blazor.web.js"]"></script>
            </body>
            </html>
            """);
        _temp.Write("Counters/Components/Routes.razor", """
            <Router AppAssembly="typeof(Program).Assembly">
                <Found Context="routeData">
                    <RouteView RouteData="routeData" />
                </Found>
            </Router>
            """);
        _temp.Write("Counters/Components/Counter.razor", Counter);
        _temp.Write("Counters/Components/Counter.razor.css", ".counter { font-weight: bold; }\n");
        _temp.Write("Counters/Components/Home.razor", Home);
        return project;
    }
}
