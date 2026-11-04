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
        Assert.Equal(new[] { "EmbeddedResource", "ResxCode", "AdditionalFiles" }, files[1].ItemTypes);
    }

    [Fact]
    public void A_resx_file_two_packages_add_keeps_what_each_says_about_it()
    {
        // moq's Moq.Sdk: ThisAssembly.Resources adds every EmbeddedResource as an additional file before ThisAssembly.Strings
        // adds its .resx files from ResxCode items. The SDK writes build_metadata.ResxCode.* for the file all the same, and
        // without them the strings generator wrote an empty ThisAssembly.Strings (CS0117 for every string).
        using var temp = new TempDirectory();
        temp.Write("Lib/Resources.resx", "<root />");
        var path = temp.Write("Lib/Lib.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
            </Project>
            """);
        var resources = temp.Write("packages/thisassembly.resources/build/netstandard2.0/ThisAssembly.Resources.targets", """
            <Project>
              <ItemGroup>
                <CompilerVisibleItemMetadata Include="EmbeddedResource" MetadataName="ThisAssemblyResource" />
              </ItemGroup>
              <Target Name="PrepareEmbeddedResources" DependsOnTargets="PrepareResourceNames" />
              <Target Name="_InjectResourcesAdditionalFiles" BeforeTargets="GenerateMSBuildEditorConfigFileShouldRun" DependsOnTargets="PrepareEmbeddedResources">
                <ItemGroup>
                  <EmbeddedResource ThisAssemblyResource="true" />
                  <AdditionalFiles Include="@(EmbeddedResource)" />
                </ItemGroup>
              </Target>
            </Project>
            """);
        var strings = temp.Write("packages/thisassembly.strings/build/netstandard2.0/ThisAssembly.Strings.targets", """
            <Project>
              <Target Name="_InjectResxAdditionalFiles" BeforeTargets="GenerateMSBuildEditorConfigFileShouldRun" DependsOnTargets="PrepareResourceNames">
                <ItemGroup>
                  <ResxCode Include="@(EmbeddedResource)" ThisAssemblyStrings="true"
                            Condition="'%(EmbeddedResource.Type)' == 'Resx' And '%(EmbeddedResource.WithCulture)' != 'true' And '%(EmbeddedResource.GenerateResource)' != 'false' And '%(EmbeddedResource.ManifestResourceName)' != ''" />
                  <AdditionalFiles Include="@(ResxCode)" />
                </ItemGroup>
              </Target>
              <ItemGroup>
                <CompilerVisibleItemMetadata Include="ResxCode" MetadataName="ThisAssemblyStrings" />
                <CompilerVisibleItemMetadata Include="ResxCode" MetadataName="ManifestResourceName" />
              </ItemGroup>
            </Project>
            """);

        var project = ProjectParser.WithPackageBuildFiles(ProjectParser.Load(path), [resources, strings]);

        var file = Assert.Single(project.AdditionalFiles, f => f.ItemTypes is not null);
        Assert.Equal("Resources.resx", file.Include);
        Assert.Contains("ResxCode", file.ItemTypes!);
        Assert.Contains("EmbeddedResource", file.ItemTypes!);
        Assert.Equal("true", file.Metadata["ThisAssemblyStrings"]);
        Assert.Equal("true", file.Metadata["ThisAssemblyResource"]);
        Assert.Equal("Lib.Resources", file.Metadata["ManifestResourceName"]);
    }

    [Fact]
    public void Microsoft_Testing_Platform_s_SelfRegisteredExtensions_is_generated_in_the_root_namespace()
    {
        // Terminal.Gui's UnitTests.Parallelizable has `using UnitTests.Parallelizable;`, a namespace only the class
        // Microsoft.Testing.Platform's task generates for an xUnit.net v3 project declares (CS0234 without it).
        using var temp = new TempDirectory();
        var path = temp.Write("UnitTestsParallelizable/UnitTests.Parallelizable.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework><OutputType>Exe</OutputType></PropertyGroup>
            </Project>
            """);
        var props = temp.Write("packages/xunit.v3.core/buildTransitive/xunit.v3.core.props", """
            <Project>
              <PropertyGroup>
                <GenerateSelfRegisteredExtensions>true</GenerateSelfRegisteredExtensions>
                <IsTestingPlatformApplication Condition=" '$(IsTestingPlatformApplication)' == '' ">true</IsTestingPlatformApplication>
              </PropertyGroup>
            </Project>
            """);
        var targets = temp.Write("packages/microsoft.testing.platform.msbuild/buildMultiTargeting/Microsoft.Testing.Platform.MSBuild.targets", """
            <Project>
              <Target Name="_CalculateGenerateSelfRegisteredExtensions">
                <PropertyGroup>
                  <GenerateSelfRegisteredExtensions Condition=" '$(GenerateSelfRegisteredExtensions)' == '' " >$(IsTestingPlatformApplication)</GenerateSelfRegisteredExtensions>
                </PropertyGroup>
              </Target>
              <PropertyGroup>
                <_SelfRegisteredExtensionsSourceName>SelfRegisteredExtensions$(DefaultLanguageSourceExtension)</_SelfRegisteredExtensionsSourceName>
                <_SelfRegisteredExtensionsSourcePath>$([System.IO.Path]::Combine($(IntermediateOutputPath),$(_SelfRegisteredExtensionsSourceName)))</_SelfRegisteredExtensionsSourcePath>
                <_SelfRegisteredExtensionsSourcePath>$([MSBuild]::NormalizePath($(MSBuildProjectDirectory), $(_SelfRegisteredExtensionsSourcePath)))</_SelfRegisteredExtensionsSourcePath>
              </PropertyGroup>
              <Target Name="_GenerateSelfRegisteredExtensions" Condition=" '$(GenerateSelfRegisteredExtensions)' == 'true' " >
                <TestingPlatformSelfRegisteredExtensions SelfRegisteredExtensionsSourcePath="$(_SelfRegisteredExtensionsSourcePath)"
                                                         SelfRegisteredExtensionsBuilderHook="@(TestingPlatformBuilderHook)"
                                                         RootNamespace="$(RootNamespace)"
                                                         Language="$(Language)" />
              </Target>
              <Target Name="_IncludeGenerateAutoRegisteredExtensionsIntoCompilation" BeforeTargets="BeforeCompile;XamlPreCompile"
                      DependsOnTargets="_CalculateGenerateSelfRegisteredExtensions;_GenerateSelfRegisteredExtensions">
                <ItemGroup Condition=" '$(GenerateSelfRegisteredExtensions)' == 'true' ">
                  <Compile Include="$(_SelfRegisteredExtensionsSourcePath)" />
                </ItemGroup>
              </Target>
            </Project>
            """);

        var project = ProjectParser.WithPackageBuildFiles(ProjectParser.Load(path), [props, targets]);

        var file = Assert.Single(project.PackageCompileFiles);
        Assert.Equal(Path.Combine(temp.Path, "UnitTestsParallelizable", "obj", "Debug", "net10.0", "SelfRegisteredExtensions.cs"), file.FullPath);
        Assert.Contains("namespace UnitTests.Parallelizable", file.Text, StringComparison.Ordinal);
        Assert.Contains("internal static class SelfRegisteredExtensions", file.Text, StringComparison.Ordinal);
        Assert.False(File.Exists(file.FullPath));
        Assert.Null(project.GetProperty("RootNamespace"));
    }

    [Fact]
    public void A_source_a_target_writes_from_a_property_gets_the_characters_its_escapes_stand_for()
    {
        // FastEndpoints.Swagger writes its module initializer from a property whose semicolons are written %3b; MSBuild
        // hands WriteLinesToFile the text unescaped. Written as it was, CleanArchitecture's web project had 14 errors.
        using var temp = new TempDirectory();
        var path = temp.Write("Web/Web.csproj", """
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
            </Project>
            """);
        var targets = temp.Write("packages/fastendpoints.swagger/build/FastEndpoints.Swagger.targets", """
            <Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
              <PropertyGroup>
                <SwaggerExportPath Condition="'$(SwaggerExportPath)' == ''">wwwroot/openapi</SwaggerExportPath>
              </PropertyGroup>
              <Target Name="GenerateSwaggerExportPathInitializer" BeforeTargets="CoreCompile" Condition="'$(MSBuildProjectName)' != 'FastEndpoints.Swagger'">
                <PropertyGroup>
                  <_SwaggerInitFile>$(IntermediateOutputPath)SwaggerExportPathInitializer.g.cs</_SwaggerInitFile>
                  <_SwaggerInitFileContent>
            using System.Runtime.CompilerServices%3b
            namespace FastEndpoints.Swagger%3b
            internal static class SwaggerExportPathInitializer
            {
                [ModuleInitializer]
                internal static void Initialize() => DocumentOptions.SwaggerExportPath = "$(SwaggerExportPath)"%3b
            }
                  </_SwaggerInitFileContent>
                </PropertyGroup>
                <WriteLinesToFile File="$(_SwaggerInitFile)" Lines="$(_SwaggerInitFileContent)" Overwrite="true" WriteOnlyWhenDifferent="true"/>
                <ItemGroup>
                  <Compile Include="$(_SwaggerInitFile)"/>
                </ItemGroup>
              </Target>
            </Project>
            """);

        var project = ProjectParser.WithPackageBuildFiles(ProjectParser.Load(path), [targets]);

        var file = Assert.Single(project.PackageCompileFiles);
        Assert.Equal("SwaggerExportPathInitializer.g.cs", Path.GetFileName(file.FullPath));
        Assert.DoesNotContain("%3b", file.Text, StringComparison.Ordinal);
        Assert.Contains("using System.Runtime.CompilerServices;", file.Text, StringComparison.Ordinal);
        Assert.Contains("DocumentOptions.SwaggerExportPath = \"wwwroot/openapi\";", file.Text, StringComparison.Ordinal);
        Assert.Contains("namespace FastEndpoints.Swagger;", file.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_package_s_targets_remove_the_generator_it_replaces()
    {
        // eShop's Ordering.API: Microsoft.Extensions.Telemetry.Abstractions writes [LoggerMessage] code itself and removes
        // Microsoft.Extensions.Logging's generator, from ASP.NET Core and from its package, or every method is
        // implemented twice (CS0757). Its other target, for compilers without component versioning, does not run.
        using var temp = new TempDirectory();
        var path = temp.Write("Api/Api.csproj", """
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
            </Project>
            """);
        var props = temp.Write("packages/telemetry/buildTransitive/net8.0/Microsoft.Extensions.Telemetry.Abstractions.props", """
            <Project><PropertyGroup><DisableMicrosoftExtensionsLoggingSourceGenerator>true</DisableMicrosoftExtensionsLoggingSourceGenerator></PropertyGroup></Project>
            """);
        var targets = temp.Write("packages/telemetry/buildTransitive/net8.0/Microsoft.Extensions.Telemetry.Abstractions.targets", """
            <Project>
              <Target Name="_Microsoft_Extensions_Logging_AbstractionsRemoveAnalyzers"
                      Condition="'$(DisableMicrosoftExtensionsLoggingSourceGenerator)' == 'true'" AfterTargets="ResolveReferences">
                <ItemGroup>
                  <_Logging Include="@(Analyzer)" Condition="'%(Analyzer.AssemblyName)' == 'Microsoft.Extensions.Logging.Generators' Or '%(Analyzer.NuGetPackageId)' == 'Microsoft.Extensions.Logging.Abstractions'" />
                </ItemGroup>
                <ItemGroup><Analyzer Remove="@(_Logging)" /></ItemGroup>
              </Target>
              <Target Name="_OptionsAnalyzerMultiTargeting" Condition="'$(SupportsRoslynComponentVersioning)' != 'true'" AfterTargets="ResolvePackageDependenciesForBuild;ResolveNuGetPackageAssets">
                <ItemGroup><Analyzer Remove="@(Analyzer)" /></ItemGroup>
              </Target>
            </Project>
            """);
        var project = ProjectParser.WithPackageBuildFiles(ProjectParser.Load(path), [props, targets]);
        var framework = Path.Combine(temp.Path, "packs", "microsoft.aspnetcore.app.ref", "10.0.0", "analyzers", "dotnet", "cs", "Microsoft.Extensions.Logging.Generators.dll");
        var package = Path.Combine(temp.Path, "packages", "microsoft.extensions.logging.abstractions", "10.0.0", "analyzers", "dotnet", "roslyn4.4", "cs", "Microsoft.Extensions.Logging.Generators.dll");
        var telemetry = Path.Combine(temp.Path, "packages", "microsoft.extensions.telemetry.abstractions", "10.0.0", "analyzers", "dotnet", "roslyn4.4", "cs", "Microsoft.Gen.Logging.dll");

        var removed = TargetItems.RemovedAnalyzers(project, [
            new AnalyzerItem(framework, "microsoft.aspnetcore.app.ref"),
            new AnalyzerItem(package, "microsoft.extensions.logging.abstractions"),
            new AnalyzerItem(telemetry, "microsoft.extensions.telemetry.abstractions")]);

        Assert.Equal(new[] { framework, package }, removed);
    }

    [Fact]
    public void CommunityToolkit_Mvvm_keeps_its_generators_in_a_C_sharp_project()
    {
        // Its targets remove its generators where no C# compiler is found (CSharpCoreTargetsPath empty) or the compiler
        // is older than Roslyn 4. Running their DependsOnTargets took [ObservableProperty]'s generator away (CS0103 on the
        // properties it writes); the generators stay in C# and go in Visual Basic, as with the SDK.
        using var temp = new TempDirectory();
        var targets = temp.Write("packages/communitytoolkit.mvvm/8.4.0/buildTransitive/CommunityToolkit.Mvvm.SourceGenerators.targets", """
            <Project>
              <Target Name="MVVMToolkitGatherAnalyzers">
                <ItemGroup>
                  <MVVMToolkitAnalyzer Include="@(Analyzer)" Condition="'%(Analyzer.NuGetPackageId)' == 'CommunityToolkit.Mvvm'" />
                </ItemGroup>
              </Target>
              <Target Name="MVVMToolkitRemoveAnalyzersForRosynNotFound" Condition="'$(CSharpCoreTargetsPath)' == ''"
                      AfterTargets="ResolvePackageDependenciesForBuild;ResolveNuGetPackageAssets" DependsOnTargets="MVVMToolkitGatherAnalyzers">
                <ItemGroup><Analyzer Remove="@(MVVMToolkitAnalyzer)"/></ItemGroup>
              </Target>
              <Target Name="MVVMToolkitRemoveAnalyzersForRoslyn3" Condition="'$(CSharpCoreTargetsPath)' != ''"
                      AfterTargets="ResolvePackageDependenciesForBuild;ResolveNuGetPackageAssets" DependsOnTargets="MVVMToolkitGatherAnalyzers">
                <GetAssemblyIdentity AssemblyFiles="$([System.IO.Path]::Combine(`$([System.IO.Path]::GetDirectoryName($(CSharpCoreTargetsPath)))`,`Microsoft.Build.Tasks.CodeAnalysis.dll`))">
                  <Output TaskParameter="Assemblies" ItemName="MVVMToolkitCurrentCompilerAssemblyIdentity"/>
                </GetAssemblyIdentity>
                <PropertyGroup>
                  <MVVMToolkitCurrentCompilerVersion>@(MVVMToolkitCurrentCompilerAssemblyIdentity->'%(Version)')</MVVMToolkitCurrentCompilerVersion>
                  <MVVMToolkitCurrentCompilerVersionIsNotNewEnough Condition="$([MSBuild]::VersionLessThan($(MVVMToolkitCurrentCompilerVersion), 4.0))">true</MVVMToolkitCurrentCompilerVersionIsNotNewEnough>
                </PropertyGroup>
                <ItemGroup Condition ="'$(MVVMToolkitCurrentCompilerVersionIsNotNewEnough)' == 'true'">
                  <Analyzer Remove="@(MVVMToolkitAnalyzer)"/>
                </ItemGroup>
                <PropertyGroup>
                  <MVVMToolkitIsManualRoslynMultiTargetingLogicNeeded Condition="'$(MVVMToolkitCurrentCompilerVersionIsNotNewEnough)' != 'true' AND '$(SupportsRoslynComponentVersioning)' != 'true'">true</MVVMToolkitIsManualRoslynMultiTargetingLogicNeeded>
                </PropertyGroup>
              </Target>
            </Project>
            """);
        var generator = Path.Combine(temp.Path, "packages", "communitytoolkit.mvvm", "8.4.0", "analyzers", "dotnet", "roslyn4.12", "cs", "CommunityToolkit.Mvvm.SourceGenerators.dll");
        var csharp = ProjectParser.WithPackageBuildFiles(ProjectParser.Load(temp.Write("App/App.csproj", """
            <Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>
            """)), [targets]);
        var basic = ProjectParser.WithPackageBuildFiles(ProjectParser.Load(temp.Write("Vb/Vb.vbproj", """
            <Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>
            """)), [targets]);

        Assert.Empty(TargetItems.RemovedAnalyzers(csharp, [new AnalyzerItem(generator, "CommunityToolkit.Mvvm")]));
        Assert.Equal(new[] { generator }, TargetItems.RemovedAnalyzers(basic, [new AnalyzerItem(generator, "CommunityToolkit.Mvvm")]));
    }

    [Fact]
    public void A_target_reads_the_Compile_items_with_their_well_known_metadata()
    {
        // Moq's AddConstants picks the Compile items under Scenarios/ by %(RelativeDir).
        using var temp = new TempDirectory();
        temp.Write("Tests/Scenarios/MultipleUses.cs", "class MultipleUses { }");
        temp.Write("Tests/Other.cs", "class Other { }");
        var path = temp.Write("Tests/Tests.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
              <Target Name="AddScenarios" BeforeTargets="GenerateMSBuildEditorConfigFileShouldRun">
                <ItemGroup>
                  <AdditionalFiles Include="@(Compile)" Condition="$([MSBuild]::ValueOrDefault('%(RelativeDir)', '').StartsWith('Scenarios'))" />
                </ItemGroup>
              </Target>
            </Project>
            """);

        var project = ProjectParser.Load(path);

        var files = project.AdditionalFiles.Where(f => f.ItemTypes is not null).Select(f => f.Include.Replace('\\', '/')).ToList();
        Assert.Equal(new[] { "Scenarios/MultipleUses.cs" }, files);
    }

    [Fact]
    public void Metadata_a_target_sets_from_other_metadata_follows_its_property_functions()
    {
        // ThisAssembly.Constants' PrepareConstants: AreaPath from RelativeDir and Filename, then Area with '\' and '/' as dots.
        using var temp = new TempDirectory();
        temp.Write("Tests/Scenarios/MultipleUses.cs", "class MultipleUses { }");
        var path = temp.Write("Tests/Tests.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
              <Target Name="Areas" BeforeTargets="GenerateMSBuildEditorConfigFileShouldRun">
                <ItemGroup>
                  <FileConstant Include="@(Compile)" />
                  <FileConstant Condition="!$([System.IO.Path]::IsPathRooted('%(RelativeDir)')) OR '%(Link)' != ''">
                    <AreaPath Condition="!$([System.IO.Path]::IsPathRooted('%(RelativeDir)'))">%(RelativeDir)%(Filename)</AreaPath>
                    <FileExtension Condition="!$([System.IO.Path]::IsPathRooted('%(RelativeDir)'))">%(Extension)</FileExtension>
                  </FileConstant>
                  <FileConstant Condition="'%(AreaPath)' != ''">
                    <Area>$([MSBuild]::ValueOrDefault('%(AreaPath)', '').Replace('\', '.').Replace('/', '.'))</Area>
                    <Value>%(AreaPath)%(FileExtension)</Value>
                  </FileConstant>
                  <AdditionalFiles Include="@(FileConstant)" />
                </ItemGroup>
              </Target>
            </Project>
            """);

        var file = Assert.Single(ProjectParser.Load(path).AdditionalFiles, f => f.ItemTypes is not null);

        Assert.Equal("Scenarios/MultipleUses", file.Metadata.GetValueOrDefault("AreaPath")?.Replace('\\', '/'));
        Assert.Equal("Scenarios.MultipleUses", file.Metadata.GetValueOrDefault("Area"));
        Assert.Equal("Scenarios/MultipleUses.cs", file.Metadata.GetValueOrDefault("Value")?.Replace('\\', '/'));
    }

    [Fact]
    public void ThisAssembly_Constants_and_Project_get_the_constants_their_targets_make()
    {
        // Moq's static proxy tests use ThisAssembly.Constants.Scenarios.MultipleUses, a FileConstant its own target adds
        // from each Compile item under Scenarios/, and ThisAssembly.Project.MSBuildProjectDirectory, from a ProjectProperty
        // item: the packages' targets turn both into Constant items, which reach the generator as additional files without
        // a file. None did: 10 CS0117 on ThisAssembly.
        using var temp = new TempDirectory();
        temp.Write("Tests/Scenarios/MultipleUses.cs", "class MultipleUses { }");
        temp.Write("Tests/Other.cs", "class Other { }");
        var path = temp.Write("Tests/Tests.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
              <ItemGroup>
                <ProjectProperty Include="MSBuildProjectDirectory" />
                <ProjectProperty Include="CI" />
                <ProjectProperty Include="CI" />
              </ItemGroup>
              <Target Name="AddConstants" BeforeTargets="PrepareForBuild;CompileDesignTime;GenerateMSBuildEditorConfigFileShouldRun">
                <ItemGroup>
                  <FileConstant Include="@(Compile)" Condition="$([MSBuild]::ValueOrDefault('%(RelativeDir)', '').StartsWith('Scenarios'))" />
                </ItemGroup>
              </Target>
            </Project>
            """);
        var project = temp.Write("packages/thisassembly.project/build/ThisAssembly.Project.targets", """
            <Project>
              <Target Name="PrepareProjectProperties" BeforeTargets="PrepareConstants" DependsOnTargets="$(PrepareProjectPropertiesDependsOn)">
                <ItemGroup>
                  <ProjectProperty Include="$(ProjectProperty)" Condition="'$(ProjectProperty)' != ''" />
                  <ProjectPropertyDistinct Include="@(ProjectProperty -> Distinct())" />
                </ItemGroup>
                <ItemGroup>
                  <Constant Include="@(ProjectPropertyDistinct -> 'Project.%(Identity)')" Value="$(%(ProjectPropertyDistinct.Identity))" Comment="%(ProjectPropertyDistinct.Comment)" Root="." />
                </ItemGroup>
              </Target>
            </Project>
            """);
        var constants = temp.Write("packages/thisassembly.constants/build/ThisAssembly.Constants.targets", """
            <Project>
              <ItemGroup>
                <CompilerVisibleItemMetadata Include="Constant" MetadataName="ItemType" />
                <CompilerVisibleItemMetadata Include="Constant" MetadataName="Value" />
                <CompilerVisibleItemMetadata Include="Constant" MetadataName="Root" />
              </ItemGroup>
              <Target Name="PrepareConstants">
                <ItemGroup>
                  <FileConstant Condition="!$([System.IO.Path]::IsPathRooted('%(RelativeDir)')) OR '%(Link)' != ''">
                    <AreaPath Condition="!$([System.IO.Path]::IsPathRooted('%(RelativeDir)'))">%(RelativeDir)%(Filename)</AreaPath>
                    <FileExtension Condition="!$([System.IO.Path]::IsPathRooted('%(RelativeDir)'))">%(Extension)</FileExtension>
                  </FileConstant>
                  <FileConstant Condition="'%(AreaPath)' != ''">
                    <Area>$([MSBuild]::ValueOrDefault('%(AreaPath)', '').Replace('', '.').Replace('/', '.'))</Area>
                    <Value>%(AreaPath)%(FileExtension)</Value>
                  </FileConstant>
                  <Constant Include="@(FileConstant -> '%(Area)')" Condition="'%(FileConstant.Area)' != '' AND '%(FileConstant.Value)' != ''"
                            Value="%(FileConstant.Value)" Comment="%(FileConstant.Comment)" />
                </ItemGroup>
                <ItemGroup>
                  <Constant Update="@(Constant)">
                    <Value>$([MSBuild]::ValueOrDefault('%(Constant.Value)', '').Replace('&#xD;&#xA;', '`n').Replace('&#xA;', '`n'))</Value>
                  </Constant>
                </ItemGroup>
              </Target>
              <Target Name="_InjectConstantAdditionalFiles" BeforeTargets="GenerateMSBuildEditorConfigFileShouldRun" DependsOnTargets="PrepareResourceNames;PrepareConstants">
                <ItemGroup>
                  <Constant Update="@(Constant)" Condition="$([MSBuild]::ValueOrdefault('%(Constant.Value)', '').Contains(';'))">
                    <Value>|$([MSBuild]::ValueOrdefault('%(Constant.Value)', '').Replace(';', '|'))|</Value>
                  </Constant>
                  <AdditionalFiles Include="@(Constant)" />
                </ItemGroup>
              </Target>
              <!-- Not ThisAssembly's: shows the file constants in the message below when the test fails. -->
              <Target Name="ShowFileConstants" AfterTargets="_InjectConstantAdditionalFiles">
                <ItemGroup><AdditionalFiles Include="@(FileConstant)" /></ItemGroup>
              </Target>
            </Project>
            """);

        var result = ProjectParser.WithPackageBuildFiles(ProjectParser.Load(path), [project, constants]);

        var items = result.AdditionalFiles.Where(f => f.ItemTypes?.Contains("Constant") == true).ToDictionary(f => f.Include, StringComparer.Ordinal);
        var seen = "targets: " + string.Join(", ", result.CompilerTargets.Select(t => t.Attribute("Name")?.Value)) + "\n"
            + string.Join("\n", result.AdditionalFiles.Select(f => $"{f.Include} [{string.Join(",", f.ItemTypes ?? [])}] {string.Join("; ", f.Metadata.Select(m => m.Key + "=" + m.Value))}"));
        Assert.True(items.ContainsKey("Scenarios.MultipleUses"), seen);
        Assert.Equal(new[] { "Project.CI", "Project.MSBuildProjectDirectory", "Scenarios.MultipleUses" }, items.Keys.Order(StringComparer.Ordinal));
        Assert.All(items.Values, f => Assert.True(f.IsItemOnly));
        Assert.Equal("Scenarios/MultipleUses.cs", items["Scenarios.MultipleUses"].Metadata["Value"].Replace('\\', '/'));
        Assert.Equal(Path.GetDirectoryName(path), items["Project.MSBuildProjectDirectory"].Metadata["Value"]);
        Assert.Equal(".", items["Project.MSBuildProjectDirectory"].Metadata["Root"]);
        Assert.Contains(result.CompilerVisibleItemMetadata, m => m is { ItemType: "Constant", MetadataName: "ItemType" });
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
