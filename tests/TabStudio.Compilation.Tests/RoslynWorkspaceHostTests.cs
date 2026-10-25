using Microsoft.CodeAnalysis.Classification;
using TabStudio.Compilation.Building;
using TabStudio.Compilation.Running;
using TabStudio.Core.Diagnostics;
using TabStudio.Core.Workspaces;
using TabStudio.Testing;

namespace TabStudio.Compilation.Tests;

public sealed class RoslynWorkspaceHostTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly RoslynWorkspaceHost _host = new();

    public RoslynWorkspaceHostTests()
    {
        _temp.Write("Shop.slnx", """
            <Solution>
              <Project Path="App/App.csproj" />
              <Project Path="Lib/Lib.csproj" />
            </Solution>
            """);
        _temp.Write("App/App.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net10.0</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
              </PropertyGroup>
              <ItemGroup><ProjectReference Include="..\Lib\Lib.csproj" /></ItemGroup>
            </Project>
            """);
        _temp.Write("App/Program.cs", """
            using Lib;
            Console.WriteLine(Greeter.Greet("tablet"));
            """);
        _temp.Write("Lib/Lib.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
            </Project>
            """);
        _temp.Write("Lib/Greeter.cs", """
            namespace Lib;
            public static class Greeter
            {
                public static string Greet(string name) => $"Hello, {name}";
            }
            """);

        _host.Load(WorkspaceLoader.Load(_temp.Path), FileSystemTextProvider.Instance);
    }

    private string ProgramPath => _temp.Resolve("App/Program.cs");

    [Fact]
    public void Loads_every_csharp_source_file()
    {
        Assert.True(_host.ContainsDocument(ProgramPath));
        Assert.True(_host.ContainsDocument(_temp.Resolve("Lib/Greeter.cs")));
        Assert.False(_host.ContainsDocument(_temp.Resolve("App/App.csproj")));
    }

    [Fact]
    public async Task A_clean_file_has_no_errors()
    {
        var diagnostics = await _host.GetDocumentDiagnosticsAsync(ProgramPath);

        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverityLevel.Error);
    }

    [Fact]
    public async Task Edits_show_up_in_live_diagnostics()
    {
        Assert.True(_host.UpdateText(ProgramPath, "Console.WriteLine(Greeter.Gret(\"x\"));"));

        var diagnostics = await _host.GetDocumentDiagnosticsAsync(ProgramPath);

        Assert.Contains(diagnostics, d => d.Severity == DiagnosticSeverityLevel.Error && d.Location?.FilePath == ProgramPath);
    }

    [Fact]
    public async Task Live_diagnostics_include_the_in_process_safety_check()
    {
        _host.UpdateText(ProgramPath, "Environment.Exit(2);");

        var diagnostics = await _host.GetDocumentDiagnosticsAsync(ProgramPath);

        Assert.Contains(diagnostics, d => d.Id == InProcessSafetyAnalyzer.ExitId);
    }

    [Fact]
    public async Task Classifies_keywords_types_and_strings()
    {
        var text = await File.ReadAllTextAsync(_temp.Resolve("Lib/Greeter.cs"));

        var spans = await _host.GetClassificationsAsync(_temp.Resolve("Lib/Greeter.cs"), 0, text.Length);

        Assert.Contains(spans, s => s.Classification == ClassificationTypeNames.Keyword && text.Substring(s.Start, s.Length) == "public");
        Assert.Contains(spans, s => s.Classification == ClassificationTypeNames.ClassName && text.Substring(s.Start, s.Length) == "Greeter");
        Assert.Contains(spans, s => s.Classification == ClassificationTypeNames.MethodName && text.Substring(s.Start, s.Length) == "Greet");
    }

    [Fact]
    public void Build_order_puts_dependencies_first()
    {
        var order = _host.GetBuildOrder(_temp.Resolve("App/App.csproj"));

        Assert.Equal(new[] { "Lib", "App" }, order.Select(p => p.Name));
    }

    [Fact]
    public async Task Builds_and_runs_a_project_with_a_project_reference()
    {
        var app = _host.FindProject(_temp.Resolve("App/App.csproj"))!;
        var build = await new ProjectBuilder(_host).BuildAsync(app);

        Assert.True(build.Success, string.Join('\n', build.Diagnostics));
        Assert.Equal("Lib", Assert.Single(build.Dependencies).Name);

        var console = new CapturingConsole();
        var run = await new ProgramRunner().RunAsync(build.EntryAssembly!, build.Dependencies, [], console);

        Assert.Equal(RunOutcome.Completed, run.Outcome);
        Assert.Equal("Hello, tablet\n", console.Output);
    }

    [Fact]
    public async Task A_new_file_joins_the_project_that_contains_it()
    {
        var path = _temp.Write("Lib/Farewell.cs", "namespace Lib; public static class Farewell { public static string Bye() => \"bye\"; }");
        Assert.True(_host.AddDocument(path, await File.ReadAllTextAsync(path)));
        _host.UpdateText(ProgramPath, "using Lib; Console.WriteLine(Farewell.Bye());");

        var diagnostics = await _host.GetDocumentDiagnosticsAsync(ProgramPath);

        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverityLevel.Error);
    }

    public void Dispose()
    {
        _host.Dispose();
        _temp.Dispose();
    }
}
