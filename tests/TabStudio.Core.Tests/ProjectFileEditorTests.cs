using TabStudio.Core.Workspaces;
using TabStudio.Testing;

namespace TabStudio.Core.Tests;

public sealed class ProjectFileEditorTests : IDisposable
{
    private const string AppProject = """
        <Project Sdk="Microsoft.NET.Sdk">

          <PropertyGroup>
            <OutputType>Exe</OutputType>
            <TargetFramework>net10.0</TargetFramework>
          </PropertyGroup>

        </Project>
        """;

    private readonly TempDirectory _temp = new();

    [Fact]
    public void A_project_reference_goes_into_a_new_item_group_with_a_relative_path()
    {
        var app = _temp.Write("src/App/App.csproj", AppProject);
        var lib = _temp.Write("src/Lib/Lib.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\" />");

        Assert.True(ProjectFileEditor.AddProjectReference(app, lib));
        Assert.False(ProjectFileEditor.AddProjectReference(app, lib));

        var text = File.ReadAllText(app);
        Assert.Contains("""
              <ItemGroup>
                <ProjectReference Include="..\Lib\Lib.csproj" />
              </ItemGroup>
            """.Replace("\r\n", "\n", StringComparison.Ordinal), text.Replace("\r\n", "\n", StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.StartsWith("<Project Sdk=\"Microsoft.NET.Sdk\">\n\n  <PropertyGroup>", text.Replace("\r\n", "\n", StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.Equal(new[] { lib }, ProjectParser.Load(app).ProjectReferences);
    }

    [Fact]
    public void References_join_an_existing_item_group_and_can_be_removed()
    {
        var app = _temp.Write("App/App.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <ProjectReference Include="..\One\One.csproj" />
              </ItemGroup>
            </Project>
            """);
        var one = _temp.Write("One/One.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        var two = _temp.Write("Two/Two.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\" />");

        ProjectFileEditor.AddProjectReference(app, two);
        Assert.Single(File.ReadAllText(app).Split("<ItemGroup>"), part => part.Contains("Two.csproj", StringComparison.Ordinal));
        Assert.Equal(2, ProjectParser.Load(app).ProjectReferences.Count);

        Assert.True(ProjectFileEditor.RemoveProjectReference(app, one));
        Assert.True(ProjectFileEditor.RemoveProjectReference(app, two));
        Assert.Empty(ProjectParser.Load(app).ProjectReferences);
        Assert.DoesNotContain("ItemGroup", File.ReadAllText(app), StringComparison.Ordinal);
    }

    [Fact]
    public void Package_references_are_added_and_updated_in_the_project()
    {
        var app = _temp.Write("App/App.csproj", AppProject);

        Assert.True(ProjectFileEditor.SetPackageReference(app, "Humanizer.Core", "2.14.1"));
        Assert.False(ProjectFileEditor.SetPackageReference(app, "humanizer.core", "2.14.1"));
        Assert.True(ProjectFileEditor.SetPackageReference(app, "Humanizer.Core", "3.0.1"));

        var package = Assert.Single(ProjectParser.Load(app).PackageReferences);
        Assert.Equal(new PackageReferenceInfo("Humanizer.Core", "3.0.1"), package);
        Assert.True(ProjectFileEditor.RemovePackageReference(app, "Humanizer.Core"));
        Assert.Empty(ProjectParser.Load(app).PackageReferences);
    }

    [Fact]
    public void With_central_package_management_the_version_goes_into_Directory_Packages_props()
    {
        var props = _temp.Write("Directory.Packages.props", """
            <Project>
              <PropertyGroup>
                <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
              </PropertyGroup>
            </Project>
            """);
        var app = _temp.Write("src/App/App.csproj", AppProject);

        Assert.Equal(props, ProjectFileEditor.FindCentralVersionsFile(app));
        ProjectFileEditor.SetPackageReference(app, "Newtonsoft.Json", "13.0.3");

        Assert.Contains("<PackageReference Include=\"Newtonsoft.Json\" />", File.ReadAllText(app), StringComparison.Ordinal);
        Assert.Contains("<PackageVersion Include=\"Newtonsoft.Json\" Version=\"13.0.3\" />", File.ReadAllText(props), StringComparison.Ordinal);

        ProjectFileEditor.SetPackageReference(app, "Newtonsoft.Json", "13.0.4");
        Assert.Contains("Version=\"13.0.4\"", File.ReadAllText(props), StringComparison.Ordinal);
    }

    [Fact]
    public void Windows_line_endings_are_kept()
    {
        var app = _temp.Write("App/App.csproj", AppProject.Replace("\n", "\r\n", StringComparison.Ordinal));
        var lib = _temp.Write("Lib/Lib.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\" />");

        ProjectFileEditor.AddProjectReference(app, lib);

        var text = File.ReadAllText(app);
        Assert.DoesNotContain("\n", text.Replace("\r\n", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
    }

    public void Dispose() => _temp.Dispose();
}
