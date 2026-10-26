using TabStudio.Core.Compatibility;
using TabStudio.Core.Workspaces;
using TabStudio.Testing;

namespace TabStudio.Core.Tests;

/// <summary>Project items the .NET SDK acts on that real repositories use: generated assembly attributes, reference metadata and SDKs.</summary>
public sealed class SdkProjectItemsTests
{
    private static readonly string ProjectPath = Path.Combine(Path.GetTempPath(), "Repo", "src", "Lib", "Lib.csproj");

    [Fact]
    public void InternalsVisibleTo_and_AssemblyAttribute_items_become_the_attributes_the_SDK_generates()
    {
        var project = ProjectParser.Parse(ProjectPath, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><PublicKey>0024abcd</PublicKey></PropertyGroup>
              <ItemGroup>
                <InternalsVisibleTo Include="$(AssemblyName).Tests" />
                <InternalsVisibleTo Include="DynamicProxyGenAssembly2" Key="00240000" />
                <InternalsVisibleTo Include="$(Unknown).Skipped" />
                <AssemblyAttribute Include="System.Runtime.CompilerServices.InternalsVisibleToAttribute">
                  <_Parameter1>Lib.Benchmarks, PublicKey=0024ffff</_Parameter1>
                </AssemblyAttribute>
                <AssemblyAttribute Include="System.Reflection.AssemblyMetadataAttribute" _Parameter1="Built" _Parameter2="today" />
                <AssemblyAttribute Include="System.CLSCompliantAttribute" _Parameter1="false" _Parameter1_IsLiteral="true" />
              </ItemGroup>
              <Target Name="Stamp"><ItemGroup><AssemblyAttribute Include="Never.Generated" /></ItemGroup></Target>
            </Project>
            """);

        var attributes = project.AssemblyAttributes.Select(a => (a.TypeName, string.Join("|", a.Arguments.Select(x => x.IsLiteral ? "=" + x.Value : x.Value)))).ToList();

        Assert.Equal(
            new (string, string)[]
            {
                (AssemblyAttributeItems.InternalsVisibleTo, "Lib.Tests, PublicKey=0024abcd"),
                (AssemblyAttributeItems.InternalsVisibleTo, "DynamicProxyGenAssembly2, PublicKey=00240000"),
                ("System.Runtime.CompilerServices.InternalsVisibleToAttribute", "Lib.Benchmarks, PublicKey=0024ffff"),
                ("System.Reflection.AssemblyMetadataAttribute", "Built|today"),
                ("System.CLSCompliantAttribute", "=false"),
            },
            attributes);
    }

    [Fact]
    public void GenerateAssemblyInfo_false_generates_nothing_and_items_in_Directory_Build_props_count()
    {
        using var temp = new TempDirectory();
        temp.Write("Directory.Build.props", """
            <Project><ItemGroup><InternalsVisibleTo Include="$(MSBuildProjectName).Tests" /></ItemGroup></Project>
            """);
        var path = temp.Write("src/Core/Core.csproj", """<Project Sdk="Microsoft.NET.Sdk" />""");
        var off = temp.Write("src/Off/Off.csproj", """
            <Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><GenerateAssemblyInfo>false</GenerateAssemblyInfo></PropertyGroup></Project>
            """);

        Assert.Equal("Core.Tests", Assert.Single(ProjectParser.Load(path).AssemblyAttributes).Arguments[0].Value);
        Assert.Empty(ProjectParser.Load(off).AssemblyAttributes);
    }

    [Fact]
    public void MSBuildThisFileDirectory_means_the_file_that_sets_the_property()
    {
        // Serilog: <AssemblyOriginatorKeyFile>$(MSBuildThisFileDirectory)assets/Serilog.snk</AssemblyOriginatorKeyFile>
        using var temp = new TempDirectory();
        temp.Write("Directory.Build.props", """
            <Project><PropertyGroup><AssemblyOriginatorKeyFile>$(MSBuildThisFileDirectory)assets/Repo.snk</AssemblyOriginatorKeyFile></PropertyGroup></Project>
            """);
        var path = temp.Write("src/Lib/Lib.csproj", """
            <Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><Notes>$(MSBuildThisFileDirectory)notes</Notes></PropertyGroup></Project>
            """);

        var project = ProjectParser.Load(path);

        Assert.Equal(Path.Combine(temp.Path, "assets/Repo.snk"), project.GetProperty("AssemblyOriginatorKeyFile"));
        Assert.Equal(Path.Combine(temp.Path, "src", "Lib") + Path.DirectorySeparatorChar + "notes", project.GetProperty("Notes"));
        Assert.Equal("Lib", project.GetProperty("MSBuildProjectName"));
    }

    [Fact]
    public void Project_references_keep_their_metadata_and_packages_their_PrivateAssets()
    {
        var project = ProjectParser.Parse(ProjectPath, """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <ProjectReference Include="..\Core\Core.csproj" />
                <ProjectReference Include="..\Gen\Gen.csproj" OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
                <ProjectReference Include="..\Shared\Shared.csproj"><PrivateAssets>all</PrivateAssets></ProjectReference>
                <PackageReference Include="Mediator.SourceGenerator" Version="3.0.2"><PrivateAssets>all</PrivateAssets></PackageReference>
                <PackageReference Include="Vogen" Version="8.0.7" />
              </ItemGroup>
            </Project>
            """);

        var references = project.ProjectReferenceItems.ToDictionary(r => Path.GetFileNameWithoutExtension(r.Path));
        Assert.True(references["Core"].IsCompiledAgainst);
        Assert.False(references["Gen"].IsCompiledAgainst);
        Assert.True(references["Shared"].IsPrivate);
        Assert.True(project.PackageReferences.Single(p => p.Name == "Mediator.SourceGenerator").IsPrivate);
        Assert.False(project.PackageReferences.Single(p => p.Name == "Vogen").IsPrivate);
    }

    [Theory]
    [InlineData("""<Project Sdk="Aspire.AppHost.Sdk/13.3.5"><PropertyGroup><OutputType>Exe</OutputType></PropertyGroup></Project>""")]
    [InlineData("""<Project Sdk="Microsoft.NET.Sdk"><Sdk Name="Aspire.AppHost.Sdk" Version="13.0.0" /><PropertyGroup><OutputType>Exe</OutputType></PropertyGroup></Project>""")]
    [InlineData("""<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><IsAspireHost>true</IsAspireHost></PropertyGroup></Project>""")]
    public void An_Aspire_AppHost_is_classified_honestly_and_never_reported_ready(string xml)
    {
        var project = ProjectParser.Parse(ProjectPath, xml);
        var report = CompatibilityDoctor.Diagnose(project);

        Assert.Equal(ProjectKind.AspireAppHost, project.Kind);
        Assert.Equal(CompatibilityStatus.Unsupported, report.Status);
        Assert.Contains("computer", report.Explanation, StringComparison.Ordinal);
    }
}
