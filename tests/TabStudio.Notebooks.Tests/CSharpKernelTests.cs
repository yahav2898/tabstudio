using Microsoft.CodeAnalysis;
using TabStudio.Compilation;
using TabStudio.Core.Diagnostics;
using TabStudio.Notebooks.Execution;
using TabStudio.Notebooks.Outputs;
using TabStudio.Testing;

namespace TabStudio.Notebooks.Tests;

[Collection(KernelCollection.Name)]
public sealed class CSharpKernelTests : IDisposable
{
    private readonly CSharpKernel _kernel = new();

    public void Dispose() => _kernel.Dispose();

    [Fact]
    public async Task Variables_carry_over_from_cell_to_cell()
    {
        await _kernel.RunAsync("var counter = 41;");
        await _kernel.RunAsync("counter++;");

        var result = await _kernel.RunAsync("counter");

        Assert.Equal("42", result.ResultText());
    }

    [Fact]
    public async Task Functions_and_types_from_one_cell_can_be_used_in_later_cells()
    {
        await _kernel.RunAsync("""
            record Point(int X, int Y);
            static int Square(int value) => value * value;
            """);

        var result = await _kernel.RunAsync("Square(new Point(3, 4).Y)");

        Assert.Equal("16", result.ResultText());
    }

    [Fact]
    public async Task Usings_from_an_earlier_cell_apply_to_later_cells()
    {
        await _kernel.RunAsync("using System.Text.RegularExpressions;");

        var result = await _kernel.RunAsync("""Regex.IsMatch("tablet", "^tab")""");

        Assert.Equal("True", result.ResultText());
    }

    [Fact]
    public async Task The_last_expression_without_a_semicolon_is_the_result()
    {
        var value = await _kernel.RunAsync("""var name = "TabStudio"; name.ToUpperInvariant()""");
        var statement = await _kernel.RunAsync("name.ToUpperInvariant();");
        var nothing = await _kernel.RunAsync("string? missing = null; missing");

        var output = Assert.IsType<DisplayOutput>(Assert.Single(value.Outputs));
        Assert.True(output.IsResult);
        Assert.Equal(value.ExecutionCount, output.ExecutionCount);
        Assert.Equal("TABSTUDIO", output.Data.PlainText);
        Assert.Empty(statement.Outputs);
        Assert.Equal("<null>", nothing.ResultText());
    }

    [Fact]
    public async Task Display_shows_values_in_order_with_console_output()
    {
        var result = await _kernel.RunAsync("""
            Console.Write("before ");
            display(42);
            Console.WriteLine("between");
            Display("text");
            new[] { 1, 2 }.Display();
            """);

        Assert.Collection(
            result.Outputs,
            o => Assert.Equal("before ", Assert.IsType<StreamOutput>(o).Text),
            o => Assert.Equal("42", Assert.IsType<DisplayOutput>(o).Data.PlainText),
            o => Assert.Equal("between\n", Assert.IsType<StreamOutput>(o).Text.ReplaceLineEndings("\n")),
            o => Assert.Equal("text", Assert.IsType<DisplayOutput>(o).Data.PlainText),
            o => Assert.Equal("[1, 2]", Assert.IsType<DisplayOutput>(o).Data.PlainText));
        Assert.DoesNotContain(result.Outputs.OfType<DisplayOutput>(), o => o.IsResult);
    }

    [Fact]
    public async Task DisplayAs_shows_a_string_as_the_given_type()
    {
        var result = await _kernel.RunAsync("""
            "<b>bold</b>".DisplayAs("text/html");
            display("# Title", "text/markdown");
            """);

        var html = Assert.IsType<DisplayOutput>(result.Outputs[0]);
        Assert.Equal("<b>bold</b>", html.Data.Html);
        Assert.Equal(1, html.Data.Count);
        Assert.Equal("# Title", Assert.IsType<DisplayOutput>(result.Outputs[1]).Data[MimeTypes.Markdown]);
    }

