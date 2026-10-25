using TabStudio.Compilation.Razor;
using TabStudio.Web.StaticWebAssets;

namespace TabStudio.Web.Tests;

/// <summary>
/// Scoped CSS against what the .NET SDK writes for the templates: the same scopes, and stylesheets rewritten byte for
/// byte as its <c>RewriteCss</c> task rewrites them.
/// </summary>
public sealed class ScopedCssTests
{
    public static TheoryData<string, string, string> TemplateStylesheets => new()
    {
        { "BlazorApp1.MainLayout", "BlazorApp1", "Components/Layout/MainLayout.razor.css" },
        { "BlazorApp1.NavMenu", "BlazorApp1", "Components/Layout/NavMenu.razor.css" },
        { "BlazorApp1.ReconnectModal", "BlazorApp1", "Components/Layout/ReconnectModal.razor.css" },
        { "BlazorWasm.MainLayout", "BlazorApp1", "Layout/MainLayout.razor.css" },
        { "BlazorWasm.NavMenu", "BlazorApp1", "Layout/NavMenu.razor.css" },
        { "WebApplication1._Layout", "WebApplication1", "Views/Shared/_Layout.cshtml.css" },
        { "RazorClassLibrary1.Component1", "RazorClassLibrary1", "Component1.razor.css" },
    };

    [Theory]
    [MemberData(nameof(TemplateStylesheets))]
    public void Rewrites_the_templates_stylesheets_as_the_SDK_does(string name, string assemblyName, string relativePath)
    {
        var expected = TestData.Read($"ScopedCss/{name}.expected.css");
        var scope = CssScope.Compute(assemblyName, relativePath);

        var result = ScopedCss.Rewrite(TestData.Read($"ScopedCss/{name}.css"), scope);

        Assert.Empty(result.Errors);
        Assert.Contains($"[{scope}]", expected, StringComparison.Ordinal);
        Assert.Equal(expected, result.Css);
    }

    [Theory]
    [InlineData("h1 { color: red; }", "h1[b-test] { color: red; }")]
    [InlineData("a::before, p:hover { }", "a[b-test]::before, p:hover[b-test] { }")]
    [InlineData("a:after { }", "a[b-test]:after { }")]
    [InlineData(".row ::deep a { }", ".row[b-test]  a { }")]
    [InlineData("::deep .x { }", "[b-test] .x { }")]
    [InlineData("@media (max-width: 600px) { .x { top: 0; } }", "@media (max-width: 600px) { .x[b-test] { top: 0; } }")]
    [InlineData("@font-face { font-family: x; }", "@font-face { font-family: x; }")]
    [InlineData("div > span + i ~ b { }", "div > span + i ~ b[b-test] { }")]
    public void Scopes_every_selector(string css, string expected)
    {
        var result = ScopedCss.Rewrite(css, "b-test");

        Assert.Empty(result.Errors);
        Assert.Equal(expected, result.Css);
    }

    [Fact]
    public void Renames_the_files_keyframes_and_the_animations_that_use_them()
    {
        var result = ScopedCss.Rewrite(
            "@keyframes spin { to { transform: rotate(1turn); } }\n.x { animation: spin 1s infinite; animation-name: spin; }\n.y { animation: fade 1s; }",
            "b-test");

        Assert.Contains("@keyframes spin-b-test", result.Css, StringComparison.Ordinal);
        Assert.Contains("animation: spin-b-test 1s infinite", result.Css, StringComparison.Ordinal);
        Assert.Contains("animation-name: spin-b-test", result.Css, StringComparison.Ordinal);
        Assert.Contains("animation: fade 1s", result.Css, StringComparison.Ordinal);
    }

    [Fact]
    public void An_import_is_an_error_on_its_line()
    {
        var result = ScopedCss.Rewrite("h1 { }\n  @import 'other.css';\n", "b-test");

        var error = Assert.Single(result.Errors);
        Assert.Equal(2, error.Line);
        Assert.Equal(3, error.Column);
        Assert.Equal(ScopedCss.ImportNotAllowed, error.Message);

        var diagnostic = ScopedCss.ToDiagnostic("/app/Counter.razor.css", error, "App");
        Assert.Equal(WebDiagnosticIds.ScopedCss, diagnostic.Id);
        Assert.Equal(9, diagnostic.Location!.Start);
        Assert.Equal(2, diagnostic.Location.Line);
    }

    [Theory]
    [InlineData("BlazorApp1", "Components/Layout/MainLayout.razor.css", "b-nz1yiwl0um")]
    [InlineData("BlazorApp1", "Components/Layout/NavMenu.razor.css", "b-zjo6z1fhij")]
    [InlineData("WebApplication1", "Views/Shared/_Layout.cshtml.css", "b-4ak7nacacg")]
    [InlineData("RazorClassLibrary1", "Component1.razor.css", "b-9hms91upey")]
    public void Computes_the_scope_the_SDK_computes(string assemblyName, string relativePath, string expected) =>
        Assert.Equal(expected, CssScope.Compute(assemblyName, relativePath));
}
