using System.Net;
using TabStudio.Compilation.Razor;
using TabStudio.Testing;

namespace TabStudio.Web.Tests.Apps;

/// <summary>
/// A Razor class library used by a Blazor Web App, end to end against nuget.org: the library builds, its component
/// renders in the app with its scope, and its static web assets and scoped CSS bundle are served under
/// <c>_content/&lt;Library&gt;</c>, imported by the app's stylesheet.
/// </summary>
[Collection(NuGetTestGroup.Name)]
public sealed class RazorClassLibraryTests(NuGetFixture nuget) : IDisposable
{
    private readonly TempDirectory _temp = new();

    [Fact]
    public async Task A_Blazor_Web_App_uses_a_Razor_class_librarys_components_styles_and_files()
    {
        WriteLibrary();
        var app = WriteApp();
        using var workspace = await WebAppWorkspace.LoadAsync(nuget, app);
        var library = workspace.Project("Shared");
        var scope = CssScope.Compute(library, _temp.Resolve("Shared/Component1.razor.css"));

        await using var running = await workspace.StartServerAppAsync("Site");

        var page = await nuget.Http.GetStringAsync(running.Address);
        Assert.Contains($"<div class=\"my-component\" {scope}>This component is defined in the <strong {scope}>Shared</strong> library.</div>", page, StringComparison.Ordinal);
        Assert.StartsWith("@import '_content/Shared/Shared.bundle.scp.css';", await nuget.Http.GetStringAsync(new Uri(running.Address, "Site.styles.css")), StringComparison.Ordinal);
        Assert.Contains($".my-component[{scope}]", await nuget.Http.GetStringAsync(new Uri(running.Address, "_content/Shared/Shared.bundle.scp.css")), StringComparison.Ordinal);
        using var image = await nuget.Http.GetAsync(new Uri(running.Address, "_content/Shared/background.png"));
        Assert.Equal(HttpStatusCode.OK, image.StatusCode);
        Assert.Equal("image/png", image.Content.Headers.ContentType?.MediaType);
    }

    public void Dispose() => _temp.Dispose();

    private void WriteLibrary()
    {
        _temp.Write("Shared/Shared.csproj", """
            <Project Sdk="Microsoft.NET.Sdk.Razor">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <Nullable>enable</Nullable>
                <ImplicitUsings>enable</ImplicitUsings>
              </PropertyGroup>
              <ItemGroup>
                <SupportedPlatform Include="browser" />
              </ItemGroup>
              <ItemGroup>
                <PackageReference Include="Microsoft.AspNetCore.Components.Web" Version="10.0.0" />
              </ItemGroup>
            </Project>
            """);
        _temp.Write("Shared/_Imports.razor", "@using Microsoft.AspNetCore.Components.Web\n");
        _temp.Write("Shared/Component1.razor", """
            <div class="my-component">This component is defined in the <strong>Shared</strong> library.</div>
            """);
        _temp.Write("Shared/Component1.razor.css", ".my-component { border: 2px dashed red; }\n");
        _temp.Write("Shared/wwwroot/background.png", "not really a picture");
    }

    private string WriteApp()
    {
        _temp.Write("Site/Site.csproj", """
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <Nullable>enable</Nullable>
                <ImplicitUsings>enable</ImplicitUsings>
              </PropertyGroup>
              <ItemGroup>
                <ProjectReference Include="..\Shared\Shared.csproj" />
              </ItemGroup>
            </Project>
            """);
        _temp.Write("Site/Program.cs", """
            using Site.Components;

            var builder = WebApplication.CreateBuilder(args);
            builder.Services.AddRazorComponents();

            var app = builder.Build();
            app.UseAntiforgery();
            app.MapStaticAssets();
            app.MapRazorComponents<App>();
            app.Run();
            """);
        _temp.Write("Site/Components/_Imports.razor", """
            @using Microsoft.AspNetCore.Components.Routing
            @using Microsoft.AspNetCore.Components.Web
            @using Shared
            @using Site.Components
            """);
        _temp.Write("Site/Components/App.razor", """
            <!DOCTYPE html>
            <html lang="en">
            <head>
                <base href="/" />
                <link rel="stylesheet" href="@Assets["Site.styles.css"]" />
            </head>
            <body>
                <Router AppAssembly="typeof(Program).Assembly">
                    <Found Context="routeData">
                        <RouteView RouteData="routeData" />
                    </Found>
                </Router>
            </body>
            </html>
            """);
        _temp.Write("Site/Components/Home.razor", """
            @page "/"

            <Component1 />
            """);
        return _temp.Write("Site.slnx", """
            <Solution>
              <Project Path="Site/Site.csproj" />
              <Project Path="Shared/Shared.csproj" />
            </Solution>
            """);
    }
}
