using System.Net;
using TabStudio.Testing;

namespace TabStudio.Web.Tests.Apps;

/// <summary>
/// A standalone Blazor WebAssembly app end to end against nuget.org: built by TabStudio, served by the WebAssembly
/// app host with the real .NET for WebAssembly, and run by a real browser.
/// </summary>
[Collection(NuGetCollection.Name)]
public sealed class BlazorWebAssemblyTests(NuGetFixture nuget) : IDisposable
{
    private readonly TempDirectory _temp = new();

    [Fact]
    public async Task Serves_the_boot_files_with_their_media_types()
    {
        using var workspace = await WebAppWorkspace.LoadAsync(nuget, WriteApp());
        await using var host = await workspace.StartWebAssemblyAppAsync("Wasm1");

        var page = await nuget.Http.GetStringAsync(host.Address);
        Assert.Contains("<script src=\"_framework/blazor.webassembly.js\"></script>", page, StringComparison.Ordinal);
        Assert.Contains("<link href=\"_framework/dotnet.js\" rel=\"preload\"", page, StringComparison.Ordinal);
        Assert.Equal(page, await nuget.Http.GetStringAsync(new Uri(host.Address, "counter")));

        var loader = await GetAsync(host.Address, "_framework/dotnet.js", "text/javascript");
        var text = await loader.Content.ReadAsStringAsync();
        Assert.Contains("\"mainAssemblyName\": \"Wasm1\"", text, StringComparison.Ordinal);
        Assert.Contains("\"virtualPath\": \"Microsoft.AspNetCore.Components.WebAssembly.dll\"", text, StringComparison.Ordinal);
        Assert.DoesNotContain("dotnetBootConfig", text, StringComparison.Ordinal);

        await GetAsync(host.Address, "_framework/blazor.webassembly.js", "text/javascript");
        await GetAsync(host.Address, "_framework/dotnet.runtime.js", "text/javascript");
        await GetAsync(host.Address, "_framework/dotnet.native.js", "text/javascript");
        await GetAsync(host.Address, "_framework/dotnet.native.wasm", "application/wasm");
        await GetAsync(host.Address, "_framework/icudt_EFIGS.dat", "application/octet-stream");
        await GetAsync(host.Address, "_framework/System.Private.CoreLib.dll", "application/octet-stream");
        await GetAsync(host.Address, "_framework/Wasm1.dll", "application/octet-stream");
        await GetAsync(host.Address, "_framework/Wasm1.pdb", "application/octet-stream");
        await GetAsync(host.Address, "Wasm1.styles.css", "text/css");
    }

    [ChromeFact]
    public async Task Runs_in_a_browser_on_NET_for_WebAssembly()
    {
        using var workspace = await WebAppWorkspace.LoadAsync(nuget, WriteApp());
        await using var host = await workspace.StartWebAssemblyAppAsync("Wasm1");
        await using var chrome = await HeadlessChrome.StartAsync();

        await chrome.NavigateAsync(host.Address);
        await chrome.WaitUntilAsync("document.querySelector('h1')?.textContent === 'Hello from WebAssembly'", TimeSpan.FromSeconds(90));
        await chrome.ClickAsync("button.btn");
        await chrome.ClickAsync("button.btn");

        await chrome.WaitUntilAsync("document.querySelector('button.btn').textContent === 'Count: 2'", TimeSpan.FromSeconds(10));
        Assert.True(host.HasServed("_framework/dotnet.native.wasm"));
        Assert.True(host.HasServed("_framework/Wasm1.dll"));
        Assert.Equal("rgb(128, 0, 128)", (await chrome.EvaluateAsync("getComputedStyle(document.querySelector('h1')).color"))?.GetValue<string>());
    }

    public void Dispose() => _temp.Dispose();

    private async Task<HttpResponseMessage> GetAsync(Uri address, string route, string mediaType)
    {
        var response = await nuget.Http.GetAsync(new Uri(address, route));
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{route}: {response.StatusCode}");
        Assert.Equal(mediaType, response.Content.Headers.ContentType?.MediaType);
        return response;
    }

    /// <summary>A Blazor WebAssembly app with a page, a counter and scoped CSS.</summary>
    private string WriteApp()
    {
        var project = _temp.Write("Wasm1/Wasm1.csproj", """
            <Project Sdk="Microsoft.NET.Sdk.BlazorWebAssembly">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <Nullable>enable</Nullable>
                <ImplicitUsings>enable</ImplicitUsings>
                <OverrideHtmlAssetPlaceholders>true</OverrideHtmlAssetPlaceholders>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Microsoft.AspNetCore.Components.WebAssembly" Version="10.0.0" />
              </ItemGroup>
            </Project>
            """);
        _temp.Write("Wasm1/Program.cs", """
            using Microsoft.AspNetCore.Components.Web;
            using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
            using Wasm1;

            var builder = WebAssemblyHostBuilder.CreateDefault(args);
            builder.RootComponents.Add<App>("#app");
            builder.RootComponents.Add<HeadOutlet>("head::after");

            await builder.Build().RunAsync();
            """);
        _temp.Write("Wasm1/_Imports.razor", """
            @using Microsoft.AspNetCore.Components.Routing
            @using Microsoft.AspNetCore.Components.Web
            @using Wasm1
            """);
        _temp.Write("Wasm1/App.razor", """
            <Router AppAssembly="typeof(App).Assembly">
                <Found Context="routeData">
                    <RouteView RouteData="routeData" />
                </Found>
            </Router>
            """);
        _temp.Write("Wasm1/Pages/Home.razor", """
            @page "/"

            <h1>Hello from WebAssembly</h1>
            <button class="btn" @onclick="() => count++">Count: @count</button>

            @code {
                private int count;
            }
            """);
        _temp.Write("Wasm1/Pages/Home.razor.css", "h1 { color: purple; }\n");
        _temp.Write("Wasm1/wwwroot/index.html", TestData.Read("WebAssembly/index.html").Replace("BlazorApp1", "Wasm1", StringComparison.Ordinal));
        _temp.Write("Wasm1/wwwroot/css/app.css", "body { font-family: sans-serif; }\n");
        return project;
    }
}
