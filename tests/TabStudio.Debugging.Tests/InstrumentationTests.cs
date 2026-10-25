using System.Text.RegularExpressions;
using TabStudio.Compilation;
using TabStudio.Compilation.Running;
using TabStudio.Debugging.Building;

namespace TabStudio.Debugging.Tests;

public sealed partial class InstrumentationTests
{
    private const string Throwing = """
        Run();

        static void Run()
        {
            var values = new[] { 1, 2 };
            Helper(values);
        }

        static void Helper(int[] values)
        {
            Console.WriteLine(values.Length);
            throw new InvalidOperationException("boom");
        }
        """;

    /// <summary>A sweep of C# constructs the instrumenter has to keep compiling and behaving the same.</summary>
    private const string Constructs = """
        var shapes = new List<Shape> { new Circle(1), new Square(2) };
        foreach (var shape in shapes)
            Console.WriteLine($"{shape.Name}: {(int)(shape.Area * 100)}");

        Console.WriteLine(string.Join(",", Numbers.Up(3)));
        await foreach (var n in Numbers.UpAsync(2)) Console.Write(n);
        Console.WriteLine();

        var counter = new Counter();
        counter.Increment();
        counter++;
        Console.WriteLine(counter.Value + " " + counter[1]);

        int total = 0;
        Add(ref total, 5);
        Console.WriteLine(total);
        Console.WriteLine(Classify(3) + Classify(-1));

        Func<int, int> twice = x =>
        {
            var y = x * 2;
            return y; // lambda
        };
        Func<int, int> square = static x => { return x * x; };
        Action<string> print = delegate (string s) { Console.WriteLine(s); };
        print($"{twice(4)} {square(3)}");

        var i = 0;
        loop:
        if (i < 2) { i++; goto loop; }
        Console.WriteLine(i);

        lock (shapes) Console.WriteLine("locked");
        using (var reader = new StringReader("line")) Console.WriteLine(reader.ReadLine());
        try { throw new InvalidOperationException("caught"); }
        catch (InvalidOperationException e) when (e.Message == "caught") { Console.WriteLine(e.Message); }
        finally { Console.WriteLine("finally"); }

        var point = new Point(1, 2);
        Console.WriteLine(point with { Y = 5 });
        Console.WriteLine(Describe(point));
        Console.WriteLine(LocalSum(2, 3));
        do total--; while (total > 3);
        Console.WriteLine(total);
        switch (total) { case 3: Console.WriteLine("three"); break; default: Console.WriteLine("other"); break; }
        Console.WriteLine(new Vector { X = -4 }.Length());

        static void Add(ref int target, in int amount) { target += amount; }
        static string Classify(int n) => n switch { > 0 => "positive", < 0 => "negative", _ => "zero" };
        static string Describe(object o) { if (o is Point { X: var x }) return "x=" + x; return "?"; }
        int LocalSum(int a, int b) => a + b;

        abstract record Shape(string Name)
        {
            public abstract double Area { get; }
        }

        sealed record Circle(double Radius) : Shape("circle")
        {
            public override double Area => Math.PI * Radius * Radius; // property
        }

        sealed record Square(double Side) : Shape("square")
        {
            public override double Area { get { return Side * Side; } }
        }

        readonly record struct Point(int X, int Y);

        sealed class Counter
        {
            private int _value;
            public int Value => _value;
            public int this[int offset] => _value + offset;
            public void Increment() => _value++;
            public static Counter operator ++(Counter c) { c._value++; return c; }
        }

        static class Numbers
        {
            public static IEnumerable<int> Up(int count)
            {
                for (var i = 1; i <= count; i++)
                {
                    yield return i; // iterator
                }
            }

            public static async IAsyncEnumerable<int> UpAsync(int count)
            {
                for (var i = 1; i <= count; i++)
                {
                    await Task.Yield();
                    yield return i;
                }
            }
        }

        struct Vector
        {
            public int X;
            public int Length() { var copy = this; return Math.Abs(copy.X); }
        }
        """;

