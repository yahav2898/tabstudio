using TabStudio.Compilation.Running;

namespace TabStudio.Compilation.Tests;

public sealed class ProgramRunnerTests
{
    private readonly CSharpCompiler _compiler = new();
    private readonly ProgramRunner _runner = new();

    [Fact]
    public async Task Captures_standard_output()
    {
        var console = new CapturingConsole();

        var result = await RunAsync("""
            Console.WriteLine("Hello from the tablet");
            Console.Write('!');
            """, console);

        Assert.Equal(RunOutcome.Completed, result.Outcome);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal("Hello from the tablet\n!", console.Output);
    }

    [Fact]
    public async Task Returns_the_exit_code_from_main()
    {
        var result = await RunAsync("return 42;", new CapturingConsole());

        Assert.Equal(42, result.ExitCode);
    }

    [Fact]
    public async Task Awaits_an_async_main()
    {
        var console = new CapturingConsole();

        var result = await RunAsync("""
            await Task.Delay(10);
            Console.WriteLine("after await");
            return 7;
            """, console);

        Assert.Equal(RunOutcome.Completed, result.Outcome);
        Assert.Equal(7, result.ExitCode);
        Assert.Equal("after await\n", console.Output);
    }

    [Fact]
    public async Task Passes_command_line_arguments()
    {
        var console = new CapturingConsole();

        await RunAsync("Console.WriteLine(string.Join('|', args));", console, ["a", "b c"]);

        Assert.Equal("a|b c\n", console.Output);
    }

    [Fact]
    public async Task Reports_an_unhandled_exception_as_a_crash_with_a_stack_trace()
    {
        var console = new CapturingConsole();

        var result = await RunAsync("""
            Console.WriteLine("before");
            throw new InvalidOperationException("boom");
            """, console);

        Assert.Equal(RunOutcome.Crashed, result.Outcome);
        Assert.Equal(-1, result.ExitCode);
        Assert.IsType<InvalidOperationException>(result.Exception);
        Assert.Equal("before\n", console.Output);
        Assert.Contains("Unhandled exception. System.InvalidOperationException: boom", console.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reads_lines_from_console_input()
    {
        var console = new CapturingConsole();
        using var input = new ConsoleInput();
        input.SubmitLine("Ada");
        input.Complete();

        await RunAsync("""
            var name = Console.ReadLine();
            Console.WriteLine($"Hi {name}");
            Console.WriteLine(Console.ReadLine() is null ? "end" : "more");
            """, console, input: input);

        Assert.Equal("Hi Ada\nend\n", console.Output);
    }

    [Fact]
    public async Task Stopping_a_program_returns_without_waiting_for_it()
    {
        using var input = new ConsoleInput();
        using var stop = new CancellationTokenSource();
        input.WaitingForInput += (_, _) => stop.Cancel();

        var result = await RunAsync("Console.ReadLine(); Console.WriteLine(\"unreachable\");", new CapturingConsole(), input: input, cancellationToken: stop.Token);

        Assert.Equal(RunOutcome.Stopped, result.Outcome);
    }

    [Fact]
    public async Task Restores_the_console_after_a_run()
    {
        var originalOut = Console.Out;

        await RunAsync("Console.WriteLine(1);", new CapturingConsole());

        Assert.Same(originalOut, Console.Out);
    }

    [Fact]
    public async Task Loads_referenced_project_assemblies()
    {
        var library = _compiler.Compile(new CompilationRequest
        {
            AssemblyName = "Greetings",
            Kind = AssemblyKind.Library,
            Documents = [new SourceDocument("Greeter.cs", "public static class Greeter { public static string Greet(string n) => $\"Hello, {n}\"; }")],
        });
        Assert.True(library.Success);

        var program = _compiler.Compile(new CompilationRequest
        {
            AssemblyName = "App",
            Documents = [new SourceDocument("Program.cs", "Console.WriteLine(Greeter.Greet(\"tablet\"));")],
            AdditionalReferences = [Microsoft.CodeAnalysis.MetadataReference.CreateFromImage(library.Assembly!.Image)],
        });
        Assert.True(program.Success, string.Join('\n', program.Diagnostics));

        var console = new CapturingConsole();
        var result = await _runner.RunAsync(program.Assembly!, [library.Assembly!], [], console);

        Assert.Equal(RunOutcome.Completed, result.Outcome);
        Assert.Equal("Hello, tablet\n", console.Output);
    }

    private async Task<RunResult> RunAsync(
        string source,
        CapturingConsole console,
        string[]? arguments = null,
        ConsoleInput? input = null,
        CancellationToken cancellationToken = default)
    {
        var compiled = _compiler.Compile(new CompilationRequest
        {
            AssemblyName = "Program" + Guid.NewGuid().ToString("N")[..8],
            Documents = [new SourceDocument("Program.cs", source)],
        }, CancellationToken.None);
        Assert.True(compiled.Success, string.Join('\n', compiled.Diagnostics));

        return await _runner.RunAsync(compiled.Assembly!, [], arguments ?? [], console, input, cancellationToken);
    }
}
