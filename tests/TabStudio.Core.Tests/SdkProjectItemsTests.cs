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
                <InternalsVisibleTo Include="Lib.Specs" PublicKey="0024beef" />
                <InternalsVisibleTo Include="$(Unknown).Skipped" />
                <AssemblyAttribute Include="System.Runtime.CompilerServices.InternalsVisibleToAttribute">
                  <_Parameter1>Lib.Benchmarks, PublicKey=0024ffff</_Parameter1>
                </AssemblyAttribute>
                <AssemblyAttribute Include="System.Reflection.AssemblyMetadataAttribute" _Parameter1="Built" _Parameter2="today" />
                <AssemblyAttribute Include="System.CLSCompliantAttribute" _Parameter1="false" _Parameter1_IsLiteral="true" />
              </ItemGroup>
              <ItemGroup><FriendProject Include="Lib.Benchmarks" /></ItemGroup>
              <Target Name="Stamp"><ItemGroup><AssemblyAttribute Include="Never.Generated" /></ItemGroup></Target>
              <Target Name="AddFriends" BeforeTargets="BeforeCompile" Condition=" '@(FriendProject)' != '' ">
                <ItemGroup><InternalsVisibleTo Include="%(FriendProject.Identity)" PublicKey="$(PublicKey)" /></ItemGroup>
              </Target>
            </Project>
            """);

        var attributes = project.AssemblyAttributes.Select(a => (a.TypeName, string.Join("|", a.Arguments.Select(x => x.IsLiteral ? "=" + x.Value : x.Value)))).ToList();

        Assert.Equal(
            new (string, string)[]
            {
                (AssemblyAttributeItems.InternalsVisibleTo, "Lib.Tests, PublicKey=0024abcd"),
                (AssemblyAttributeItems.InternalsVisibleTo, "DynamicProxyGenAssembly2, PublicKey=00240000"),
                (AssemblyAttributeItems.InternalsVisibleTo, "Lib.Specs, PublicKey=0024beef"),
                (AssemblyAttributeItems.InternalsVisibleTo, "Lib.Benchmarks, PublicKey=0024abcd"),
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
    public void Imports_of_the_Directory_files_and_the_project_bring_their_properties_and_items()
    {
        // Polly: Directory.Build.props imports eng/Common.props, and Directory.Build.targets imports
        // eng/$(ProjectType).targets with a package and usings. Humanizer: a project imports its own props file.
        using var temp = new TempDirectory();
        temp.Write("Directory.Build.props", """
            <Project><Import Project="$(MsBuildThisFileDirectory)eng/Common.props" /></Project>
            """);
        temp.Write("eng/Common.props", """
            <Project>
              <PropertyGroup><ImplicitUsings>enable</ImplicitUsings><SignAssembly>true</SignAssembly></PropertyGroup>
              <ItemGroup><Using Include="System.Collections.Concurrent" /></ItemGroup>
            </Project>
            """);
        temp.Write("Directory.Build.targets", """
            <Project>
              <Import Project="$(MsBuildThisFileDirectory)eng/$(ProjectType).targets" Condition="$(ProjectType) != ''" />
              <ItemGroup><Using Remove="System.Net.Http" /></ItemGroup>
            </Project>
            """);
        temp.Write("eng/Benchmark.targets", """
            <Project><ItemGroup><PackageReference Include="BenchmarkDotNet" /><Using Include="BenchmarkDotNet.Attributes" /></ItemGroup></Project>
            """);
        temp.Write("Directory.Packages.props", """
            <Project><PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup></Project>
            """);
        temp.Write("src/Bench/Analyzers.props", """
            <Project><ItemGroup><PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="$(RoslynVersion).0" /></ItemGroup></Project>
            """);
        var path = temp.Write("src/Bench/Bench.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><ProjectType>Benchmark</ProjectType><RoslynVersion>3.8</RoslynVersion></PropertyGroup>
              <Import Project="Analyzers.props" />
            </Project>
            """);

        var project = ProjectParser.Load(path);

        Assert.True(project.ImplicitUsings);
        Assert.Equal("true", project.GetProperty("SignAssembly"));
        Assert.Equal(new[] { "net8.0" }, project.TargetFrameworks);
        Assert.Contains(project.PackageReferences, p => p.Name == "BenchmarkDotNet");
        // Items are expanded with the properties at the end of the evaluation, as MSBuild expands them.
        Assert.Contains(project.PackageReferences, p => p.Name == "Microsoft.CodeAnalysis.CSharp" && p.Version == "3.8.0");
        Assert.Contains(project.Usings, u => u.Name == "System.Collections.Concurrent");
        Assert.Contains(project.Usings, u => u.Name == "BenchmarkDotNet.Attributes");
        Assert.Contains("System.Net.Http", project.RemovedUsings);
    }

    [Fact]
    public void Generators_get_the_resx_files_a_target_adds_before_the_compiler_runs()
    {
        // MudBlazor's LanguageResource comes from ResxSourceGenerator, and Moq's strings from ThisAssembly.Strings: both
        // packages hand the generator the project's .resx files from a target, which TabStudio does not run.
        using var temp = new TempDirectory();
        temp.Write("Lib/Resources/LanguageResource.resx", "<root />");
        temp.Write("Lib/Resources/LanguageResource.de.resx", "<root />");
        temp.Write("Lib/Strings.resx", "<root />");
        var path = temp.Write("Lib/Lib.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
              <ItemGroup>
                <EmbeddedResource Update="Resources\LanguageResource.resx"><AsConstants>true</AsConstants></EmbeddedResource>
              </ItemGroup>
            </Project>
            """);
        var package = temp.Write("packages/thisassembly.strings/build/ThisAssembly.Strings.targets", """
            <Project>
              <Target Name="_InjectResxAdditionalFiles" BeforeTargets="GenerateMSBuildEditorConfigFileShouldRun" DependsOnTargets="PrepareResourceNames">
                <ItemGroup>
                  <ResxCode Include="@(EmbeddedResource)" ThisAssemblyStrings="true"
                            Condition="'%(EmbeddedResource.Type)' == 'Resx' And '%(EmbeddedResource.WithCulture)' != 'true' And '%(EmbeddedResource.ManifestResourceName)' != ''" />
                  <AdditionalFiles Include="@(ResxCode)" />
                </ItemGroup>
              </Target>
              <ItemGroup>
                <CompilerVisibleItemMetadata Include="ResxCode" MetadataName="ManifestResourceName" />
              </ItemGroup>
            </Project>
            """);

        var project = ProjectParser.WithPackageBuildFiles(ProjectParser.Load(path), [package]);

        var files = project.AdditionalFiles.Where(f => f.ItemTypes is not null).OrderBy(f => f.Include, StringComparer.Ordinal).ToList();
        Assert.Equal(new[] { Path.Combine("Resources", "LanguageResource.resx"), "Strings.resx" }, files.Select(f => f.Include));
        Assert.Equal("Lib.Resources.LanguageResource", files[0].Metadata["ManifestResourceName"]);
        Assert.Equal("true", files[0].Metadata["AsConstants"]);
        Assert.Equal("true", files[0].Metadata["ThisAssemblyStrings"]);
        Assert.Equal(new[] { "ResxCode", "AdditionalFiles" }, files[1].ItemTypes);
    }

    [Fact]
    public void Project_references_keep_their_metadata_and_packages_their_PrivateAssets()
    {
        var project = ProjectParser.Parse(ProjectPath, """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <ProjectReference Include="..\Core\Core.csproj" />
                <ProjectReference Include="..\Gen\Gen.csproj" OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
                <ProjectReference Include="..\Attributes\Attributes.csproj" OutputItemType="Analyzer" />
                <ProjectReference Include="..\Shared\Shared.csproj"><PrivateAssets>all</PrivateAssets></ProjectReference>
                <PackageReference Include="Mediator.SourceGenerator" Version="3.0.2"><PrivateAssets>all</PrivateAssets></PackageReference>
                <PackageReference Include="Vogen" Version="8.0.7" />
              </ItemGroup>
            </Project>
            """);

        var references = project.ProjectReferenceItems.ToDictionary(r => Path.GetFileNameWithoutExtension(r.Path));
        Assert.True(references["Core"].IsCompiledAgainst);
        Assert.False(references["Core"].IsAnalyzer);
        Assert.False(references["Gen"].IsCompiledAgainst);
        Assert.True(references["Gen"].IsAnalyzer);

        // As with the SDK, an analyzer reference that keeps ReferenceOutputAssembly is both: StronglyTypedId's attributes.
        Assert.True(references["Attributes"].IsCompiledAgainst);
        Assert.True(references["Attributes"].IsAnalyzer);
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
