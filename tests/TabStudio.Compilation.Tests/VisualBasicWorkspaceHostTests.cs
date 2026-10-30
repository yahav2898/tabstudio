using Microsoft.CodeAnalysis.Classification;
using TabStudio.Compilation.Building;
using TabStudio.Compilation.Running;
using TabStudio.Core.Diagnostics;
using TabStudio.Core.Workspaces;
using TabStudio.Testing;

namespace TabStudio.Compilation.Tests;

/// <summary>Visual Basic projects in the Roslyn workspace: loading, live diagnostics, options, coloring, formatting and running.</summary>
public sealed class VisualBasicWorkspaceHostTests : IDisposable
{
    private const string Program = """
        Imports Greetings

        Module Program
            Sub Main()
                Console.WriteLine(Greeter.Greet("tablet"))
            End Sub
        End Module
        """;

    private const string Greeter = """
        Public Module Greeter
            Public Function Greet(name As String) As String
                Return "Hello, " & name
            End Function
        End Module
        """;

    private readonly TempDirectory _temp = new();
    private readonly RoslynWorkspaceHost _host = new();

    public VisualBasicWorkspaceHostTests()
    {
        _temp.Write("App/App.vbproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net10.0</TargetFramework>
                <RootNamespace>App</RootNamespace>
              </PropertyGroup>
              <ItemGroup><ProjectReference Include="..\Greetings\Greetings.vbproj" /></ItemGroup>
            </Project>
            """);
        _temp.Write("App/Program.vb", Program);
        _temp.Write("App/Helpers/Strings.vb", """
            Friend Module Strings
                Public Function Shout(text As String) As String
                    Return text.ToUpperInvariant()
                End Function
            End Module
            """);
        _temp.Write("Greetings/Greetings.vbproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
            </Project>
            """);
        _temp.Write("Greetings/Greeter.vb", Greeter);

        _host.Load(WorkspaceLoader.Load(_temp.Path), FileSystemTextProvider.Instance);
    }

    private string ProgramPath => _temp.Resolve("App/Program.vb");

    private string GreeterPath => _temp.Resolve("Greetings/Greeter.vb");

    [Fact]
    public void Loads_every_visual_basic_source_file()
    {
        Assert.True(_host.ContainsDocument(ProgramPath));
        Assert.True(_host.ContainsDocument(_temp.Resolve("App/Helpers/Strings.vb")));
        Assert.True(_host.ContainsDocument(GreeterPath));
        Assert.False(_host.ContainsDocument(_temp.Resolve("App/App.vbproj")));
        Assert.Equal(new[] { "Greetings", "App" }, _host.GetBuildOrder(_temp.Resolve("App/App.vbproj")).Select(p => p.Name));
    }

