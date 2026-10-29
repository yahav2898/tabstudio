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
