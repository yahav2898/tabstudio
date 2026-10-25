using System.Text.Json;
using TabStudio.Core.Workspaces;
using TabStudio.Testing;
using TabStudio.Web.StaticWebAssets;

namespace TabStudio.Web.Tests;

/// <summary>Collecting a project's static web assets, bundling its scoped CSS and writing the manifests ASP.NET Core reads.</summary>
public sealed class StaticWebAssetsTests : IDisposable
{
    private const string WebProject = """
        <Project Sdk="Microsoft.NET.Sdk.Web">
          <PropertyGroup>
            <TargetFramework>net10.0</TargetFramework>
          </PropertyGroup>
        </Project>
        """;

    private readonly TempDirectory _temp = new();

    [Fact]
    public void Bundles_the_scoped_CSS_of_the_Blazor_template_as_the_SDK_does()
    {
        var project = BlazorTemplateLayout();

        var files = ScopedCssBundle.FindFiles(project);
        var bundle = ScopedCssBundle.Create(project, files, string.Empty, []);

        Assert.Equal(3, files.Count);
        Assert.Empty(bundle.Errors);
        Assert.Equal(TestData.Read("ScopedCss/BlazorApp1.styles.expected.css"), bundle.Css);
        Assert.Equal("BlazorApp1.styles.css", ScopedCssBundle.AppBundleName(project));
    }

    [Fact]
    public void An_apps_bundle_imports_its_libraries_bundles_first()
    {
        var project = BlazorTemplateLayout();

        var bundle = ScopedCssBundle.Create(project, ScopedCssBundle.FindFiles(project), string.Empty, ["_content/Lib/Lib.bundle.scp.css"]);

        Assert.StartsWith("@import '_content/Lib/Lib.bundle.scp.css';\n\n/* /Components/Layout/MainLayout.razor.rz.scp.css */\n", bundle.Css, StringComparison.Ordinal);
    }

    [Fact]
    public void A_scoped_CSS_file_with_an_import_is_reported_and_left_out()
    {
        var project = Project("App", WebProject);
        _temp.Write("App/Pages/Index.razor", "<h1>Hi</h1>");
        var css = _temp.Write("App/Pages/Index.razor.css", "@import 'site.css';\nh1 { color: red; }");

        var bundle = ScopedCssBundle.Create(project, ScopedCssBundle.FindFiles(project), string.Empty, []);

        var error = Assert.Single(bundle.Errors);
        Assert.Equal(css, error.Location!.FilePath);
        Assert.Equal(1, error.Location.Line);
        Assert.False(bundle.HasContent);
    }

    [Fact]
    public void Collects_the_web_root_collocated_modules_and_the_bundle()
    {
        var project = BlazorTemplateLayout();
        _temp.Write("BlazorApp1/wwwroot/app.css", "body { }");
        _temp.Write("BlazorApp1/wwwroot/lib/site.js", "");
        _temp.Write("BlazorApp1/wwwroot/.hidden/secret.txt", "");
        _temp.Write("BlazorApp1/Components/Layout/ReconnectModal.razor.js", "export function f() { }");
        var output = _temp.Resolve("out");

        var result = ProjectWebAssets.Collect(project, string.Empty, isApp: true, [], output);

        Assert.Equal("BlazorApp1.styles.css", result.ScopedCssBundleRoute);
        Assert.Equal(
            new[] { "app.css", "lib/site.js", "Components/Layout/ReconnectModal.razor.js", "BlazorApp1.styles.css" },
            result.Assets.Select(a => a.Route));
        Assert.True(File.Exists(Path.Combine(output, "BlazorApp1.styles.css")));
    }

    [Fact]
    public void A_librarys_assets_are_served_under_its_content_path()
    {
        var library = Project("Shared", """
            <Project Sdk="Microsoft.NET.Sdk.Razor">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);
        _temp.Write("Shared/wwwroot/background.png", "png");
        _temp.Write("Shared/Component1.razor", "<div class=\"my-component\">Hi</div>");
        _temp.Write("Shared/Component1.razor.css", ".my-component { border: 2px dashed red; }");

        var basePath = ProjectWebAssets.LibraryBasePath(library);
        var result = ProjectWebAssets.Collect(library, basePath, isApp: false, [], _temp.Resolve("out"));

        Assert.Equal("_content/Shared", basePath);
        Assert.Equal("_content/Shared/Shared.bundle.scp.css", result.ScopedCssBundleRoute);
        Assert.Contains(result.Assets, a => a.Route == "_content/Shared/background.png");
        var bundle = File.ReadAllText(result.Assets.Single(a => a.Route == result.ScopedCssBundleRoute).FilePath);
        Assert.StartsWith("/* _content/Shared/Component1.razor.rz.scp.css */\n", bundle, StringComparison.Ordinal);
    }

    [Fact]
    public void The_client_of_a_Blazor_Web_App_is_served_from_the_root()
    {
        var client = Project("App.Client", """
            <Project Sdk="Microsoft.NET.Sdk.BlazorWebAssembly">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);

        Assert.Equal(string.Empty, ProjectWebAssets.LibraryBasePath(client));
    }