    private const string ConstructsOutput = """
        circle: 314
        square: 400
        1,2,3
        12
        2 3
        5
        positivenegative
        8 9
        2
        locked
        line
        caught
        finally
        Point { X = 1, Y = 5 }
        x=1
        5
        3
        three
        4

        """;

    [Fact]
    public async Task Instrumentation_keeps_the_line_numbers_of_exception_stack_traces()
    {
        var original = new CSharpCompiler().Compile(new CompilationRequest
        {
            AssemblyName = "Original" + Guid.NewGuid().ToString("N")[..8],
            Documents = [new SourceDocument(DebuggedProgram.FilePath, Throwing)],
        });
        Assert.True(original.Success, string.Join('\n', original.Diagnostics));
        var instrumented = DebuggedProgram.Compile(Throwing);

        var plainRun = await new ProgramRunner().RunAsync(original.Assembly!, [], [], new CapturingConsole());
        var debugRun = await new ProgramRunner().RunAsync(instrumented.Result.EntryAssembly!, [], [], new CapturingConsole());

        Assert.Equal(new[] { 12, 6, 1 }, StackLines(plainRun.Exception));
        Assert.Equal(StackLines(plainRun.Exception), StackLines(debugRun.Exception));
    }

    [Fact]
    public async Task A_program_without_a_debugger_attached_runs_normally()
    {
        var build = DebuggedProgram.Compile(Constructs);
        var console = new CapturingConsole();

        var result = await new ProgramRunner().RunAsync(build.Result.EntryAssembly!, [], [], console);

        Assert.True(result.Outcome == RunOutcome.Completed, console.Error);
        Assert.Equal(ConstructsOutput, console.Output);
    }

    [Fact]
    public async Task Every_construct_keeps_its_behavior_under_the_debugger()
    {
        var (property, iterator, lambda) = (LineOf(Constructs, "// property"), LineOf(Constructs, "// iterator"), LineOf(Constructs, "// lambda"));
        using var program = new DebuggedProgram(Constructs, property, iterator, lambda);
        program.Start();

        var inProperty = await program.NextPauseAsync();
        Assert.Equal(property, inProperty.Location?.Line);
        Assert.Equal("Circle.Area.get()", inProperty.CallStack[0].MethodName);
        Assert.Equal("Circle", DebuggedProgram.Local(inProperty, "this").Type);
        program.Session.Continue();

        var inIterator = await program.NextPauseAsync();
        Assert.Equal(iterator, inIterator.Location?.Line);
        Assert.Equal("1", DebuggedProgram.Local(inIterator, "i").Value);
        Assert.Equal("Numbers.Up(int)", inIterator.CallStack[0].MethodName);
        program.Session.RemoveBreakpoint(DebuggedProgram.FilePath, iterator);
        program.Session.Continue();

        var inLambda = await program.NextPauseAsync();
        Assert.Equal(lambda, inLambda.Location?.Line);
        Assert.Equal("4", DebuggedProgram.Local(inLambda, "x").Value);
        Assert.Equal("8", DebuggedProgram.Local(inLambda, "y").Value);
        Assert.Equal("<lambda>(int)", inLambda.CallStack[0].MethodName);
        program.Session.Continue();

        var result = await program.FinishAsync();
        Assert.True(result.Outcome == RunOutcome.Completed, program.Console.Error);
        Assert.Equal(ConstructsOutput, program.Console.Output);
    }

