using System.Net;
using System.Text;
using System.Text.Json;
using TabStudio.Testing;
using TabStudio.Web.StaticWebAssets;
using TabStudio.Web.WebAssembly;

namespace TabStudio.Web.Tests;

/// <summary>
/// The WebAssembly app host over a runtime pack laid out like the real one: the boot configuration in
/// <c>dotnet.js</c>, the start page's placeholders, and serving every file with its media type and no stale caching.
/// </summary>
public sealed class WebAssemblyAppHostTests : IDisposable
{
    private static readonly byte[] AppImage = Encoding.UTF8.GetBytes("app image");
    private static readonly byte[] AppSymbols = Encoding.UTF8.GetBytes("app symbols");

    private readonly TempDirectory _temp = new();
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(30) };

    [Fact]
    public void Writes_the_boot_configuration_into_dotnet_js()
    {
        var bundle = WebAssemblyBundle.Create(App());

        var loader = bundle.Find("_framework/dotnet.js")!;
        var text = Encoding.UTF8.GetString(loader.Content.Span);
        Assert.True(loader.IsInMemory);
        Assert.DoesNotContain("dotnetBootConfig", text, StringComparison.Ordinal);
        Assert.DoesNotContain("bundlerFriendlyImports", text, StringComparison.Ordinal);
        Assert.Contains("dotnet.withConfig(/*json-start*/{", text, StringComparison.Ordinal);

        using var config = BootConfig(text);
        var root = config.RootElement;
        Assert.Equal("App", root.GetProperty("mainAssemblyName").GetString());
        Assert.Equal("Development", root.GetProperty("applicationEnvironment").GetString());
        Assert.Equal(-1, root.GetProperty("debugLevel").GetInt32());
        Assert.Equal("sharded", root.GetProperty("globalizationMode").GetString());
        Assert.True(root.GetProperty("extensions").TryGetProperty("blazor", out _));
        Assert.Equal(new[] { "../appsettings.json" }, root.GetProperty("appsettings").EnumerateArray().Select(e => e.GetString() ?? string.Empty));

        var resources = root.GetProperty("resources");
        Assert.StartsWith("sha256-", resources.GetProperty("hash").GetString(), StringComparison.Ordinal);
        Assert.Equal(new[] { "dotnet.native.js" }, Names(resources, "jsModuleNative"));
        Assert.Equal(new[] { "dotnet.runtime.js" }, Names(resources, "jsModuleRuntime"));
        Assert.Equal(new[] { "dotnet.native.wasm" }, Names(resources, "wasmNative"));
        Assert.Equal(new[] { "icudt_CJK.dat", "icudt_EFIGS.dat", "icudt_no_CJK.dat" }, Names(resources, "icu"));
        Assert.Equal(new[] { "System.Private.CoreLib.dll", "System.Runtime.InteropServices.JavaScript.dll" }, Names(resources, "coreAssembly").Order(StringComparer.Ordinal));
        Assert.Equal(new[] { "App.dll", "Lib.dll", "System.Runtime.dll", "System.Text.Json.dll" }, Names(resources, "assembly"));
        Assert.Equal(new[] { "App.pdb" }, Names(resources, "pdb"));

        var app = resources.GetProperty("assembly").EnumerateArray().First();
        Assert.Equal("App.dll", app.GetProperty("virtualPath").GetString());
        Assert.Equal(bundle.Find("_framework/App.dll")!.Integrity, app.GetProperty("hash").GetString());

        var properties = root.GetProperty("runtimeConfig").GetProperty("runtimeOptions").GetProperty("configProperties");
        Assert.False(properties.GetProperty("System.Globalization.Invariant").GetBoolean());
        Assert.True(properties.GetProperty("System.Resources.UseSystemResourceKeys").GetBoolean());
        Assert.False(properties.GetProperty("Feature.Of.The.App").GetBoolean());
    }

    [Fact]
    public void An_app_without_culture_data_loads_no_ICU()
    {
        var bundle = WebAssemblyBundle.Create(App() with { InvariantGlobalization = true });

        using var config = BootConfig(Encoding.UTF8.GetString(bundle.Find("_framework/dotnet.js")!.Content.Span));
        Assert.Equal("invariant", config.RootElement.GetProperty("globalizationMode").GetString());
        Assert.False(config.RootElement.GetProperty("resources").TryGetProperty("icu", out _));
        Assert.Null(bundle.Find("_framework/icudt_EFIGS.dat"));
    }

    [Fact]
    public void A_dotnet_js_from_before_NET_10_is_refused()
    {
        var error = Assert.Throws<InvalidDataException>(() => WebAssemblyBootConfig.MergeInto("export default {};", "{}"));
        Assert.Contains(".NET 10", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Fills_in_the_start_pages_placeholders()
    {
        var html = HtmlAssetPlaceholders.Apply(TestData.Read("WebAssembly/index.html"), "_framework", "sha256-abc");

        Assert.Contains("<script src=\"_framework/blazor.webassembly.js\"></script>", html, StringComparison.Ordinal);
        Assert.Contains("<script type=\"importmap\">{ \"imports\": {} }</script>", html, StringComparison.Ordinal);
        Assert.Contains("<link href=\"_framework/dotnet.js\" rel=\"preload\" as=\"script\" fetchpriority=\"high\" crossorigin=\"anonymous\" integrity=\"sha256-abc\" />", html, StringComparison.Ordinal);
        Assert.DoesNotContain("fingerprint", html, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"webassembly\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Serves_the_app_with_the_right_media_types_and_no_stale_caching()
    {
        await using var host = await WebAssemblyAppHost.StartAsync(App());

        using var page = await _http.GetAsync(host.Address);
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Equal("text/html", page.Content.Headers.ContentType?.MediaType);
        Assert.Contains("_framework/blazor.webassembly.js", await page.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        await AssertServedAsync(host, "_framework/dotnet.js", "text/javascript");
        await AssertServedAsync(host, "_framework/dotnet.native.wasm", "application/wasm");
        await AssertServedAsync(host, "_framework/icudt_EFIGS.dat", "application/octet-stream");
        await AssertServedAsync(host, "css/app.css", "text/css");
        Assert.Equal(AppImage, await _http.GetByteArrayAsync(new Uri(host.Address, "_framework/App.dll")));
        Assert.Equal(AppSymbols, await _http.GetByteArrayAsync(new Uri(host.Address, "_framework/App.pdb")));
        Assert.True(host.HasServed("_framework/dotnet.native.wasm"));
        Assert.False(host.HasServed("_framework/System.Runtime.dll"));
    }

    [Fact]
    public async Task Answers_the_apps_routes_with_the_start_page_and_missing_files_with_404()
    {
        await using var host = await WebAssemblyAppHost.StartAsync(App());

        var start = await _http.GetStringAsync(host.Address);
        Assert.Equal(start, await _http.GetStringAsync(new Uri(host.Address, "counter")));
        Assert.Equal(start, await _http.GetStringAsync(new Uri(host.Address, "weather/today")));

        using var missing = await _http.GetAsync(new Uri(host.Address, "css/missing.css"));
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);

        using var post = await _http.PostAsync(host.Address, new StringContent("x"));
        Assert.Equal(HttpStatusCode.MethodNotAllowed, post.StatusCode);
    }

    [Fact]
    public async Task Answers_304_when_the_browsers_copy_is_current_and_HEAD_without_a_body()
    {
        await using var host = await WebAssemblyAppHost.StartAsync(App());
        var address = new Uri(host.Address, "_framework/dotnet.native.wasm");

        using var first = await _http.GetAsync(address);
        var etag = first.Headers.ETag!;
        using var conditional = new HttpRequestMessage(HttpMethod.Get, address);
        conditional.Headers.IfNoneMatch.Add(etag);
        using var second = await _http.SendAsync(conditional);
        using var head = await _http.SendAsync(new HttpRequestMessage(HttpMethod.Head, address));

        Assert.Equal(HttpStatusCode.NotModified, second.StatusCode);
        Assert.Equal(HttpStatusCode.OK, head.StatusCode);
        Assert.Equal(4, head.Content.Headers.ContentLength);
        Assert.Empty(await head.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task A_new_build_is_served_at_the_same_address()
    {
        await using var host = await WebAssemblyAppHost.StartAsync(App());
        var address = host.Address;
        var newImage = Encoding.UTF8.GetBytes("new app image");

        await host.UpdateAsync(App() with { Assemblies = [WebAssemblyAssembly.FromImage("App", newImage, null)] });

        Assert.Equal(address, host.Address);
        Assert.Equal(newImage, await _http.GetByteArrayAsync(new Uri(host.Address, "_framework/App.dll")));
        using var symbols = await _http.GetAsync(new Uri(host.Address, "_framework/App.pdb"));
        Assert.Equal(HttpStatusCode.NotFound, symbols.StatusCode);
    }

    [Fact]
    public async Task Prefers_the_port_it_is_given_and_stops_listening_when_disposed()
    {
        var port = Compilation.Running.WebAppHost.FindFreePort();
        var host = await WebAssemblyAppHost.StartAsync(App(), port);
        Assert.Equal(port, host.Address.Port);

        await host.DisposeAsync();

        await Assert.ThrowsAsync<HttpRequestException>(() => _http.GetAsync(host.Address));
    }

    public void Dispose()
    {
        _http.Dispose();
        _temp.Dispose();
    }

    private static string[] Names(JsonElement resources, string property) =>
        [.. resources.GetProperty(property).EnumerateArray().Select(e => e.GetProperty("name").GetString() ?? string.Empty)];

    private static JsonDocument BootConfig(string dotnetJs)
    {
        var start = dotnetJs.IndexOf("/*json-start*/", StringComparison.Ordinal) + "/*json-start*/".Length;
        var end = dotnetJs.IndexOf("/*json-end*/", StringComparison.Ordinal);
        return JsonDocument.Parse(dotnetJs[start..end]);
    }

    private async Task AssertServedAsync(WebAssemblyAppHost host, string route, string mediaType)
    {
        using var response = await _http.GetAsync(new Uri(host.Address, route));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(mediaType, response.Content.Headers.ContentType?.MediaType);
        Assert.True(response.Headers.CacheControl?.NoCache, $"{route} is cached without checking.");
        Assert.NotNull(response.Headers.ETag);
    }

    private WebAssemblyApp App()
    {
        var library = _temp.Write("packages/lib/1.0.0/lib/net10.0/Lib.dll", "library");
        return new WebAssemblyApp
        {
            MainAssemblyName = "App",
            Assemblies = [WebAssemblyAssembly.FromImage("App", AppImage, AppSymbols), WebAssemblyAssembly.FromFile(library)],
            Runtime = FakeBrowserRuntime.Create(_temp),
            Content =
            [
                StaticWebAsset.Create("index.html", _temp.Write("app/wwwroot/index.html", TestData.Read("WebAssembly/index.html"))),
                StaticWebAsset.Create("css/app.css", _temp.Write("app/wwwroot/css/app.css", "h1 { }")),
                StaticWebAsset.Create("appsettings.json", _temp.Write("app/wwwroot/appsettings.json", "{}")),
            ],
            StartPage = "index.html",
            AppSettings = ["appsettings.json"],
            BootExtensions = ["blazor"],
            RuntimeProperties = new Dictionary<string, object> { ["Feature.Of.The.App"] = false },
        };
    }
}
