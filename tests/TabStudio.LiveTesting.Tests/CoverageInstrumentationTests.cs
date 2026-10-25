using System.Globalization;
using System.Text.RegularExpressions;
using TabStudio.Compilation;
using TabStudio.Compilation.Running;
using TabStudio.LiveTesting.Instrumentation;

namespace TabStudio.LiveTesting.Tests;

public sealed partial class CoverageInstrumentationTests
{
    [Fact]
    public async Task Every_construct_keeps_its_behavior_and_records_its_statements()
    {
        var (assembly, module, _) = CoveredProgram.Instrument(Corpus.Constructs);
        var table = CoveredProgram.TableFor(module);

        var (result, output) = await CoveredProgram.RunAsync(assembly, table);

        Assert.True(result.Outcome == RunOutcome.Completed, output + result.Exception);
        Assert.Equal(Corpus.ConstructsOutput, output);
        var hit = CoveredProgram.HitLines(module, table);
        Assert.Contains(CoveredProgram.LineOf(Corpus.Constructs, "// property"), hit);
        Assert.Contains(CoveredProgram.LineOf(Corpus.Constructs, "// iterator"), hit);
        Assert.Contains(CoveredProgram.LineOf(Corpus.Constructs, "// lambda"), hit);
        Assert.Contains(CoveredProgram.LineOf(Corpus.Constructs, "public void Increment() => _value++;"), hit);
        Assert.Contains(CoveredProgram.LineOf(Corpus.Constructs, "int LocalSum(int a, int b) => a + b;"), hit);
        Assert.True(hit.Count > 40, $"Only {hit.Count} lines were recorded.");
    }

    [Fact]
    public async Task More_constructs_keep_their_behavior()
    {
        var plain = await CoveredProgram.RunAsync(CoveredProgram.Plain(Corpus.MoreConstructs));
        var (assembly, module, _) = CoveredProgram.Instrument(Corpus.MoreConstructs);
        var table = CoveredProgram.TableFor(module);

        var (result, output) = await CoveredProgram.RunAsync(assembly, table);

        Assert.True(result.Outcome == RunOutcome.Completed, output + result.Exception);
        Assert.Equal(plain.Output, output);
        Assert.Contains("Money { Cents = 11 }", output, StringComparison.Ordinal);
        Assert.Contains(CoveredProgram.LineOf(Corpus.MoreConstructs, "public event Action Changed"), CoveredProgram.HitLines(module, table));
    }

    [Fact]
    public async Task An_instrumented_program_runs_normally_outside_a_test()
    {
        var (assembly, _, _) = CoveredProgram.Instrument(Corpus.Constructs);

        var (result, output) = await CoveredProgram.RunAsync(assembly);

        Assert.True(result.Outcome == RunOutcome.Completed, output + result.Exception);
        Assert.Equal(Corpus.ConstructsOutput, output);
    }

    [Fact]
    public async Task Instrumentation_keeps_the_line_numbers_of_stack_traces()
    {
        var plain = await CoveredProgram.RunAsync(CoveredProgram.Plain(Corpus.Throwing));
        var (assembly, module, text) = CoveredProgram.Instrument(Corpus.Throwing);

        var covered = await CoveredProgram.RunAsync(assembly, CoveredProgram.TableFor(module));

        Assert.Equal(new[] { 12, 6, 1 }, StackLines(plain.Result.Exception));
        Assert.Equal(StackLines(plain.Result.Exception), StackLines(covered.Result.Exception));
        Assert.Equal(Corpus.Throwing.Split('\n').Length, text.Split('\n').Length);
    }

