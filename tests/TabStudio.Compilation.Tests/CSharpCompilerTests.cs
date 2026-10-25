using Microsoft.CodeAnalysis;
using TabStudio.Core.Diagnostics;

namespace TabStudio.Compilation.Tests;

public sealed class CSharpCompilerTests
{
    private static readonly string ProgramPath = Path.Combine(Path.GetTempPath(), "Hello", "Program.cs");

    private readonly CSharpCompiler _compiler = new();

    [Fact]
    public void Compiles_top_level_statements_with_implicit_usings()
    {
        var result = _compiler.Compile(Request("""
            var names = new List<string> { "tablet", "phone" };
            Console.WriteLine(string.Join(", ", names.Select(n => n.ToUpperInvariant())));
            """));

        Assert.True(result.Success, string.Join('\n', result.Diagnostics));
        Assert.NotNull(result.Assembly);
        Assert.NotEmpty(result.Assembly.Image);
        Assert.NotNull(result.Assembly.Symbols);
        Assert.Equal(0, result.ErrorCount);
    }

    [Fact]
    public void Reports_errors_with_one_based_positions_in_the_right_file()
    {
        var result = _compiler.Compile(Request("""
            Console.WriteLine("ok");
            Console.WriteLine(undefinedName);
            """));

        Assert.False(result.Success);
        var error = Assert.Single(result.Diagnostics, d => d.Severity == DiagnosticSeverityLevel.Error);
        Assert.Equal("CS0103", error.Id);
        Assert.Equal(ProgramPath, error.Location?.FilePath);
        Assert.Equal(2, error.Location?.Line);
        Assert.Equal(19, error.Location?.Column);
        Assert.Equal("undefinedName".Length, error.Location?.Length);
    }

    [Fact]
    public void Warnings_do_not_stop_a_build()
    {
        var result = _compiler.Compile(Request("""
            string? maybe = null;
            Console.WriteLine(maybe.Length);
            """));

        Assert.True(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Id == "CS8602" && d.Severity == DiagnosticSeverityLevel.Warning);
    }

    [Fact]
    public void Assembly_version_unification_is_not_a_warning_as_in_the_SDK()
    {
        var options = CSharpCompiler.CreateCompilationOptions(AssemblyKind.Library, nullable: true);

        Assert.Equal(ReportDiagnostic.Suppress, options.SpecificDiagnosticOptions["CS1701"]);
        Assert.Equal(ReportDiagnostic.Suppress, options.SpecificDiagnosticOptions["CS1702"]);
    }

    [Theory]
    [InlineData("Environment.Exit(1);", InProcessSafetyAnalyzer.ExitId)]
    [InlineData("System.Environment.FailFast(\"bye\");", InProcessSafetyAnalyzer.FailFastId)]
    public void Refuses_to_build_programs_that_would_close_the_ide(string statement, string expectedId)
    {
        var result = _compiler.Compile(Request(statement));

        Assert.False(result.Success);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(expectedId, diagnostic.Id);
        Assert.Equal(DiagnosticSeverityLevel.Error, diagnostic.Severity);
    }

    [Fact]
    public void A_method_named_exit_on_another_type_is_fine()
    {
        var result = _compiler.Compile(Request("""
            Door.Exit(1);
            static class Door { public static void Exit(int code) => Console.WriteLine(code); }
            """));

        Assert.True(result.Success, string.Join('\n', result.Diagnostics));
    }

    [Fact]
    public void Libraries_are_not_checked_for_in_process_safety()
    {
        var request = Request("public static class Quit { public static void Now() => System.Environment.Exit(0); }") with
        {
            Kind = AssemblyKind.Library,
        };

        Assert.True(_compiler.Compile(request).Success);
    }

    private static CompilationRequest Request(string source) => new()
    {
        AssemblyName = "Hello",
        Documents = [new SourceDocument(ProgramPath, source)],
    };
}