    [Fact]
    public async Task A_clean_file_has_no_errors()
    {
        var diagnostics = await _host.GetDocumentDiagnosticsAsync(ProgramPath);

        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverityLevel.Error);
    }

    [Fact]
    public async Task Compiler_errors_are_reported_on_their_line()
    {
        _host.UpdateText(ProgramPath, """
            Module Program
                Sub Main()
                    Console.WriteLine(missing)
                End Sub
            End Module
            """);

        var diagnostics = await _host.GetDocumentDiagnosticsAsync(ProgramPath);

        var error = Assert.Single(diagnostics, d => d.Id == "BC30451");
        Assert.Equal(DiagnosticSeverityLevel.Error, error.Severity);
        Assert.Equal(ProgramPath, error.Location?.FilePath);
        Assert.Equal(3, error.Location?.Line);
        Assert.Equal(27, error.Location?.Column);
    }

    [Fact]
    public async Task Project_diagnostics_cover_every_file()
    {
        _host.UpdateText(GreeterPath, Greeter.Replace("& name", "& nam", StringComparison.Ordinal));

        var diagnostics = await _host.GetProjectDiagnosticsAsync(_temp.Resolve("Greetings/Greetings.vbproj"));

        Assert.Contains(diagnostics, d => d.Id == "BC30451" && d.Location is { Line: 3 } location && location.FilePath == GreeterPath);
    }

    [Theory]
    [InlineData("End")]
    [InlineData("Environment.Exit(2)")]
    public async Task Live_diagnostics_include_the_in_process_safety_check(string statement)
    {
        _host.UpdateText(ProgramPath, $"""
            Module Program
                Sub Main()
                    {statement}
                End Sub
            End Module
            """);

        var diagnostics = await _host.GetDocumentDiagnosticsAsync(ProgramPath);

        var exit = Assert.Single(diagnostics, d => d.Id == InProcessSafetyAnalyzer.ExitId);
        Assert.Equal(3, exit.Location?.Line);

        // The advice can be followed in a click handler too, where there is no Main to return from.
        Assert.Contains("Application.Exit()", exit.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Option_strict_from_the_project_file_reports_implicit_narrowing()
    {
        const string Narrowing = """
            Module Program
                Sub Main()
                    Dim d As Double = 1.5
                    Dim i As Integer = d
                    Console.WriteLine(i)
                End Sub
            End Module
            """;
        using var strict = SingleProject.Load("<OptionStrict>On</OptionStrict>", string.Empty, Narrowing);
        using var lax = SingleProject.Load(string.Empty, string.Empty, Narrowing);

        var strictDiagnostics = await strict.Host.GetDocumentDiagnosticsAsync(strict.ProgramPath);
        var laxDiagnostics = await lax.Host.GetDocumentDiagnosticsAsync(lax.ProgramPath);

        var error = Assert.Single(strictDiagnostics, d => d.Id == "BC30512");
        Assert.Equal(4, error.Location?.Line);
        Assert.DoesNotContain(laxDiagnostics, d => d.Severity == DiagnosticSeverityLevel.Error);
    }

    [Fact]
    public async Task Import_items_of_the_project_file_import_namespaces_into_every_file()
    {
        const string UsesStringBuilder = """
            Module Program
                Sub Main()
                    Dim builder As New StringBuilder()
                    Console.WriteLine(builder.Append("x").ToString())
                End Sub
            End Module
            """;
        using var imported = SingleProject.Load(string.Empty, """<Import Include="System.Text" />""", UsesStringBuilder);
        using var plain = SingleProject.Load(string.Empty, string.Empty, UsesStringBuilder);

        var importedDiagnostics = await imported.Host.GetDocumentDiagnosticsAsync(imported.ProgramPath);
        var plainDiagnostics = await plain.Host.GetDocumentDiagnosticsAsync(plain.ProgramPath);

        Assert.DoesNotContain(importedDiagnostics, d => d.Severity == DiagnosticSeverityLevel.Error);
        Assert.Contains(plainDiagnostics, d => d.Id == "BC30002");
    }

    [Fact]
    public async Task The_root_namespace_prefixes_every_namespace()
    {
        using var project = SingleProject.Load("<RootNamespace>Shop.Core</RootNamespace>", string.Empty, """
            Namespace Orders
                Public Class Order
                End Class
            End Namespace

            Module Program
                Sub Main()
                    Dim order As New Global.Shop.Core.Orders.Order()
                    Console.WriteLine(GetType(Orders.Order).FullName)
                End Sub
            End Module
            """);

        var diagnostics = await project.Host.GetDocumentDiagnosticsAsync(project.ProgramPath);
        project.Host.UpdateText(project.ProgramPath, """
            Namespace Orders
                Public Class Order
                End Class
            End Namespace

            Module Program
                Sub Main()
                    Dim order As New Global.Orders.Order()
                End Sub
            End Module
            """);
        var unprefixed = await project.Host.GetDocumentDiagnosticsAsync(project.ProgramPath);

        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverityLevel.Error);
        Assert.Contains(unprefixed, d => d.Severity == DiagnosticSeverityLevel.Error);
    }

    [Fact]
    public async Task Classifies_keywords_names_and_strings()
    {
        var spans = await _host.GetClassificationsAsync(GreeterPath, 0, Greeter.Length);

        // The first occurrence of a token, or of the token after some text.
        string? ClassOf(string token, string before = "")
        {
            var start = Greeter.IndexOf(before + token, StringComparison.Ordinal) + before.Length;
            return spans.FirstOrDefault(s => s.Start == start && s.Length == token.Length).Classification;
        }

        Assert.Equal(ClassificationTypeNames.Keyword, ClassOf("Public"));
        Assert.Equal(ClassificationTypeNames.Keyword, ClassOf("Function"));
        Assert.Equal(ClassificationTypeNames.ModuleName, ClassOf("Greeter"));
        Assert.Equal(ClassificationTypeNames.MethodName, ClassOf("Greet", before: "Function "));
        Assert.Equal(ClassificationTypeNames.StringLiteral, ClassOf("\"Hello, \""));
        Assert.Equal(ClassificationTypeNames.ControlKeyword, ClassOf("Return"));
    }

    [Fact]
    public async Task Format_document_indents_blocks()
    {
        const string Messy = """
            Public Module Greeter
            Public Function Greet(name As String) As String
            Return "Hello, " & name
            End Function
            End Module
            """;
        _host.UpdateText(GreeterPath, Messy);

        var edits = await _host.GetFormattingEditsAsync(GreeterPath);

        var formatted = Messy;
        foreach (var edit in edits.Reverse())
        {
            formatted = formatted.Remove(edit.Start, edit.Length).Insert(edit.Start, edit.NewText);
        }

        Assert.NotEmpty(edits);
        Assert.Contains("\n    Public Function Greet(name As String) As String", formatted.ReplaceLineEndings("\n"), StringComparison.Ordinal);
        Assert.Contains("\n        Return \"Hello, \" & name", formatted.ReplaceLineEndings("\n"), StringComparison.Ordinal);
        Assert.Contains("\n    End Function", formatted.ReplaceLineEndings("\n"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Builds_and_runs_a_visual_basic_program_with_a_project_reference()
    {
        var app = _host.FindProject(_temp.Resolve("App/App.vbproj"))!;
        var build = await new ProjectBuilder(_host).BuildAsync(app);

        Assert.True(build.Success, string.Join('\n', build.Diagnostics));
        Assert.Equal("Greetings", Assert.Single(build.Dependencies).Name);

        var console = new CapturingConsole();
        var run = await new ProgramRunner().RunAsync(build.EntryAssembly!, build.Dependencies, [], console);

        Assert.Equal(RunOutcome.Completed, run.Outcome);
        Assert.Equal("Hello, tablet\n", console.Output);
    }

    [Fact]
    public async Task A_new_file_joins_the_visual_basic_project_that_contains_it()
    {
        var path = _temp.Write("Greetings/Farewell.vb", """
            Public Module Farewell
                Public Function Bye() As String
                    Return "bye"
                End Function
            End Module
            """);

        Assert.True(_host.AddDocument(path, await File.ReadAllTextAsync(path)));
        Assert.False(_host.AddDocument(_temp.Write("Greetings/Wrong.cs", "class Wrong { }"), "class Wrong { }"));
        _host.UpdateText(ProgramPath, Program.Replace("Greeter.Greet(\"tablet\")", "Farewell.Bye()", StringComparison.Ordinal));
        var diagnostics = await _host.GetDocumentDiagnosticsAsync(ProgramPath);

        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverityLevel.Error);
    }

    public void Dispose()
    {
        _host.Dispose();
        _temp.Dispose();
    }

    /// <summary>A workspace with one Visual Basic console project, <c>App</c>, and its <c>Program.vb</c>.</summary>
    private sealed class SingleProject : IDisposable
    {
        private readonly TempDirectory _temp = new();

        private SingleProject()
        {
        }

        public RoslynWorkspaceHost Host { get; } = new();

        public string ProgramPath => _temp.Resolve("App/Program.vb");

        /// <summary>Loads the project.</summary>
        /// <param name="properties">More properties, as XML.</param>
        /// <param name="items">More items, as XML.</param>
        /// <param name="program">The source of <c>Program.vb</c>.</param>
        public static SingleProject Load(string properties, string items, string program)
        {
            var project = new SingleProject();
            project._temp.Write("App/App.vbproj", $"""
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <TargetFramework>net10.0</TargetFramework>
                    {properties}
                  </PropertyGroup>
                  <ItemGroup>{items}</ItemGroup>
                </Project>
                """);
            project._temp.Write("App/Program.vb", program);
            project.Host.Load(WorkspaceLoader.Load(project._temp.Path), FileSystemTextProvider.Instance);
            return project;
        }

        public void Dispose()
        {
            Host.Dispose();
            _temp.Dispose();
        }
    }
}