    [Fact]
    public async Task Programs_with_span_and_ref_locals_compile_and_report_what_can_be_read()
    {
        using var program = new DebuggedProgram("""
            int[] array = { 1, 2, 3 };
            Span<int> numbers = stackalloc int[3];
            numbers[0] = 5;
            ref int first = ref numbers[0];
            first = 7;
            ref int slot = ref array[1];
            slot = 20;
            ReadOnlySpan<char> text = "hello".AsSpan();
            Fill(ref slot, in first, out var length, numbers);
            Console.WriteLine(numbers[0] + slot + text.Length + length);

            static void Fill(ref int target, in int source, out int length, Span<int> span)
            {
                target = source + target;
                length = span.Length;
            }
            """, 10, 15);
        program.Start();

        var inFill = await program.NextPauseAsync();
        Assert.Equal(15, inFill.Location?.Line);
        Assert.Equal("27", DebuggedProgram.Local(inFill, "target").Value);
        Assert.Equal("7", DebuggedProgram.Local(inFill, "source").Value);
        Assert.DoesNotContain(inFill.Locals, local => local.Name is "span" or "length");
        program.Session.Continue();

        var atEnd = await program.NextPauseAsync();
        Assert.Equal("7", DebuggedProgram.Local(atEnd, "first").Value);
        Assert.Equal("27", DebuggedProgram.Local(atEnd, "slot").Value);
        Assert.Equal("3", DebuggedProgram.Local(atEnd, "length").Value);
        Assert.DoesNotContain(atEnd.Locals, local => local.Name is "numbers" or "text");
        program.Session.Continue();

        await program.FinishAsync();
        Assert.Equal("42\n", program.Console.Output);
    }

    [Fact]
    public void Generated_files_are_not_instrumented()
    {
        var compilation = new CSharpCompiler().CreateCompilation(new CompilationRequest
        {
            AssemblyName = "Generated",
            Documents =
            [
                new SourceDocument(DebuggedProgram.FilePath, "Console.WriteLine(Helper.Value() + Marked.Value());"),
                new SourceDocument("obj/Helper.g.cs", "static class Helper { public static int Value() { return 1; } }"),
                new SourceDocument("Marked.cs", "// <auto-generated/>\nstatic class Marked { public static int Value() { return 2; } }"),
            ],
        });

        var build = DebugBuilder.Build(compilation);

        Assert.True(build.Success, string.Join('\n', build.Result.Diagnostics));
        Assert.All(build.DebugInfo.Statements, s => Assert.Equal(DebuggedProgram.FilePath, s.Location.FilePath));
        Assert.Single(build.DebugInfo.Statements);
        Assert.DoesNotContain(build.DebugInfo.Methods, m => m.Name.Contains("Value", StringComparison.Ordinal));
    }

    [Fact]
    public void The_in_process_safety_check_still_rejects_environment_exit()
    {
        var compilation = new CSharpCompiler().CreateCompilation(new CompilationRequest
        {
            AssemblyName = "Exits",
            Documents = [new SourceDocument(DebuggedProgram.FilePath, "Environment.Exit(3);")],
        });

        var build = DebugBuilder.Build(compilation);

        Assert.False(build.Success);
        Assert.Contains(build.Result.Diagnostics, d => d.Id == InProcessSafetyAnalyzer.ExitId);
    }

    [Fact]
    public void Statements_are_mapped_to_their_original_positions()
    {
        var build = DebuggedProgram.Compile("""
            var x = 1;
            if (x > 0)
                Console.WriteLine(x);
            """);

        var statements = build.DebugInfo.GetStatements(DebuggedProgram.FilePath);

        Assert.Equal(new[] { 1, 2, 3 }, statements.Select(s => s.Location.Line));
        Assert.Equal("if (x > 0)".Length, statements[1].Location.Length);
        Assert.Equal(5, statements[2].Location.Column);
        Assert.Contains(statements[2].Locals, l => l.Name == "x" && l.Type == "int");
    }

    private static int LineOf(string source, string marker) =>
        source.Split('\n').Select((text, index) => (text, index)).Single(l => l.text.Contains(marker, StringComparison.Ordinal)).index + 1;

    private static int[] StackLines(Exception? exception)
    {
        Assert.NotNull(exception);
        return LinePattern().Matches(exception.StackTrace ?? string.Empty)
            .Select(m => int.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture))
            .ToArray();
    }

    [GeneratedRegex(@"Program\.cs:line (\d+)")]
    private static partial Regex LinePattern();
}
