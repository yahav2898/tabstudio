using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using TabStudio.Compilation.Building;
using TabStudio.Compilation.Running;
using TabStudio.Core.Diagnostics;
using TabStudio.Core.Workspaces;
using TabStudio.Testing;

namespace TabStudio.Compilation.Tests;

/// <summary>
/// Source generators built from the repository, as Mediator, Terminal.Gui and StronglyTypedId reference theirs: a
/// project of the solution referenced with <c>OutputItemType="Analyzer"</c> is compiled and run inside the compilation
/// of the project that references it. Also, a generator that .NET and a package both bring runs once.
/// </summary>
public sealed class ProjectGeneratorTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly RoslynWorkspaceHost _host = new();

    /// <summary>A generator, as a repository keeps one: it names the assembly it generates into.</summary>
    private const string HelloGenerator = """
        using Microsoft.CodeAnalysis;

        [Generator]
        public sealed class HelloGenerator : IIncrementalGenerator
        {
            public void Initialize(IncrementalGeneratorInitializationContext context) =>
                context.RegisterSourceOutput(context.CompilationProvider, static (output, compilation) =>
                    output.AddSource("Hello.g.cs", "namespace Generated { public static class Hello { public const string Text = \"GREETING " + compilation.AssemblyName + "\"; } }"));
        }
        """;

    [Fact]
    public async Task A_generator_project_referenced_as_an_analyzer_runs_for_IntelliSense_builds_and_runs_and_again_after_an_edit()
    {
        var generatorSource = _temp.Write("Gen/HelloGenerator.cs", HelloGenerator.Replace("GREETING", "Hello from", StringComparison.Ordinal));
        _temp.Write("Gen/Gen.csproj", GeneratorProject());
        _temp.Write("App/App.csproj", App("""<ProjectReference Include="..\Gen\Gen.csproj" OutputItemType="Analyzer" ReferenceOutputAssembly="false" />"""));
        var program = _temp.Write("App/Program.cs", "System.Console.WriteLine(Generated.Hello.Text);");
        Load("App/App.csproj", "Gen/Gen.csproj");

        var diagnostics = await _host.GetDocumentDiagnosticsAsync(program);
        var build = await BuildAsync("App/App.csproj");

        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverityLevel.Error);
        Assert.True(build.Success, string.Join('\n', build.Diagnostics));
        Assert.Empty(Problems(build));

        // The generator runs inside the compiler; it is not part of the program.
        Assert.Empty(build.Dependencies);
        Assert.Equal("Hello from App\n", await RunAsync(build));

        // An edit to the generator is what the next build runs.
        _host.UpdateText(generatorSource, HelloGenerator.Replace("GREETING", "Edited for", StringComparison.Ordinal));
        var rebuilt = await BuildAsync("App/App.csproj");
        Assert.True(rebuilt.Success, string.Join('\n', rebuilt.Diagnostics));
        Assert.Equal("Edited for App\n", await RunAsync(rebuilt));
    }

    [Fact]
    public async Task An_analyzer_reference_that_keeps_ReferenceOutputAssembly_is_also_compiled_against_as_with_the_SDK()
    {
        // StronglyTypedId's integration tests reference its attributes project both ways.
        _temp.Write("Gen/HelloGenerator.cs", HelloGenerator.Replace("GREETING", "Hello from", StringComparison.Ordinal));
        _temp.Write("Gen/Gen.csproj", GeneratorProject());
        _temp.Write("Attributes/Attributes.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>netstandard2.0</TargetFramework></PropertyGroup>
            </Project>
            """);
        _temp.Write("Attributes/IdAttribute.cs", "namespace Ids { [System.AttributeUsage(System.AttributeTargets.Struct)] public sealed class IdAttribute : System.Attribute { } }");
        _temp.Write("App/App.csproj", App("""
            <ProjectReference Include="..\Gen\Gen.csproj" OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
            <ProjectReference Include="..\Attributes\Attributes.csproj" OutputItemType="Analyzer" />
            """));
        _temp.Write("App/Program.cs", """
            System.Console.WriteLine(System.Attribute.IsDefined(typeof(OrderId), typeof(Ids.IdAttribute)) + " " + Generated.Hello.Text);

            [Ids.Id]
            internal partial struct OrderId { }
            """);
        Load("App/App.csproj", "Gen/Gen.csproj", "Attributes/Attributes.csproj");

        var build = await BuildAsync("App/App.csproj");

        Assert.True(build.Success, string.Join('\n', build.Diagnostics));
        Assert.Empty(Problems(build));
        Assert.Equal("Attributes", Assert.Single(build.Dependencies).Name);
        Assert.Equal("True Hello from App\n", await RunAsync(build));
    }

    [Fact]
    public async Task A_generator_project_that_does_not_compile_is_one_warning_that_names_it()
    {
        // CS0535: the generator does not implement Initialize.
        _temp.Write("Gen/HelloGenerator.cs", "[Microsoft.CodeAnalysis.Generator] public sealed class HelloGenerator : Microsoft.CodeAnalysis.IIncrementalGenerator { }");
        _temp.Write("Gen/Gen.csproj", GeneratorProject());
        _temp.Write("App/App.csproj", App("""<ProjectReference Include="..\Gen\Gen.csproj" OutputItemType="Analyzer" ReferenceOutputAssembly="false" />"""));
        _temp.Write("App/Program.cs", "System.Console.WriteLine(\"no generated code needed\");");
        Load("App/App.csproj", "Gen/Gen.csproj");

        var build = await BuildAsync("App/App.csproj");
        var projectDiagnostics = await _host.GetProjectDiagnosticsAsync(_temp.Resolve("App/App.csproj"));

        Assert.True(build.Success, string.Join('\n', build.Diagnostics));
        var warning = Assert.Single(Problems(build));
        Assert.Equal(ProjectBuilder.GeneratorProjectFailedId, warning.Id);
        Assert.Equal(DiagnosticSeverityLevel.Warning, warning.Severity);
        Assert.Equal("App", warning.ProjectName);
        Assert.Contains("project Gen did not run: it does not compile", warning.Message, StringComparison.Ordinal);
        Assert.Single(projectDiagnostics, d => d.Id == ProjectBuilder.GeneratorProjectFailedId);
    }

    [Fact]
    public async Task A_generator_project_that_references_the_project_using_it_is_one_warning_not_a_hang()
    {
        _temp.Write("Gen/HelloGenerator.cs", HelloGenerator.Replace("GREETING", "Hello from", StringComparison.Ordinal));
        _temp.Write("Gen/Gen.csproj", GeneratorProject("""<ItemGroup><ProjectReference Include="..\App\App.csproj" /></ItemGroup>"""));
        _temp.Write("App/App.csproj", App("""<ProjectReference Include="..\Gen\Gen.csproj" OutputItemType="Analyzer" ReferenceOutputAssembly="false" />""", exe: false));
        _temp.Write("App/Library.cs", "namespace App { public static class Library { } }");
        Load("App/App.csproj", "Gen/Gen.csproj");

        var build = await BuildAsync("App/App.csproj");

        Assert.True(build.Success, string.Join('\n', build.Diagnostics));
        var warning = Assert.Single(Problems(build));
        Assert.Equal(ProjectBuilder.GeneratorProjectFailedId, warning.Id);
        Assert.Contains("it references App", warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_generator_that_the_framework_and_a_package_both_bring_runs_once_the_higher_version()
    {
        // ASP.NET Core and the Microsoft.Extensions.Logging.Abstractions package both ship the logging generator.
        var framework = CompileVersionedGenerator("packs/Microsoft.AspNetCore.App.Ref/analyzers/dotnet/cs", "1.0.0.0");
        var package = CompileVersionedGenerator("packages/versioned.generator/2.0.0/analyzers/dotnet/cs", "2.0.0.0");
        _temp.Write("App/App.csproj", App(string.Empty));
        _temp.Write("App/Program.cs", "System.Console.WriteLine(Generated.Versioned.Text);");
        Load("App/App.csproj");
        var app = _temp.Resolve("App/App.csproj");

        _host.SetFrameworkAnalyzers([framework]);
        _host.SetAnalyzers(app, [package, framework]);
        var build = await BuildAsync("App/App.csproj");

        Assert.True(build.Success, string.Join('\n', build.Diagnostics));
        Assert.Equal(package, Assert.Single(_host.GetAnalyzerPaths(app)));
        Assert.Equal("generator 2.0.0.0\n", await RunAsync(build));
    }

    public void Dispose()
    {
        _host.Dispose();
        _temp.Dispose();
    }

    /// <summary>A generator project as repositories write one: a .NET Standard library on Microsoft.CodeAnalysis.</summary>
    private static string GeneratorProject(string extra = "") => $"""
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>netstandard2.0</TargetFramework>
            <IsRoslynComponent>true</IsRoslynComponent>
          </PropertyGroup>
          <ItemGroup><PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="4.14.0" PrivateAssets="all" /></ItemGroup>
          {extra}
        </Project>
        """;

    private static string App(string references, bool exe = true) => $"""
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            {(exe ? "<OutputType>Exe</OutputType>" : string.Empty)}
            <TargetFramework>net10.0</TargetFramework>
          </PropertyGroup>
          <ItemGroup>{references}</ItemGroup>
        </Project>
        """;

    /// <summary>
    /// Loads the projects as one solution, the first being the one that is opened, and gives each generator project the
    /// Roslyn its package reference would restore.
    /// </summary>
    private void Load(params string[] projects)
    {
        var solution = _temp.Write("All.slnx", "<Solution>" + string.Concat(projects.Select(p => $"<Project Path=\"{p}\" />")) + "</Solution>");
        _host.Load(WorkspaceLoader.Load(solution), FileSystemTextProvider.Instance);
        foreach (var project in projects.Where(p => p.StartsWith("Gen/", StringComparison.Ordinal)))
        {
            _host.SetPackageReferences(_temp.Resolve(project), [typeof(IIncrementalGenerator).Assembly.Location]);
        }
    }

    /// <summary>The warnings and errors of a build.</summary>
    private static List<BuildDiagnostic> Problems(BuildResult build) =>
        [.. build.Diagnostics.Where(d => d.Severity >= DiagnosticSeverityLevel.Warning)];

    private Task<BuildResult> BuildAsync(string project) =>
        new ProjectBuilder(_host).BuildAsync(_host.FindProject(_temp.Resolve(project))!);

    private static async Task<string> RunAsync(BuildResult build)
    {
        var console = new CapturingConsole();
        var run = await new ProgramRunner().RunAsync(build.EntryAssembly!, build.Dependencies, [], console);
        Assert.Equal(RunOutcome.Completed, run.Outcome);
        return console.Output;
    }

    /// <summary>Compiles a generator with an assembly version, which the code it generates says, and returns its path.</summary>
    private string CompileVersionedGenerator(string folder, string version)
    {
        var source = $$"""
            using Microsoft.CodeAnalysis;

            [assembly: System.Reflection.AssemblyVersion("{{version}}")]

            [Generator]
            public sealed class VersionedGenerator : IIncrementalGenerator
            {
                public void Initialize(IncrementalGeneratorInitializationContext context) =>
                    context.RegisterPostInitializationOutput(static output =>
                        output.AddSource("Versioned.g.cs", "namespace Generated { public static class Versioned { public const string Text = \"generator {{version}}\"; } }"));
            }
            """;
        var compilation = CSharpCompilation.Create(
            "Versioned.Generator",
            [CSharpSyntaxTree.ParseText(source)],
            [.. Basic.Reference.Assemblies.Net100.References.All, MetadataReference.CreateFromFile(typeof(IIncrementalGenerator).Assembly.Location)],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var path = Path.Combine(_temp.Resolve(folder), "Versioned.Generator.dll");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var result = compilation.Emit(path);
        Assert.True(result.Success, string.Join('\n', result.Diagnostics));
        return path;
    }
}