    [Fact]
    public void Writes_a_runtime_manifest_with_a_tree_of_routes_and_the_web_root_pattern()
    {
        var webRoot = _temp.Resolve("App/wwwroot");
        var css = _temp.Write("App/wwwroot/css/app.css", "body { }");
        var script = _temp.Write("packages/blazor/_framework/blazor.web.js", "// Blazor");
        var manifest = _temp.Resolve("obj/App.staticwebassets.runtime.json");

        StaticAssetManifests.WriteRuntimeManifest(
            [StaticWebAsset.Create("css/app.css", css), StaticWebAsset.Create("_framework/blazor.web.js", script)],
            [webRoot],
            manifest);

        using var json = JsonDocument.Parse(File.ReadAllText(manifest));
        var roots = json.RootElement.GetProperty("ContentRoots").EnumerateArray().Select(r => r.GetString()).ToList();
        var root = json.RootElement.GetProperty("Root");
        var framework = root.GetProperty("Children").GetProperty("_framework");
        var blazor = framework.GetProperty("Children").GetProperty("blazor.web.js").GetProperty("Asset");
        Assert.Equal(Path.GetDirectoryName(script) + Path.DirectorySeparatorChar, roots[blazor.GetProperty("ContentRootIndex").GetInt32()]);
        Assert.Equal("blazor.web.js", blazor.GetProperty("SubPath").GetString());
        var pattern = Assert.Single(root.GetProperty("Patterns").EnumerateArray());
        Assert.Equal(webRoot + Path.DirectorySeparatorChar, roots[pattern.GetProperty("ContentRootIndex").GetInt32()]);
        Assert.Equal("**", pattern.GetProperty("Pattern").GetString());
        Assert.Equal(JsonValueKind.Null, framework.GetProperty("Patterns").ValueKind);
    }

    [Fact]
    public void Writes_an_endpoints_manifest_with_the_headers_MapStaticAssets_sends()
    {
        var css = _temp.Write("App/wwwroot/app.css", "body { color: red; }");
        var manifest = _temp.Resolve("bin/App.staticwebassets.endpoints.json");

        StaticAssetManifests.WriteEndpointsManifest(
            [StaticWebAsset.Create("app.css", css), StaticWebAsset.Create("app.css", _temp.Write("other.css", "")), StaticWebAsset.Create("missing.css", _temp.Resolve("missing.css"))],
            manifest);

        using var json = JsonDocument.Parse(File.ReadAllText(manifest));
        Assert.Equal(1, json.RootElement.GetProperty("Version").GetInt32());
        Assert.Equal("Build", json.RootElement.GetProperty("ManifestType").GetString());
        var endpoint = Assert.Single(json.RootElement.GetProperty("Endpoints").EnumerateArray());
        Assert.Equal("app.css", endpoint.GetProperty("Route").GetString());
        Assert.Equal("app.css", endpoint.GetProperty("AssetFile").GetString());
        var headers = endpoint.GetProperty("ResponseHeaders").EnumerateArray()
            .ToDictionary(h => h.GetProperty("Name").GetString()!, h => h.GetProperty("Value").GetString());
        Assert.Equal("text/css", headers["Content-Type"]);
        Assert.Equal("20", headers["Content-Length"]);
        Assert.Equal("no-cache", headers["Cache-Control"]);
        Assert.Equal($"\"{StaticAssetManifests.Describe(css).Hash}\"", headers["ETag"]);
        Assert.True(headers.ContainsKey("Last-Modified"));
    }

    [Fact]
    public void Lists_the_JavaScript_initializers_of_the_site_as_the_SDK_does()
    {
        var file = _temp.Resolve("obj/App.modules.json");

        var written = JavaScriptInitializers.Write(
            [
                StaticWebAsset.Create("app.css", _temp.Write("a.css")),
                StaticWebAsset.Create("_content/Library/Library.lib.module.js", _temp.Write("b.js")),
                StaticWebAsset.Create("App.lib.module.js", _temp.Write("c.js")),
            ],
            file);

        Assert.Equal(file, written);
        Assert.Equal("""["_content/Library/Library.lib.module.js","App.lib.module.js"]""", File.ReadAllText(file));
        Assert.Null(JavaScriptInitializers.Write([StaticWebAsset.Create("app.css", _temp.Write("a.css"))], _temp.Resolve("obj/None.modules.json")));
    }

    [Theory]
    [InlineData("_framework/dotnet.native.wasm", "application/wasm")]
    [InlineData("_framework/dotnet.js", "text/javascript")]
    [InlineData("_framework/App.dll", "application/octet-stream")]
    [InlineData("_framework/icudt_EFIGS.dat", "application/octet-stream")]
    [InlineData("index.html", "text/html")]
    [InlineData("App.styles.css", "text/css")]
    [InlineData("favicon.png", "image/png")]
    [InlineData("appsettings.json", "application/json")]
    [InlineData("fonts/x.woff2", "font/woff2")]
    public void Serves_each_file_with_its_media_type(string route, string expected) =>
        Assert.Equal(expected, ContentTypes.For(route));

    public void Dispose() => _temp.Dispose();

    /// <summary>The layout components of the Blazor Web App template, with their scoped CSS from the template.</summary>
    private ProjectInfo BlazorTemplateLayout()
    {
        var project = Project("BlazorApp1", WebProject);
        foreach (var name in new[] { "MainLayout", "NavMenu", "ReconnectModal" })
        {
            _temp.Write($"BlazorApp1/Components/Layout/{name}.razor", "<div></div>");
            _temp.Write($"BlazorApp1/Components/Layout/{name}.razor.css", TestData.Read($"ScopedCss/BlazorApp1.{name}.css"));
        }

        return project;
    }

    private ProjectInfo Project(string name, string xml) => ProjectParser.Load(_temp.Write($"{name}/{name}.csproj", xml));
}
