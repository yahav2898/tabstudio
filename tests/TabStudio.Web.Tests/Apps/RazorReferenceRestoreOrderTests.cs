using TabStudio.Compilation;
using TabStudio.Compilation.Building;
using TabStudio.Core.Diagnostics;
using TabStudio.Core.Workspaces;
using TabStudio.Testing;

namespace TabStudio.Web.Tests.Apps;

/// <summary>
/// The packages of a solution are restored one project after another, in solution order, so a Razor project can have
/// its packages before a component library it references has its own, as MudBlazor.Docs does before MudBlazor. When
/// the Razor project is compiled in between, by IntelliSense or a document's analysis, the library has no components
/// yet; once the library is restored, its components must be found.
/// </summary>
[Collection(NuGetTestGroup.Name)]
public sealed class RazorReferenceRestoreOrderTests(NuGetFixture nuget) : IDisposable
{
    private readonly TempDirectory _temp = new();

    [Fact]
    public async Task A_component_library_restored_after_the_project_that_uses_it_still_gives_it_its_components()
    {
        var solution = WriteSolution();
        var workspace = WorkspaceLoader.Load(solution);
        using var host = new RoslynWorkspaceHost();
        host.Load(workspace, FileSystemTextProvider.Instance);
        var library = workspace.Projects.Single(p => p.Name == "Widgets");
        var user = workspace.Projects.Single(p => p.Name == "Docs");
        var page = _temp.Resolve("Docs/Page.razor");

        // The project that uses the library first, as in solution order.
        var userRestore = await nuget.Restorer.RestoreAsync(user);
        Assert.True(userRestore.Success, string.Join('\n', userRestore.Diagnostics));
        host.SetPackageReferences(user.FilePath, userRestore.CompileAssemblies);
        host.SetAnalyzers(user.FilePath, userRestore.Analyzers);

        // Compiled before the library has its packages: the library's types are not components yet.
        _ = await host.GetDocumentDiagnosticsAsync(page);

        var libraryRestore = await nuget.Restorer.RestoreAsync(library);
        Assert.True(libraryRestore.Success, string.Join('\n', libraryRestore.Diagnostics));
        host.SetPackageReferences(library.FilePath, libraryRestore.CompileAssemblies);
        host.SetAnalyzers(library.FilePath, libraryRestore.Analyzers);

        // Before the fix the page kept "no components" from the library: RZ10012 for <Toggle> and RZ9991 for its @bind-Open.
        var live = await host.GetDocumentDiagnosticsAsync(page);
        Assert.DoesNotContain(live, d => d.Severity >= DiagnosticSeverityLevel.Warning && d.Id.StartsWith("RZ", StringComparison.Ordinal));
        Assert.DoesNotContain(live, d => d.Severity == DiagnosticSeverityLevel.Error);

        var build = await new ProjectBuilder(host).BuildAsync(user);
        Assert.True(build.Success, string.Join('\n', build.Diagnostics.Where(d => d.Severity == DiagnosticSeverityLevel.Error)));
        Assert.DoesNotContain(build.Diagnostics, d => d.Id == "RZ10012");
    }

    public void Dispose() => _temp.Dispose();

    private string WriteSolution()
    {
        _temp.Write("Widgets/Widgets.csproj", """
            <Project Sdk="Microsoft.NET.Sdk.Razor">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <Nullable>enable</Nullable>
                <ImplicitUsings>enable</ImplicitUsings>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Microsoft.AspNetCore.Components.Web" Version="10.0.0" />
              </ItemGroup>
            </Project>
            """);
        _temp.Write("Widgets/Toggle.razor", """
            <button @onclick="() => OpenChanged.InvokeAsync(!Open)">@(Open ? "Close" : "Open")</button>

            @code {
                [Parameter] public bool Open { get; set; }

                [Parameter] public EventCallback<bool> OpenChanged { get; set; }

                [Parameter] public RenderFragment? ChildContent { get; set; }
            }
            """);
        _temp.Write("Docs/Docs.csproj", """
            <Project Sdk="Microsoft.NET.Sdk.Razor">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <Nullable>enable</Nullable>
                <ImplicitUsings>enable</ImplicitUsings>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Microsoft.AspNetCore.Components.Web" Version="10.0.0" />
              </ItemGroup>
              <ItemGroup>
                <ProjectReference Include="..\Widgets\Widgets.csproj" />
              </ItemGroup>
            </Project>
            """);
        _temp.Write("Docs/_Imports.razor", """
            @using Microsoft.AspNetCore.Components.Web
            @using Widgets
            """);
        _temp.Write("Docs/Page.razor", """
            <Toggle @bind-Open="open">
                <span>@(open ? "open" : "closed")</span>
            </Toggle>

            @code {
                private bool open;
            }
            """);

        // The library last, as MudBlazor is in MudBlazor.slnx.
        return _temp.Write("Docs.slnx", """
            <Solution>
              <Project Path="Docs/Docs.csproj" />
              <Project Path="Widgets/Widgets.csproj" />
            </Solution>
            """);
    }
}