    [Fact]
    public async Task LINQ_results_are_shown_as_tables()
    {
        var result = await _kernel.RunAsync("""
            Enumerable.Range(1, 3).Select(i => new { Number = i, Square = i * i })
            """);

        var output = Assert.IsType<DisplayOutput>(Assert.Single(result.Outputs));
        var table = Assert.IsType<TableOutput>(output.Table);
        Assert.Equal(new[] { "Number", "Square" }, table.Columns);
        Assert.Equal(new[] { "2", "4" }, table.Rows[1]);
        Assert.Contains("<th>Square</th>", output.Data.Html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Console_output_and_errors_are_captured_and_the_console_is_restored()
    {
        var originalOut = Console.Out;

        var result = await _kernel.RunAsync("""
            Console.WriteLine("to out");
            Console.Error.WriteLine("to error");
            Console.WriteLine(Console.ReadLine() is null ? "no input" : "input");
            """);

        Assert.Equal("to out\nno input\n", result.StreamText());
        Assert.Equal("to error\n", result.StreamText(StreamOutput.StandardError));
        Assert.Same(originalOut, Console.Out);
    }

    [Fact]
    public async Task Cells_can_await()
    {
        var result = await _kernel.RunAsync("""
            await Task.Delay(10);
            Console.WriteLine("after await");
            await Task.FromResult(5)
            """);

        Assert.Equal("after await\n", result.StreamText());
        Assert.Equal("5", result.ResultText());
    }

    [Fact]
    public async Task Compile_errors_have_positions_relative_to_the_cell()
    {
        var result = await _kernel.ExecuteAsync("var a = 1;\nvar b = undefinedName;");

        Assert.Equal(CellExecutionStatus.CompilationFailed, result.Status);
        var error = Assert.Single(result.Diagnostics, d => d.Severity == DiagnosticSeverityLevel.Error);
        Assert.Equal("CS0103", error.Id);
        Assert.Equal(2, error.Location?.Line);
        Assert.Equal(9, error.Location?.Column);
        Assert.Equal("var a = 1;\nvar b = ".Length, error.Location?.Start);
        Assert.Equal("undefinedName".Length, error.Location?.Length);

        var output = Assert.IsType<ErrorOutput>(Assert.Single(result.Outputs));
        Assert.StartsWith("(2,9): error CS0103:", output.Message, StringComparison.Ordinal);
        Assert.Equal(1, result.ExecutionCount);
    }

    [Fact]
    public async Task Kernel_lines_do_not_move_the_positions_of_compile_errors()
    {
        var cell = NotebookCell.Code("#!csharp\n#!time\n  int x = \"text\";");

        var result = await _kernel.ExecuteAsync(cell);

        var error = Assert.Single(result.Diagnostics, d => d.Severity == DiagnosticSeverityLevel.Error);
        Assert.Equal("CS0029", error.Id);
        Assert.Equal(3, error.Location?.Line);
        Assert.Equal(11, error.Location?.Column);
        Assert.Equal(cell.Id, error.Location?.FilePath);
    }

    [Fact]
    public async Task A_cell_that_fails_to_compile_does_not_break_later_cells()
    {
        await _kernel.RunAsync("var kept = 1;");
        var failed = await _kernel.ExecuteAsync("var lost = 2; this is not C#");

        var result = await _kernel.RunAsync("kept + 1");

        Assert.Equal(CellExecutionStatus.CompilationFailed, failed.Status);
        Assert.Equal("2", result.ResultText());
    }

    [Fact]
    public async Task Exceptions_are_reported_with_a_stack_trace_trimmed_to_the_cell()
    {
        await _kernel.RunAsync("int Divide(int a, int b) => a / b;");

        var result = await _kernel.ExecuteAsync("Console.WriteLine(\"before\");\nDivide(1, 0);");

        Assert.Equal(CellExecutionStatus.Failed, result.Status);
        Assert.IsType<DivideByZeroException>(result.Exception);
        Assert.Equal("before\n", result.StreamText());
        var error = Assert.IsType<ErrorOutput>(result.Outputs[^1]);
        Assert.Equal("System.DivideByZeroException", error.Name);
        Assert.StartsWith("System.DivideByZeroException: ", error.Traceback[0], StringComparison.Ordinal);
        Assert.Contains(error.Traceback, line => line.Contains("Divide", StringComparison.Ordinal));
        Assert.Contains("Submission#", error.Traceback[^1], StringComparison.Ordinal);
        Assert.DoesNotContain(error.Traceback, line => line.Contains("TabStudio.Notebooks", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Variables_declared_before_an_exception_keep_their_values()
    {
        await _kernel.ExecuteAsync("var reached = \"yes\"; throw new InvalidOperationException(\"boom\");");

        var result = await _kernel.RunAsync("reached");

        Assert.Equal("yes", result.ResultText());
    }

    [Theory]
    [InlineData("Environment.Exit(0);", "TS1001")]
    [InlineData("System.Environment.FailFast(\"bye\");", "TS1002")]
    public async Task Calls_that_would_close_TabStudio_are_rejected(string code, string expectedId)
    {
        var result = await _kernel.ExecuteAsync(code);

        Assert.Equal(CellExecutionStatus.CompilationFailed, result.Status);
        Assert.Equal(expectedId, Assert.Single(result.Diagnostics).Id);
    }

    [Fact]
    public async Task NuGet_references_report_that_they_are_not_supported_yet()
    {
        var result = await _kernel.ExecuteAsync("#r \"nuget: Newtonsoft.Json, 13.0.3\"\nConsole.WriteLine(1);");

        Assert.Equal(CellExecutionStatus.CompilationFailed, result.Status);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(NotebookDiagnosticIds.NuGetReferenceNotSupported, diagnostic.Id);
        Assert.Contains("nuget: Newtonsoft.Json, 13.0.3", diagnostic.Message, StringComparison.Ordinal);
        Assert.Equal(1, diagnostic.Location?.Line);
    }

    [Theory]
    [InlineData("#!set --name x --value 1", NotebookDiagnosticIds.MagicCommandNotSupported)]
    [InlineData("var a = 1;\n#!fsharp\nlet b = 2", NotebookDiagnosticIds.LanguageNotSupported)]
    [InlineData("#load \"helpers.csx\"", NotebookDiagnosticIds.LoadNotSupported)]
    [InlineData("#r \"Library.dll\"", NotebookDiagnosticIds.ReferenceNotResolved)]
    public async Task Directives_the_kernel_cannot_run_yet_are_reported(string code, string expectedId)
    {
        var result = await _kernel.ExecuteAsync(code);

        Assert.Equal(CellExecutionStatus.CompilationFailed, result.Status);
        Assert.Contains(result.Diagnostics, d => d.Id == expectedId);
    }

    [Fact]
    public async Task Cells_in_other_languages_are_unsupported_and_do_not_count()
    {
        var result = await _kernel.ExecuteAsync(NotebookCell.Code("let x = 1", NotebookLanguages.FSharp));

        Assert.Equal(CellExecutionStatus.Unsupported, result.Status);
        Assert.Equal(NotebookDiagnosticIds.LanguageNotSupported, Assert.Single(result.Diagnostics).Id);
        Assert.Null(result.ExecutionCount);
        Assert.Equal(0, _kernel.ExecutionCount);
    }

    [Fact]
    public async Task Markdown_cells_are_skipped()
    {
        var cell = NotebookCell.Markdown("# Title");

        var result = await _kernel.ExecuteAsync(cell);

        Assert.Equal(CellExecutionStatus.Skipped, result.Status);
        Assert.Same(cell, result.ApplyTo(cell));
    }

    [Fact]
    public async Task Execution_counts_and_timings_are_reported()
    {
        var first = await _kernel.RunAsync("1");
        var second = await _kernel.ExecuteAsync("not valid");
        var third = await _kernel.RunAsync("System.Threading.Thread.Sleep(20);");

        Assert.Equal(new int?[] { 1, 2, 3 }, new[] { first.ExecutionCount, second.ExecutionCount, third.ExecutionCount });
        Assert.Equal(3, _kernel.ExecutionCount);
        Assert.True(third.Duration >= TimeSpan.FromMilliseconds(20));
        Assert.True(third.CompilationDuration <= third.Duration);
    }

    [Fact]
    public async Task Events_report_start_output_and_completion()
    {
        var events = new List<string>();
        _kernel.ExecutionStarted += (_, e) => events.Add($"started {e.ExecutionCount}");
        _kernel.OutputReceived += (_, e) => events.Add($"output {e.Output.GetType().Name}");
        _kernel.ExecutionCompleted += (_, e) => events.Add($"completed {e.Result.Status}");

        await _kernel.RunAsync("Console.WriteLine(1); 2");

        Assert.Equal(new[] { "started 1", "output StreamOutput", "output DisplayOutput", "completed Succeeded" }, events);
    }

    [Fact]
    public async Task A_reference_resolver_supplies_packages_for_this_and_later_cells()
    {
        using var directory = new TempDirectory();
        var library = new CSharpCompiler().Compile(new CompilationRequest
        {
            AssemblyName = "Greetings",
            Kind = AssemblyKind.Library,
            Documents = [new SourceDocument("Greeter.cs", "public static class Greeter { public static string Greet(string name) => $\"Hello, {name}\"; }")],
        });
        Assert.True(library.Success);
        var path = directory.Resolve("Greetings.dll");
        await File.WriteAllBytesAsync(path, library.Assembly!.Image);

        var resolver = new FakeResolver(new NotebookReferenceResolution
        {
            References = [MetadataReference.CreateFromImage(library.Assembly.Image)],
            RuntimeAssemblyPaths = [path],
        });
        using var kernel = new CSharpKernel(new CSharpKernelOptions { ReferenceResolver = resolver });

        var first = await kernel.RunAsync("#r \"nuget: Greetings, 1.0.0\"\nGreeter.Greet(\"tablet\")");
        var later = await kernel.RunAsync("Greeter.Greet(\"phone\")");

        Assert.Equal(new NotebookReference(NotebookReferenceKind.NuGetPackage, "Greetings", "1.0.0"), Assert.Single(resolver.Requests));
        Assert.Equal("Hello, tablet", first.ResultText());
        Assert.Equal("Hello, phone", later.ResultText());
    }

    [Fact]
    public async Task A_reference_that_cannot_be_resolved_is_reported()
    {
        using var kernel = new CSharpKernel(new CSharpKernelOptions
        {
            ReferenceResolver = new FakeResolver(NotebookReferenceResolution.Failed("Package 'Missing' was not found.")),
        });

        var result = await kernel.ExecuteAsync("#r \"nuget: Missing\"");

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(NotebookDiagnosticIds.ReferenceNotResolved, diagnostic.Id);
        Assert.Contains("Package 'Missing' was not found.", diagnostic.Message, StringComparison.Ordinal);
    }

    private sealed class FakeResolver(NotebookReferenceResolution resolution) : INotebookReferenceResolver
    {
        public List<NotebookReference> Requests { get; } = [];

        public Task<NotebookReferenceResolution> ResolveAsync(NotebookReference reference, CancellationToken cancellationToken)
        {
            Requests.Add(reference);
            return Task.FromResult(resolution);
        }
    }
}
