using System.Globalization;
using System.Net;
using System.Text.Json;
using TabStudio.Core.Diagnostics;
using TabStudio.Testing;
using TabStudio.Web.Templates;

namespace TabStudio.Web.Tests.Apps;

/// <summary>
/// The Blazor, Razor Pages and MVC templates: the files <c>dotnet new</c> writes, each one built against nuget.org and
/// run, and the Blazor Web App's components interactive in a real browser over Server, WebAssembly and Auto.
/// </summary>
[Collection(NuGetTestGroup.Name)]
public sealed class WebTemplateTests(NuGetFixture nuget) : IDisposable
{
    private readonly TempDirectory _temp = new();

    public static TheoryData<string, string[]> TemplateFiles => new()
    {
        { WebTemplates.BlazorWebAppId, ["Shop/Shop.csproj", "Shop/Components/Pages/Counter.razor", "Shop/Components/Layout/ReconnectModal.razor.js", "Shop/wwwroot/lib/bootstrap/dist/css/bootstrap.min.css"] },
        { WebTemplates.BlazorWebAppIdFor(BlazorInteractivity.WebAssembly), ["Shop/Shop.csproj", "Shop.Client/Shop.Client.csproj", "Shop.Client/Pages/Counter.razor", "Shop/wwwroot/app.css"] },
        { WebTemplates.BlazorWebAppIdFor(BlazorInteractivity.Auto), ["Shop/Shop.csproj", "Shop.Client/Shop.Client.csproj", "Shop.Client/wwwroot/appsettings.json"] },
        { WebTemplates.BlazorWebAppIdFor(BlazorInteractivity.None), ["Shop/Shop.csproj", "Shop/Components/Pages/Weather.razor"] },
        { WebTemplates.BlazorWebAssemblyId, ["Shop/Shop.csproj", "Shop/wwwroot/index.html", "Shop/wwwroot/sample-data/weather.json", "Shop/wwwroot/icon-192.png", "Shop/wwwroot/lib/bootstrap/dist/js/bootstrap.bundle.min.js"] },
        { WebTemplates.RazorPagesId, ["Shop/Shop.csproj", "Shop/Pages/Index.cshtml.cs", "Shop/Pages/Shared/_Layout.cshtml.css", "Shop/wwwroot/lib/jquery/dist/jquery.min.js", "Shop/wwwroot/lib/jquery-validation-unobtrusive/dist/jquery.validate.unobtrusive.min.js"] },
        { WebTemplates.MvcId, ["Shop/Shop.csproj", "Shop/Controllers/HomeController.cs", "Shop/Views/Home/Index.cshtml", "Shop/wwwroot/lib/bootstrap/LICENSE"] },
    };

    [Theory]
    [MemberData(nameof(TemplateFiles))]
    public void Creates_the_files_dotnet_new_creates(string templateId, string[] expected)
    {
        var projects = WebTemplates.Create(templateId, _temp.Path, "Shop");

        Assert.Equal(_temp.Resolve("Shop/Shop.csproj"), projects[0]);
        foreach (var file in expected)
        {
            Assert.True(File.Exists(_temp.Resolve(file)), $"{file} is missing.");
        }

        foreach (var file in Directory.EnumerateFiles(_temp.Path, "*", SearchOption.AllDirectories).Where(f => !f.Contains($"{Path.DirectorySeparatorChar}lib{Path.DirectorySeparatorChar}", StringComparison.Ordinal)))
        {
            var text = File.ReadAllText(file);
            Assert.DoesNotContain("BlazorApp1", text, StringComparison.Ordinal);
            Assert.DoesNotContain("WebApplication1", text, StringComparison.Ordinal);
            Assert.DoesNotContain("$http", text, StringComparison.Ordinal);
            Assert.DoesNotContain("$copyrightYear$", text, StringComparison.Ordinal);
        }

        using var launch = JsonDocument.Parse(File.ReadAllText(_temp.Resolve("Shop/Properties/launchSettings.json")));
        var url = launch.RootElement.GetProperty("profiles").GetProperty("http").GetProperty("applicationUrl").GetString();
        Assert.InRange(new Uri(url!).Port, 5000, 5299);
    }

    public static TheoryData<string> TemplateIds => [.. TemplateFiles.Select(row => (string)row[0])];

