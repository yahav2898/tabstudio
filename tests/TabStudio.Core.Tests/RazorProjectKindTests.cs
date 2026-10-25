using TabStudio.Core.Workspaces;
using TabStudio.Testing;

namespace TabStudio.Core.Tests;

/// <summary>How web projects with Razor files are recognized, as the Web SDK decides it.</summary>
public sealed class RazorProjectKindTests : IDisposable
{
    private const string WebProject = """
        <Project Sdk="Microsoft.NET.Sdk.Web">
          <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
        </Project>
        """;

    private readonly TempDirectory _temp = new();

    [Fact]
    public void A_web_app_with_Razor_components_is_a_Blazor_Web_App()
    {
        var path = _temp.Write("BlazorApp1/BlazorApp1.csproj", WebProject);
        _temp.Write("BlazorApp1/Components/Pages/Home.razor", "@page \"/\"");

        var project = ProjectParser.Load(path);

        Assert.True(project.HasRazorComponents);
        Assert.Equal(ProjectKind.BlazorWebApp, project.Kind);
        Assert.True(project.IsExecutable);
    }

    [Fact]
    public void A_web_app_with_only_views_and_pages_stays_an_ASP_NET_Core_app()
    {
        var path = _temp.Write("WebApplication1/WebApplication1.csproj", WebProject);
        _temp.Write("WebApplication1/Pages/Index.cshtml", "@page");
        _temp.Write("WebApplication1/obj/Debug/Stale.razor", "<p>build output</p>");

        var project = ProjectParser.Load(path);

        Assert.False(project.HasRazorComponents);
        Assert.Equal(ProjectKind.AspNetCore, project.Kind);
    }

    [Fact]
    public void A_Razor_class_library_is_a_library_and_a_Blazor_WebAssembly_app_is_an_app()
    {
        var library = ProjectParser.Parse(
            _temp.Resolve("Lib/Lib.csproj"),
            """<Project Sdk="Microsoft.NET.Sdk.Razor"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>""");
        var wasm = ProjectParser.Parse(
            _temp.Resolve("App/App.csproj"),
            """<Project Sdk="Microsoft.NET.Sdk.BlazorWebAssembly"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>""");

        Assert.Equal(ProjectKind.ClassLibrary, library.Kind);
        Assert.Equal(ProjectKind.BlazorWebAssembly, wasm.Kind);
        Assert.True(wasm.IsExecutable);
    }

    public void Dispose() => _temp.Dispose();
}
