using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using TabStudio.Compilation.Building;
using TabStudio.Compilation.Running;
using TabStudio.Core.Workspaces;
using TabStudio.Testing;

namespace TabStudio.Compilation.Tests;

/// <summary>Source generators from packages: Roslyn runs them for IntelliSense and builds alike.</summary>
public sealed class SourceGeneratorTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly RoslynWorkspaceHost _host = new();
    private readonly string _projectPath;
    private readonly string _programPath;

    public SourceGeneratorTests()
    {
        _projectPath = _temp.Write("App/App.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net10.0</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
                <RootNamespace>Tablet.App</RootNamespace>
              </PropertyGroup>
            </Project>
            """);
        _programPath = _temp.Write("App/Program.cs", "Console.WriteLine(Tablet.App.Generated.Hello);");
        _host.Load(WorkspaceLoader.Load(_temp.Resolve("App")), FileSystemTextProvider.Instance);
    }

    private Core.Workspaces.ProjectInfo Project => _host.FindProject(_projectPath)!;

    [Fact]
    public async Task A_package_generator_adds_code_that_the_program_uses_and_reads_the_project_properties()
    {
        var before = await new ProjectBuilder(_host).BuildAsync(Project);
        Assert.False(before.Success);

        _host.SetAnalyzers(_projectPath, [CompileGenerator("packages/greeting.generator/1.0.0/analyzers/dotnet/cs/Greeting.Generator.dll")]);
        var diagnostics = await _host.GetDocumentDiagnosticsAsync(_programPath);
        var build = await new ProjectBuilder(_host).BuildAsync(Project);

        Assert.DoesNotContain(diagnostics, d => d.Severity == Core.Diagnostics.DiagnosticSeverityLevel.Error);
        Assert.True(build.Success, string.Join('\n', build.Diagnostics));
        var console = new CapturingConsole();
        var run = await new ProgramRunner().RunAsync(build.EntryAssembly!, build.Dependencies, [], console);
        Assert.Equal(RunOutcome.Completed, run.Outcome);
        Assert.Equal("Hello from a generator in Tablet.App\n", console.Output);
        Assert.Single(_host.GetAnalyzerPaths(_projectPath));
    }

    [Fact]
    public async Task A_generator_that_uses_its_package_s_own_Microsoft_CodeAnalysis_assembly_runs()
    {
        // As Microsoft.CodeAnalysis.ResxSourceGenerator.CSharp.dll uses Microsoft.CodeAnalysis.ResxSourceGenerator.dll,
        // which only its package has: a Microsoft.CodeAnalysis name is not by itself one of the compiler's assemblies.
        const string folder = "packages/greeting.text/1.0.0/analyzers/dotnet/cs/";
        var library = Emit("Microsoft.CodeAnalysis.GreetingText", folder + "Microsoft.CodeAnalysis.GreetingText.dll",
            "namespace Microsoft.CodeAnalysis.GreetingText; public static class Text { public static string Hello => \"Hello from the package's own assembly\"; }", []);
        var generator = Emit("Greeting.Generator.CSharp", folder + "Greeting.Generator.CSharp.dll", """
            using Microsoft.CodeAnalysis;

            [Generator]
            public sealed class GreetingGenerator : IIncrementalGenerator
            {
                public void Initialize(IncrementalGeneratorInitializationContext context) =>
                    context.RegisterPostInitializationOutput(output => output.AddSource("Generated.g.cs",
                        "namespace Tablet.App;\npublic static class Generated { public static string Hello => \"" + Microsoft.CodeAnalysis.GreetingText.Text.Hello + "\"; }\n"));
            }
            """, [MetadataReference.CreateFromFile(library)]);

        _host.SetAnalyzers(_projectPath, [generator]);
        var build = await new ProjectBuilder(_host).BuildAsync(Project);

        Assert.True(build.Success, string.Join('\n', build.Diagnostics));
        var console = new CapturingConsole();
        await new ProgramRunner().RunAsync(build.EntryAssembly!, build.Dependencies, [], console);
        Assert.Equal("Hello from the package's own assembly\n", console.Output);
    }

    [Fact]
    public async Task Framework_generators_reach_every_project_and_survive_a_reload()
    {
        _host.SetFrameworkAnalyzers([CompileGenerator("packs/generators/Greeting.Generator.dll")]);
        _host.Load(WorkspaceLoader.Load(_temp.Resolve("App")), FileSystemTextProvider.Instance);

        var build = await new ProjectBuilder(_host).BuildAsync(Project);

        Assert.True(build.Success, string.Join('\n', build.Diagnostics));
    }

    /// <summary>
    /// What a package's props and targets give its generator, as the SDK hands it over: a property it sets by default
    /// and makes visible, a visible property nothing sets (present and empty), an additional file with visible
    /// metadata, a global using, and the interceptor namespace a target adds before the compiler runs.
    /// </summary>
    [Fact]
    public async Task A_package_s_props_and_targets_reach_its_generator_as_with_the_SDK()
    {
        var package = _temp.Resolve("packages/probe.generator/1.0.0");
        _temp.Write("packages/probe.generator/1.0.0/build/Probe.Generator.props", """
            <Project>
              <PropertyGroup>
                <ProbeMode Condition="'$(ProbeMode)' == ''">default</ProbeMode>
                <ProbeWindows>false</ProbeWindows>
                <ProbeWindows Condition="$([MSBuild]::IsTargetFrameworkCompatible('$(TargetFramework)', 'net8.0-windows'))">true</ProbeWindows>
              </PropertyGroup>
              <ItemGroup>
                <CompilerVisibleProperty Include="ProbeMode;ProbeWindows;ProbeUnset" />
                <CompilerVisibleItemMetadata Include="AdditionalFiles" MetadataName="ProbeKind" />
                <AdditionalFiles Include="$(MSBuildThisFileDirectory)probe.txt" ProbeKind="settings" />
                <Using Include="Probe.Runtime" />
              </ItemGroup>
            </Project>
            """);
        _temp.Write("packages/probe.generator/1.0.0/build/probe.txt", "from the package");
        _temp.Write("packages/probe.generator/1.0.0/buildTransitive/Probe.Generator.targets", """
            <Project>
              <PropertyGroup><_ProbeNamespace>Probe.Generated.Interceptors</_ProbeNamespace></PropertyGroup>
              <Target Name="ProbeInterceptors" BeforeTargets="CoreCompile" Condition="'$(Language)' == 'C#'">
                <PropertyGroup>
                  <InterceptorsNamespaces>$(InterceptorsNamespaces);$(_ProbeNamespace)</InterceptorsNamespaces>
                </PropertyGroup>
              </Target>
            </Project>
            """);
        _host.UpdateText(_programPath, "Console.WriteLine(Probe.Runtime.Seen.Text);");
        _host.SetAnalyzers(_projectPath, [CompileGenerator("packages/probe.generator/1.0.0/analyzers/dotnet/cs/Probe.Generator.dll", ProbeGenerator)]);

        _host.SetPackageBuildFiles(_projectPath, [Path.Combine(package, "build", "Probe.Generator.props"), Path.Combine(package, "buildTransitive", "Probe.Generator.targets")]);
        var build = await new ProjectBuilder(_host).BuildAsync(Project);

        Assert.True(build.Success, string.Join('\n', build.Diagnostics));
        var console = new CapturingConsole();
        var run = await new ProgramRunner().RunAsync(build.EntryAssembly!, build.Dependencies, [], console);
        Assert.Equal(RunOutcome.Completed, run.Outcome);
        Assert.Equal("mode=default windows=false unset=present files=probe.txt:settings:from the package\n", console.Output);
        Assert.Contains("Probe.Generated.Interceptors", Project.GetProperty("InterceptorsNamespaces"), StringComparison.Ordinal);
        Assert.Equal(2, _host.GetPackageBuildFiles(_projectPath).Count);

        // The project's own value wins over a package's props, and survives a reload of the workspace.
        _temp.Write("App/App.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net10.0</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
                <RootNamespace>Tablet.App</RootNamespace>
                <ProbeMode>mine</ProbeMode>
              </PropertyGroup>
            </Project>
            """);
        _host.Load(WorkspaceLoader.Load(_temp.Resolve("App")), FileSystemTextProvider.Instance);
        _host.UpdateText(_programPath, "Console.WriteLine(Probe.Runtime.Seen.Text);");
        var rebuilt = await new ProjectBuilder(_host).BuildAsync(Project);
        Assert.True(rebuilt.Success, string.Join('\n', rebuilt.Diagnostics));
        var again = new CapturingConsole();
        await new ProgramRunner().RunAsync(rebuilt.EntryAssembly!, rebuilt.Dependencies, [], again);
        Assert.StartsWith("mode=mine ", again.Output, StringComparison.Ordinal);
    }

    /// <summary>
    /// StronglyTypedId's test projects all list its templates as AdditionalFiles: each project's generator gets them,
    /// not only the first project's.
    /// </summary>
    [Fact]
    public async Task Projects_that_share_an_additional_file_each_get_it()
    {
        _temp.Write("Templates/probe.txt", "shared");
        foreach (var name in new[] { "First", "Second" })
        {
            _temp.Write($"{name}/{name}.csproj", """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings></PropertyGroup>
                  <ItemGroup><AdditionalFiles Include="$(MSBuildThisFileDirectory)..\Templates\*.txt" /></ItemGroup>
                </Project>
                """);
            _temp.Write($"{name}/Program.cs", "Console.WriteLine(Probe.Runtime.Seen.Text);");
        }

        var generator = CompileGenerator("packages/probe.generator/1.0.0/analyzers/dotnet/cs/Probe.Generator.dll", ProbeGenerator);
        _host.Load(WorkspaceLoader.Load(_temp.Path), FileSystemTextProvider.Instance);
        foreach (var name in new[] { "First", "Second" })
        {
            var path = _temp.Resolve($"{name}/{name}.csproj");
            _host.SetAnalyzers(path, [generator]);
            var build = await new ProjectBuilder(_host).BuildAsync(_host.FindProject(path)!);
            Assert.True(build.Success, string.Join('\n', build.Diagnostics));
            var console = new CapturingConsole();
            await new ProgramRunner().RunAsync(build.EntryAssembly!, build.Dependencies, [], console);
            Assert.EndsWith("files=probe.txt::shared\n", console.Output, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Sources that packages' targets put into <c>obj/</c> before the compiler runs, which TabStudio keeps in memory:
    /// TypeNameFormatter.Sources copies its source there and adds it outside a target (moq's <c>GetFormattedName</c>),
    /// IFluentInterface writes its interface there from a <c>.cs.pp</c> with the root namespace put in, from a target
    /// listed in <c>CoreCompileDependsOn</c> that depends on the one writing it, and Microsoft.Testing.Platform generates
    /// <c>SelfRegisteredExtensions</c> in the root namespace (Terminal.Gui's <c>using UnitTests.Parallelizable;</c>).
    /// </summary>
    [Fact]
    public async Task Sources_that_packages_targets_copy_or_write_before_the_compiler_are_compiled()
    {
        var formatter = _temp.Resolve("packages/typenameformatter.sources/1.0.1/build");
        _temp.Write("packages/typenameformatter.sources/1.0.1/build/TypeNameFormatter.cs", """
            namespace TypeNameFormatter
            {
            #if TYPENAMEFORMATTER_INTERNAL
                internal
            #else
                public
            #endif
                static class TypeName
                {
                    public static string GetFormattedName(this System.Type type) => type == typeof(int) ? "int" : type.Name;
                }
            }
            """);
        _temp.Write("packages/typenameformatter.sources/1.0.1/build/TypeNameFormatter.Sources.targets", """
            <Project TreatAsLocalProperty="IsCompatibleWithProject;SourceFolder">
              <PropertyGroup>
                <TypeNameFormatterInternal Condition="'$(TypeNameFormatterInternal)' == ''">True</TypeNameFormatterInternal>
              </PropertyGroup>
              <PropertyGroup>
                <IsCompatibleWithProject Condition="'$(Language)' == 'C#'">True</IsCompatibleWithProject>
                <SourceFolder>$(MSBuildThisFileDirectory)</SourceFolder>
              </PropertyGroup>
              <PropertyGroup Condition="'$(IsCompatibleWithProject)' == 'True'">
                <DefineConstants Condition="$(TypeNameFormatterInternal)">TYPENAMEFORMATTER_INTERNAL;$(DefineConstants)</DefineConstants>
              </PropertyGroup>
              <Target Name="_TypeNameFormatterCopy" Condition="'$(IsCompatibleWithProject)' == 'True'" AfterTargets="PrepareForBuild" BeforeTargets="CoreCompile"
                      Inputs="$(SourceFolder)TypeNameFormatter.cs" Outputs="$(BaseIntermediateOutputPath)TypeNameFormatter.cs">
                <Copy SourceFiles="$(SourceFolder)TypeNameFormatter.cs" DestinationFolder="$(BaseIntermediateOutputPath)" OverwriteReadOnlyFiles="true" />
              </Target>
              <ItemGroup Condition="'$(IsCompatibleWithProject)' == 'True'">
                <Compile Include="$(BaseIntermediateOutputPath)TypeNameFormatter.cs">
                  <AutoGen>true</AutoGen>
                </Compile>
              </ItemGroup>
            </Project>
            """);
        var fluent = _temp.Resolve("packages/ifluentinterface/2.1.0/build");
        _temp.Write("packages/ifluentinterface/2.1.0/build/IFluentInterface.cs.pp", """
            namespace $rootnamespace$
            {
                using global::System;
                using global::System.ComponentModel;

                /// <summary>Hides the members of object from IntelliSense; "quoted"; semicolons; and all.</summary>
                [EditorBrowsable(EditorBrowsableState.Never)]
                public interface IFluentInterface
                {
                    [EditorBrowsable(EditorBrowsableState.Never)]
                    Type GetType();
                }
            }
            """);
        _temp.Write("packages/ifluentinterface/2.1.0/build/IFluentInterface.targets", """
            <?xml version="1.0" encoding="utf-8"?>
            <Project ToolsVersion="4.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
              <PropertyGroup>
                <IFluentInterfaceInput>$(MSBuildThisFileDirectory)IFluentInterface$(DefaultLanguageSourceExtension).pp</IFluentInterfaceInput>
                <IFluentInterfaceOutput>$(IntermediateOutputPath)IFluentInterface.g$(DefaultLanguageSourceExtension)</IFluentInterfaceOutput>
                <IFluentInterfaceSupported Condition="'$(Language)' == 'C#' Or '$(Language)' == 'VB'">true</IFluentInterfaceSupported>
              </PropertyGroup>
              <PropertyGroup Condition="'$(IFluentInterfaceSupported)' == 'true'">
                <CoreCompileDependsOn>
                  IFluentInterface;
                  $(CoreCompileDependsOn)
                </CoreCompileDependsOn>
              </PropertyGroup>
              <Target Name="IFluentInterface" Condition="'$(IFluentInterfaceSupported)' == 'true'" DependsOnTargets="IFluentInterfaceGenerate">
                <ItemGroup>
                  <Compile Include="$(IFluentInterfaceOutput)" />
                </ItemGroup>
              </Target>
              <Target Name="IFluentInterfaceGenerate" Inputs="$(IFluentInterfaceInput)" Outputs="$(IFluentInterfaceOutput)">
                <PropertyGroup>
                  <_IFluentInterfaceContent>$([System.IO.File]::ReadAllText('$(IFluentInterfaceInput)'))</_IFluentInterfaceContent>
                  <_IFluentInterfaceContent>$(_IFluentInterfaceContent.Replace('$rootnamespace$', '$(RootNamespace)'))</_IFluentInterfaceContent>
                </PropertyGroup>
                <PropertyGroup>
                  <IFluentInterfaceDir>$([System.IO.Path]::GetDirectoryName('$(IFluentInterfaceOutput)'))</IFluentInterfaceDir>
                </PropertyGroup>
                <MakeDir Directories="$(IFluentInterfaceDir)" Condition=" !Exists('$(IFluentInterfaceDir)') " />
                <WriteLinesToFile File='$(IFluentInterfaceOutput)' Lines='$(_IFluentInterfaceContent)' Overwrite='true' />
              </Target>
            </Project>
            """);
        var testing = _temp.Resolve("packages/microsoft.testing.platform.msbuild/2.5.1");
        _temp.Write("packages/microsoft.testing.platform.msbuild/2.5.1/build/Microsoft.Testing.Platform.MSBuild.targets", """
            <Project>
              <Import Project="$(MSBuildThisFileDirectory)..\buildMultiTargeting\Microsoft.Testing.Platform.MSBuild.targets" />
            </Project>
            """);
        _temp.Write("packages/microsoft.testing.platform.msbuild/2.5.1/buildMultiTargeting/Microsoft.Testing.Platform.MSBuild.targets", """
            <Project>
              <Target Name="_CalculateGenerateSelfRegisteredExtensions">
                <PropertyGroup>
                  <GenerateSelfRegisteredExtensions Condition=" '$(GenerateSelfRegisteredExtensions)' == '' ">$(IsTestingPlatformApplication)</GenerateSelfRegisteredExtensions>
                </PropertyGroup>
              </Target>
              <PropertyGroup>
                <_SelfRegisteredExtensionsSourceName>SelfRegisteredExtensions$(DefaultLanguageSourceExtension)</_SelfRegisteredExtensionsSourceName>
                <_SelfRegisteredExtensionsSourcePath>$([System.IO.Path]::Combine($(IntermediateOutputPath),$(_SelfRegisteredExtensionsSourceName)))</_SelfRegisteredExtensionsSourcePath>
                <_SelfRegisteredExtensionsSourcePath>$([MSBuild]::NormalizePath($(MSBuildProjectDirectory), $(_SelfRegisteredExtensionsSourcePath)))</_SelfRegisteredExtensionsSourcePath>
              </PropertyGroup>
              <Target Name="_GenerateSelfRegisteredExtensions" Outputs="$(_SelfRegisteredExtensionsSourcePath)" Condition=" '$(GenerateSelfRegisteredExtensions)' == 'true' ">
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
        _temp.Write("packages/xunit.v3.core/4.0.2/buildTransitive/xunit.v3.core.props", """
            <Project>
              <PropertyGroup>
                <GenerateSelfRegisteredExtensions>true</GenerateSelfRegisteredExtensions>
                <IsTestingPlatformApplication Condition=" '$(IsTestingPlatformApplication)' == '' ">true</IsTestingPlatformApplication>
              </PropertyGroup>
            </Project>
            """);
        var platform = Emit("Microsoft.Testing.Platform", "packages/microsoft.testing.platform/2.5.1/lib/net9.0/Microsoft.Testing.Platform.dll",
            "namespace Microsoft.Testing.Platform.Builder { public interface ITestApplicationBuilder { } }", []);
        _host.UpdateText(_programPath, """
            using TypeNameFormatter;
            using Tablet.App;

            Console.WriteLine($"{typeof(int).GetFormattedName()} {typeof(IFluentInterface).FullName} {typeof(SelfRegisteredExtensions).FullName}");
            """);
        var before = await new ProjectBuilder(_host).BuildAsync(Project);

        _host.SetPackageReferences(_projectPath, [platform], []);
        _host.SetPackageBuildFiles(_projectPath, [
            _temp.Resolve("packages/xunit.v3.core/4.0.2/buildTransitive/xunit.v3.core.props"),
            Path.Combine(fluent, "IFluentInterface.targets"),
            Path.Combine(testing, "build", "Microsoft.Testing.Platform.MSBuild.targets"),
            Path.Combine(formatter, "TypeNameFormatter.Sources.targets")]);
        var build = await new ProjectBuilder(_host).BuildAsync(Project);

        Assert.False(before.Success);
        Assert.True(build.Success, string.Join('\n', build.Diagnostics));
        var console = new CapturingConsole();
        var run = await new ProgramRunner().RunAsync(build.EntryAssembly!, build.Dependencies, [], console);
        Assert.Equal(RunOutcome.Completed, run.Outcome);
        Assert.Equal("int Tablet.App.IFluentInterface Tablet.App.SelfRegisteredExtensions\n", console.Output);
        Assert.False(File.Exists(_temp.Resolve("App/obj/TypeNameFormatter.cs")));
    }

    /// <summary>
    /// A project's <c>LangVersion</c> reaches its generators: ReactiveUI's writes a backing field for a <c>[Reactive]</c>
    /// partial property below C# 14 and uses <c>field</c> from C# 14 on, and Dock, at C# 13, assigns the field.
    /// </summary>
    [Fact]
    public async Task A_generator_sees_the_C_sharp_version_the_project_asks_for()
    {
        _temp.Write("App/App.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net10.0</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
                <RootNamespace>Tablet.App</RootNamespace>
                <LangVersion>13</LangVersion>
              </PropertyGroup>
            </Project>
            """);
        _temp.Write("App/Counter.cs", """
            namespace Tablet.App;

            public partial class Counter
            {
                public Counter() => _value = 42;

                public partial int Value { get; set; }
            }
            """);
        _host.Load(WorkspaceLoader.Load(_temp.Resolve("App")), FileSystemTextProvider.Instance);
        _host.UpdateText(_programPath, "Console.WriteLine(new Tablet.App.Counter().Value);");
        var generator = Emit("Counter.Generator", "packages/counter.generator/1.0.0/analyzers/dotnet/cs/Counter.Generator.dll", """
            using Microsoft.CodeAnalysis;
            using Microsoft.CodeAnalysis.CSharp;

            [Generator]
            public sealed class CounterGenerator : IIncrementalGenerator
            {
                public void Initialize(IncrementalGeneratorInitializationContext context) =>
                    context.RegisterSourceOutput(context.CompilationProvider, static (output, compilation) =>
                        output.AddSource("Counter.g.cs", compilation is CSharpCompilation { LanguageVersion: > LanguageVersion.CSharp13 }
                            ? "namespace Tablet.App;\npublic partial class Counter { public partial int Value { get => field; set => field = value; } }\n"
                            : "namespace Tablet.App;\npublic partial class Counter { private int _value; public partial int Value { get => _value; set => _value = value; } }\n"));
            }
            """, [MetadataReference.CreateFromFile(typeof(CSharpCompilation).Assembly.Location)]);

        _host.SetAnalyzers(_projectPath, [generator]);
        var build = await new ProjectBuilder(_host).BuildAsync(Project);

        Assert.True(build.Success, string.Join('\n', build.Diagnostics));
        var console = new CapturingConsole();
        await new ProgramRunner().RunAsync(build.EntryAssembly!, build.Dependencies, [], console);
        Assert.Equal("42\n", console.Output);
    }

    [Theory]
    [InlineData(null, LanguageVersion.Latest)]
    [InlineData("latest", LanguageVersion.Latest)]
    [InlineData("default", LanguageVersion.Latest)]
    [InlineData("preview", LanguageVersion.Preview)]
    [InlineData("13", LanguageVersion.CSharp13)]
    [InlineData("14.0", LanguageVersion.CSharp14)]
    [InlineData("7.3", CSharpCompiler.MinimumLanguageVersion)]
    public void A_project_compiles_with_the_C_sharp_version_its_LangVersion_asks_for(string? langVersion, LanguageVersion expected)
    {
        var property = langVersion is null ? string.Empty : $"<LangVersion>{langVersion}</LangVersion>";
        var project = ProjectParser.Parse(_projectPath, $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework>{property}</PropertyGroup>
            </Project>
            """);

        Assert.Equal(expected, CSharpCompiler.LanguageVersionOf(project));
    }

    /// <summary>A generator that reports what the analyzer configuration and additional files gave it.</summary>
    private const string ProbeGenerator = """
        using System.Linq;
        using Microsoft.CodeAnalysis;

        [Generator]
        public sealed class ProbeGenerator : IIncrementalGenerator
        {
            public void Initialize(IncrementalGeneratorInitializationContext context)
            {
                var files = context.AdditionalTextsProvider
                    .Combine(context.AnalyzerConfigOptionsProvider)
                    .Select(static (pair, cancellationToken) =>
                    {
                        pair.Right.GetOptions(pair.Left).TryGetValue("build_metadata.AdditionalFiles.ProbeKind", out var kind);
                        return System.IO.Path.GetFileName(pair.Left.Path) + ":" + kind + ":" + pair.Left.GetText(cancellationToken);
                    })
                    .Collect();
                context.RegisterSourceOutput(context.AnalyzerConfigOptionsProvider.Combine(files), static (output, input) =>
                {
                    var options = input.Left.GlobalOptions;
                    options.TryGetValue("build_property.ProbeMode", out var mode);
                    options.TryGetValue("build_property.ProbeWindows", out var windows);
                    var unset = options.TryGetValue("build_property.ProbeUnset", out var value) && value.Length == 0 ? "present" : "missing";
                    var text = "mode=" + mode + " windows=" + windows + " unset=" + unset + " files=" + string.Join(",", input.Right.Where(f => f.StartsWith("probe.txt")));
                    var literal = "\"" + text.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
                    output.AddSource("Seen.g.cs", "// <auto-generated/>\nnamespace Probe.Runtime { public static class Seen { public const string Text = " + literal + "; } }\n");
                });
            }
        }
        """;

    public void Dispose()
    {
        _host.Dispose();
        _temp.Dispose();
    }

    /// <summary>Compiles an analyzer assembly against the compiler, as a package would ship it, and returns its path.</summary>
    private string Emit(string name, string relativePath, string source, IEnumerable<MetadataReference> references)
    {
        var compilation = CSharpCompilation.Create(
            name,
            [CSharpSyntaxTree.ParseText(source)],
            [.. Basic.Reference.Assemblies.Net100.References.All, MetadataReference.CreateFromFile(typeof(IIncrementalGenerator).Assembly.Location), .. references],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var path = _temp.Resolve(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var result = compilation.Emit(path);
        Assert.True(result.Success, string.Join('\n', result.Diagnostics));
        return path;
    }

    /// <summary>Compiles a small incremental generator, as a package would ship it, and returns its path.</summary>
    private string CompileGenerator(string relativePath, string? generator = null)
    {
        const string source = """
            using Microsoft.CodeAnalysis;

            [Generator]
            public sealed class GreetingGenerator : IIncrementalGenerator
            {
                public void Initialize(IncrementalGeneratorInitializationContext context) =>
                    context.RegisterSourceOutput(context.AnalyzerConfigOptionsProvider, static (output, options) =>
                    {
                        options.GlobalOptions.TryGetValue("build_property.RootNamespace", out var name);
                        output.AddSource("Generated.g.cs",
                            "// <auto-generated/>\nnamespace " + name + ";\npublic static class Generated { public static string Hello => \"Hello from a generator in " + name + "\"; }\n");
                    });
            }
            """;
        var compilation = CSharpCompilation.Create(
            "Greeting.Generator",
            [CSharpSyntaxTree.ParseText(generator ?? source)],
            [.. Basic.Reference.Assemblies.Net100.References.All, MetadataReference.CreateFromFile(typeof(IIncrementalGenerator).Assembly.Location)],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var path = _temp.Resolve(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var result = compilation.Emit(path);
        Assert.True(result.Success, string.Join('\n', result.Diagnostics));
        return path;
    }
}
