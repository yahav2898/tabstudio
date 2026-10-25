using TabStudio.Compilation.Razor;
using TabStudio.Testing;

namespace TabStudio.Web.Tests.Apps;

/// <summary>
/// Razor Pages and MVC apps end to end against nuget.org: their <c>.cshtml</c> pages and views compile with the SDK's
/// Razor compiler, MVC finds them in the assembly, tag helpers run, and their layout's scoped CSS is served.
/// </summary>
[Collection(NuGetCollection.Name)]
public sealed class RazorPagesAndMvcTests(NuGetFixture nuget) : IDisposable
{
    private readonly TempDirectory _temp = new();

    [Fact]
    public async Task Serves_Razor_Pages_with_their_models_tag_helpers_and_scoped_CSS()
    {
        var path = _temp.Write("Pages1/Pages1.csproj", WebProject);
        _temp.Write("Pages1/Program.cs", """
            var builder = WebApplication.CreateBuilder(args);
            builder.Services.AddRazorPages();

            var app = builder.Build();
            app.MapStaticAssets();
            app.MapRazorPages().WithStaticAssets();
            app.Run();
            """);
        _temp.Write("Pages1/Pages/_ViewImports.cshtml", """
            @namespace Pages1.Pages
            @addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers
            """);
        _temp.Write("Pages1/Pages/_ViewStart.cshtml", """
            @{
                Layout = "_Layout";
            }
            """);
        _temp.Write("Pages1/Pages/Shared/_Layout.cshtml", """
            <!DOCTYPE html>
            <html lang="en">
            <head>
                <title>@ViewData["Title"] - Pages1</title>
                <link rel="stylesheet" href="~/Pages1.styles.css" asp-append-version="true" />
            </head>
            <body>
                <nav><a asp-page="/Privacy">Privacy</a></nav>
                @RenderBody()
            </body>
            </html>
            """);
        var layoutCss = _temp.Write("Pages1/Pages/Shared/_Layout.cshtml.css", "nav { border-bottom: 1px solid green; }\n");
        _temp.Write("Pages1/Pages/Index.cshtml", """
            @page
            @model IndexModel
            @{
                ViewData["Title"] = "Home page";
            }

            <h1>@Model.Message</h1>
            """);
        _temp.Write("Pages1/Pages/Index.cshtml.cs", """
            using Microsoft.AspNetCore.Mvc.RazorPages;

            namespace Pages1.Pages;

            public class IndexModel : PageModel
            {
                public string Message { get; private set; } = string.Empty;

                public void OnGet() => Message = "Hello from Razor Pages";
            }
            """);
        _temp.Write("Pages1/Pages/Privacy.cshtml", """
            @page
            <h1>Privacy Policy</h1>
            """);

        using var workspace = await WebAppWorkspace.LoadAsync(nuget, path);
        var scope = CssScope.Compute(workspace.Project("Pages1"), layoutCss);
        await using var app = await workspace.StartServerAppAsync("Pages1");

        var home = await nuget.Http.GetStringAsync(app.Address);
        Assert.Contains("<h1>Hello from Razor Pages</h1>", home, StringComparison.Ordinal);
        Assert.Contains("<title>Home page - Pages1</title>", home, StringComparison.Ordinal);
        // As in the SDK's build, the scope goes on the layout's own elements and not on those a tag helper writes.
        Assert.Contains($"<nav {scope}><a href=\"/Privacy\">Privacy</a></nav>", home, StringComparison.Ordinal);
        Assert.Contains("href=\"/Pages1.styles.css", home, StringComparison.Ordinal);
        Assert.Contains("<h1>Privacy Policy</h1>", await nuget.Http.GetStringAsync(new Uri(app.Address, "Privacy")), StringComparison.Ordinal);
        Assert.Contains($"nav[{scope}]", await nuget.Http.GetStringAsync(new Uri(app.Address, "Pages1.styles.css")), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Serves_MVC_views_with_their_models_and_tag_helpers()
    {
        var path = _temp.Write("Mvc1/Mvc1.csproj", WebProject);
        _temp.Write("Mvc1/Program.cs", """
            var builder = WebApplication.CreateBuilder(args);
            builder.Services.AddControllersWithViews();

            var app = builder.Build();
            app.MapStaticAssets();
            app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}").WithStaticAssets();
            app.Run();
            """);
        _temp.Write("Mvc1/Models/PrivacyModel.cs", """
            namespace Mvc1.Models;

            public record PrivacyModel(string Text);
            """);
        _temp.Write("Mvc1/Controllers/HomeController.cs", """
            using Microsoft.AspNetCore.Mvc;
            using Mvc1.Models;

            namespace Mvc1.Controllers;

            public class HomeController : Controller
            {
                public IActionResult Index()
                {
                    ViewData["Name"] = "MVC";
                    return View();
                }

                public IActionResult Privacy() => View(new PrivacyModel("Kept private"));
            }
            """);
        _temp.Write("Mvc1/Views/_ViewImports.cshtml", """
            @using Mvc1.Models
            @addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers
            """);
        _temp.Write("Mvc1/Views/_ViewStart.cshtml", """
            @{
                Layout = "_Layout";
            }
            """);
        _temp.Write("Mvc1/Views/Shared/_Layout.cshtml", """
            <!DOCTYPE html>
            <html lang="en">
            <body>
                <a asp-controller="Home" asp-action="Privacy">Privacy</a>
                @RenderBody()
            </body>
            </html>
            """);
        _temp.Write("Mvc1/Views/Home/Index.cshtml", "<h1>Hello from @ViewData[\"Name\"]</h1>\n");
        _temp.Write("Mvc1/Views/Home/Privacy.cshtml", """
            @model PrivacyModel
            <p>@Model.Text</p>
            """);

        using var workspace = await WebAppWorkspace.LoadAsync(nuget, path);
        await using var app = await workspace.StartServerAppAsync("Mvc1");

        var home = await nuget.Http.GetStringAsync(app.Address);
        Assert.Contains("<h1>Hello from MVC</h1>", home, StringComparison.Ordinal);
        Assert.Contains("<a href=\"/Home/Privacy\">Privacy</a>", home, StringComparison.Ordinal);
        Assert.Contains("<p>Kept private</p>", await nuget.Http.GetStringAsync(new Uri(app.Address, "Home/Privacy")), StringComparison.Ordinal);
    }

    public void Dispose() => _temp.Dispose();

    private const string WebProject = """
        <Project Sdk="Microsoft.NET.Sdk.Web">
          <PropertyGroup>
            <TargetFramework>net10.0</TargetFramework>
            <Nullable>enable</Nullable>
            <ImplicitUsings>enable</ImplicitUsings>
          </PropertyGroup>
        </Project>
        """;
}
