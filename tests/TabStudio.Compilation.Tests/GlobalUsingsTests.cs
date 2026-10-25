using TabStudio.Compilation.Building;
using TabStudio.Core.Workspaces;
using TabStudio.Testing;

namespace TabStudio.Compilation.Tests;

/// <summary>
/// A project's global usings: the SDK's implicit ones and its <c>Using</c> items, which test project templates use
/// to import their framework (<c>&lt;Using Include="Xunit" /&gt;</c>).
/// </summary>
public sealed class GlobalUsingsTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    [Fact]
    public void Using_items_join_the_implicit_usings_and_can_remove_them()
    {
        var project = ProjectParser.Parse(_temp.Resolve("Tests/Tests.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><ImplicitUsings>enable</ImplicitUsings></PropertyGroup>
              <ItemGroup>
                <Using Include="Xunit" />
                <Using Include="System.Math" Static="true" />
                <Using Include="System.Text.StringBuilder" Alias="Builder" />
                <Using Remove="System.Net.Http" />
              </ItemGroup>
            </Project>
            """);

        var source = CSharpCompiler.CreateGlobalUsingsSource(project);

        Assert.NotNull(source);
        Assert.Contains("global using global::System.Linq;\n", source, StringComparison.Ordinal);
        Assert.Contains("global using global::Xunit;\n", source, StringComparison.Ordinal);
        Assert.Contains("global using static global::System.Math;\n", source, StringComparison.Ordinal);
        Assert.Contains("global using Builder = global::System.Text.StringBuilder;\n", source, StringComparison.Ordinal);
        Assert.DoesNotContain("System.Net.Http", source, StringComparison.Ordinal);
    }

    [Fact]
    public void A_project_without_global_usings_gets_no_global_usings_file()
    {
        var project = ProjectParser.Parse(_temp.Resolve("Lib/Lib.csproj"), """<Project Sdk="Microsoft.NET.Sdk" />""");

        Assert.Null(CSharpCompiler.CreateGlobalUsingsSource(project));
    }

    [Fact]
    public async Task Code_compiles_with_the_usings_of_using_items_alone()
    {
        _temp.Write("Lib/Lib.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
              <ItemGroup>
                <Using Include="System.Text" />
                <Using Include="System.Console" Static="true" />
              </ItemGroup>
            </Project>
            """);
        _temp.Write("Lib/Writer.cs", """
            public static class Writer
            {
                public static void Write() => WriteLine(new StringBuilder("written").ToString());
            }
            """);
        using var host = new RoslynWorkspaceHost();
        host.Load(WorkspaceLoader.Load(_temp.Resolve("Lib")), FileSystemTextProvider.Instance);

        var build = await new ProjectBuilder(host).BuildAsync(host.Projects.Single());

        Assert.True(build.Success, string.Join('\n', build.Diagnostics));
    }

    public void Dispose() => _temp.Dispose();
}