    [Theory]
    [MemberData(nameof(TemplateIds))]
    public async Task Each_template_has_no_errors_or_warnings(string templateId)
    {
        using var workspace = await WebAppWorkspace.LoadAsync(nuget, CreateSolution(templateId));

        var problems = new List<string>();
        foreach (var project in workspace.Workspace.Projects)
        {
            var diagnostics = await workspace.Host.GetProjectDiagnosticsAsync(project.FilePath, CancellationToken.None);
            problems.AddRange(diagnostics
                .Where(d => d.Severity >= DiagnosticSeverityLevel.Warning)
                .Select(d => $"{d.Severity} {d.Id} {d.Location?.FilePath}({d.Location?.Line},{d.Location?.Column}): {d.Message}"));
        }

        Assert.True(problems.Count == 0, string.Join('\n', problems));
    }

    [Fact]
    public void Razor_Pages_and_MVC_get_jQuery_and_Blazor_only_Bootstrap()
    {
        WebTemplates.Create(WebTemplates.BlazorWebAppId, _temp.Resolve("blazor"), "Shop");
        WebTemplates.Create(WebTemplates.RazorPagesId, _temp.Resolve("pages"), "Shop");

        Assert.False(Directory.Exists(_temp.Resolve("blazor/Shop/wwwroot/lib/jquery")));
        Assert.False(File.Exists(_temp.Resolve("blazor/Shop/wwwroot/lib/bootstrap/LICENSE")));
        Assert.True(File.Exists(_temp.Resolve("pages/Shop/wwwroot/lib/jquery/dist/jquery.js")));
        Assert.Contains($"&copy; {DateTime.Now.Year} - Shop - ", File.ReadAllText(_temp.Resolve("pages/Shop/Pages/Shared/_Layout.cshtml")), StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_Blazor_Web_App_serves_its_pages_styles_and_scripts()
    {
        using var workspace = await WebAppWorkspace.LoadAsync(nuget, CreateSolution(WebTemplates.BlazorWebAppId));
        await using var app = await workspace.StartServerAppAsync("Shop");

        var home = await nuget.Http.GetStringAsync(app.Address);
        Assert.Contains("<h1>Hello, world!</h1>", home, StringComparison.Ordinal);
        Assert.Contains("<title>Home</title>", home, StringComparison.Ordinal);
        Assert.Contains("Current count: 0", await nuget.Http.GetStringAsync(new Uri(app.Address, "counter")), StringComparison.Ordinal);
        Assert.Contains("<table class=\"table\">", await nuget.Http.GetStringAsync(new Uri(app.Address, "weather")), StringComparison.Ordinal);
        await AssertServedAsync(app.Address, "_framework/blazor.web.js", "text/javascript");
        await AssertServedAsync(app.Address, "lib/bootstrap/dist/css/bootstrap.min.css", "text/css");
        await AssertServedAsync(app.Address, "Shop.styles.css", "text/css");
        await AssertServedAsync(app.Address, "Components/Layout/ReconnectModal.razor.js", "text/javascript");
        using var missing = await nuget.Http.GetAsync(new Uri(app.Address, "no-such-page"));
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [ChromeFact]
    public Task The_Server_Counter_counts() => AssertCounterCountsAsync(WebTemplates.BlazorWebAppId);

    [ChromeFact]
    public Task The_WebAssembly_Counter_counts_in_the_browser() => AssertCounterCountsAsync(WebTemplates.BlazorWebAppIdFor(BlazorInteractivity.WebAssembly));

    [ChromeFact]
    public Task The_Auto_Counter_counts() => AssertCounterCountsAsync(WebTemplates.BlazorWebAppIdFor(BlazorInteractivity.Auto));

    [Fact]
    public async Task The_Blazor_Web_App_without_interactivity_serves_its_pages()
    {
        using var workspace = await WebAppWorkspace.LoadAsync(nuget, CreateSolution(WebTemplates.BlazorWebAppIdFor(BlazorInteractivity.None)));
        await using var app = await workspace.StartServerAppAsync("Shop");

        Assert.Contains("<h1>Hello, world!</h1>", await nuget.Http.GetStringAsync(app.Address), StringComparison.Ordinal);
        Assert.Contains("<table class=\"table\">", await nuget.Http.GetStringAsync(new Uri(app.Address, "weather")), StringComparison.Ordinal);
    }

    [ChromeFact]
    public async Task The_Blazor_WebAssembly_app_runs_in_the_browser()
    {
        using var workspace = await WebAppWorkspace.LoadAsync(nuget, CreateSolution(WebTemplates.BlazorWebAssemblyId));
        await using var host = await workspace.StartWebAssemblyAppAsync("Shop");
        await using var chrome = await HeadlessChrome.StartAsync();

        await chrome.NavigateAsync(new Uri(host.Address, "counter"));
        await chrome.WaitUntilAsync("document.querySelector('p[role=status]')?.textContent === 'Current count: 0'", TimeSpan.FromSeconds(90));
        await chrome.ClickAsync("button.btn-primary");
        await chrome.WaitUntilAsync("document.querySelector('p[role=status]').textContent === 'Current count: 1'", TimeSpan.FromSeconds(10));

        await chrome.NavigateAsync(new Uri(host.Address, "weather"));
        await chrome.WaitUntilAsync("document.querySelectorAll('table.table tbody tr').length === 5", TimeSpan.FromSeconds(90));
        Assert.True(host.HasServed("sample-data/weather.json"));
    }

    [Fact]
    public async Task The_Razor_Pages_app_serves_its_pages_and_libraries()
    {
        using var workspace = await WebAppWorkspace.LoadAsync(nuget, CreateSolution(WebTemplates.RazorPagesId));
        await using var app = await workspace.StartServerAppAsync("Shop");

        var home = await nuget.Http.GetStringAsync(app.Address);
        Assert.Contains("<h1 class=\"display-4\">Welcome</h1>", home, StringComparison.Ordinal);
        Assert.Contains("<title>Home page - Shop</title>", home, StringComparison.Ordinal);
        Assert.Contains("Use this page to detail your site's privacy policy.", await nuget.Http.GetStringAsync(new Uri(app.Address, "Privacy")), StringComparison.Ordinal);
        await AssertServedAsync(app.Address, "lib/jquery/dist/jquery.min.js", "text/javascript");
        await AssertServedAsync(app.Address, "css/site.css", "text/css");
        await AssertServedAsync(app.Address, "Shop.styles.css", "text/css");
    }

    [Fact]
    public async Task The_MVC_app_serves_its_views()
    {
        using var workspace = await WebAppWorkspace.LoadAsync(nuget, CreateSolution(WebTemplates.MvcId));
        await using var app = await workspace.StartServerAppAsync("Shop");

        Assert.Contains("<h1 class=\"display-4\">Welcome</h1>", await nuget.Http.GetStringAsync(app.Address), StringComparison.Ordinal);
        Assert.Contains("Use this page to detail your site's privacy policy.", await nuget.Http.GetStringAsync(new Uri(app.Address, "Home/Privacy")), StringComparison.Ordinal);
        await AssertServedAsync(app.Address, "lib/bootstrap/dist/js/bootstrap.bundle.min.js", "text/javascript");
    }

    public void Dispose() => _temp.Dispose();

    /// <summary>Opens the Counter page of a Blazor Web App in Chrome and clicks until the component, once interactive, counts.</summary>
    private async Task AssertCounterCountsAsync(string templateId)
    {
        using var workspace = await WebAppWorkspace.LoadAsync(nuget, CreateSolution(templateId));
        await using var app = await workspace.StartServerAppAsync("Shop");
        await using var chrome = await HeadlessChrome.StartAsync();

        await chrome.NavigateAsync(new Uri(app.Address, "counter"));
        var waited = System.Diagnostics.Stopwatch.StartNew();
        string? status = null;
        while (waited.Elapsed < TimeSpan.FromSeconds(90))
        {
            await chrome.ClickAsync("button.btn-primary");
            await Task.Delay(250);
            status = await chrome.TextOfAsync("p[role=status]");
            if (status != "Current count: 0")
            {
                break;
            }
        }

        Assert.True(
            status != "Current count: 0",
            $"The counter never counted.\nThe console:\n{string.Join('\n', chrome.ConsoleMessages)}\nThe app:\n{app.Console.Output}\n{app.Console.Error}");
        var counted = int.Parse(status!["Current count: ".Length..], CultureInfo.InvariantCulture);
        await chrome.ClickAsync("button.btn-primary");
        await chrome.WaitUntilAsync($"document.querySelector('p[role=status]').textContent === 'Current count: {counted + 1}'", TimeSpan.FromSeconds(10));
    }

    private async Task AssertServedAsync(Uri address, string route, string mediaType)
    {
        using var response = await nuget.Http.GetAsync(new Uri(address, route));
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{route}: {response.StatusCode}");
        Assert.Equal(mediaType, response.Content.Headers.ContentType?.MediaType);
    }

    /// <summary>Creates a template's projects with a solution, as the start screen does.</summary>
    private string CreateSolution(string templateId)
    {
        var projects = WebTemplates.Create(templateId, _temp.Path, "Shop");
        var solution = _temp.Write("Shop.slnx", "<Solution>\n" + string.Concat(projects.Select(p => $"  <Project Path=\"{Path.GetRelativePath(_temp.Path, p).Replace('\\', '/')}\" />\n")) + "</Solution>\n");
        return solution;
    }
}