    [Fact]
    public async Task Only_the_branches_that_run_are_recorded()
    {
        const string source = """
            var x = Read();
            if (x > 5)
                Console.WriteLine("big");
            else
                Console.WriteLine("small");

            static int Read()
            {
                return 3;
            }

            static void Unused()
            {
                Console.WriteLine("never");
            }
            """;
        var (assembly, module, _) = CoveredProgram.Instrument(source);
        var table = CoveredProgram.TableFor(module);

        await CoveredProgram.RunAsync(assembly, table);

        Assert.Equal(new[] { 1, 2, 3, 5, 9, 14 }, module.Statements.Select(s => s.Line).Order());
        Assert.Equal(new[] { 1, 2, 5, 9 }, CoveredProgram.HitLines(module, table).Order());
    }

    [Fact]
    public void Members_are_keyed_by_type_name_and_parameters()
    {
        const string source = """
            namespace Shop;

            public class Cart<T>
            {
                private int _count;
                public int Count => _count;
                public void Add(T item, int quantity = 1) { _count += quantity; }
                public int this[int index] { get { return index; } }
                public static Cart<T> operator +(Cart<T> cart, T item) { cart.Add(item); return cart; }

                public class Line
                {
                    public Line() { }
                }
            }
            """;
        var compilation = CoveredProgram.Compilation(source).WithOptions(new Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions(Microsoft.CodeAnalysis.OutputKind.DynamicallyLinkedLibrary));

        var (_, module) = CoverageInstrumenter.Instrument(compilation, "/projects/Shop.csproj", CancellationToken.None);

        Assert.Equal(
            new[] { "Shop.Cart`1.Count", "Shop.Cart`1.Add(T,int)", "Shop.Cart`1.this[](int)", "Shop.Cart`1.operator +(Cart<T>,T)", "Shop.Cart`1+Line..ctor()" },
            module.Members.Select(m => m.Key));
        Assert.Equal(new[] { 6, 7, 8, 9, 13 }, module.Members.Select(m => m.Line));
        Assert.Equal("Shop.Cart`1+Line", module.Members[^1].TypeName);
    }

    [Fact]
    public void A_member_keeps_its_statements_when_lines_move()
    {
        const string before = """
            Console.WriteLine(Calculator.Twice(2));

            static class Calculator
            {
                public static int Twice(int x)
                {
                    var result = x * 2;
                    return result;
                }
            }
            """;
        var after = "// A comment\n\n" + before.Replace("static class", "// moved\nstatic class", StringComparison.Ordinal);

        var (_, first, _) = CoveredProgram.Instrument(before);
        var (_, second, _) = CoveredProgram.Instrument(after);

        var oldTwice = first.Members.Single(m => m.Name == "Twice");
        var newTwice = second.Members.Single(m => m.Name == "Twice");
        Assert.Equal(oldTwice.Key, newTwice.Key);
        Assert.Equal(
            first.StatementsOf(oldTwice.Index).Select(s => (s.Ordinal, s.Line + 3)),
            second.StatementsOf(newTwice.Index).Select(s => (s.Ordinal, s.Line)));
    }

    [Fact]
    public void Generated_files_are_not_instrumented()
    {
        var compilation = new CSharpCompiler().CreateCompilation(new CompilationRequest
        {
            AssemblyName = "Generated",
            Documents =
            [
                new SourceDocument(CoveredProgram.FilePath, "Console.WriteLine(Helper.Value() + Marked.Value());"),
                new SourceDocument("obj/Helper.g.cs", "static class Helper { public static int Value() { return 1; } }"),
                new SourceDocument("Marked.cs", "// <auto-generated/>\nstatic class Marked { public static int Value() { return 2; } }"),
            ],
        });

        var (_, module) = CoverageInstrumenter.Instrument(compilation, "/projects/Generated.csproj", CancellationToken.None);

        Assert.Equal(new[] { CoveredProgram.FilePath }, module.Files);
        Assert.Single(module.Statements);
    }

    private static int[] StackLines(Exception? exception)
    {
        Assert.NotNull(exception);
        return LinePattern().Matches(exception.StackTrace ?? string.Empty)
            .Select(m => int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture))
            .ToArray();
    }

    [GeneratedRegex(@"Program\.cs:line (\d+)")]
    private static partial Regex LinePattern();
}
