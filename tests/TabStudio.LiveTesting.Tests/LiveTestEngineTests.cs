using TabStudio.LiveTesting.Results;
using TabStudio.TestRunner;

namespace TabStudio.LiveTesting.Tests;

/// <summary>
/// The engine end to end on a real workspace: a library and its xUnit project, built for coverage, run with
/// TabStudio's test runner, and run again as the code changes.
/// </summary>
public sealed class LiveTestEngineTests : IDisposable
{
    private readonly LiveSolution _solution = new();

    [Fact]
    public async Task Coverage_is_attributed_per_test_and_per_line()
    {
        var summary = await _solution.RunAsync();

        Assert.Equal(4, summary.TestsRun);
        Assert.Empty(summary.FailedBuilds);
        var add = _solution.LibraryLine("return a + b;");
        Assert.Equal(LiveSolution.LineOf(LiveSolution.Library, "return a + b;"), add.Line);
        Assert.Equal(LineCoverageState.Covered, add.State);
        Assert.False(add.IsStale);
        Assert.Equal(
            new[] { "Adds", "Adds_pairs(a: 1, b: 1, sum: 2)", "Adds_pairs(a: 2, b: 2, sum: 4)" },
            add.Tests.Select(t => t.Test.DisplayName));
        Assert.All(add.Tests, t => Assert.Equal(TestOutcome.Passed, t.Outcome));

        var negate = _solution.LibraryLine("return -value;");
        Assert.Equal(LineCoverageState.NotCovered, negate.State);
        Assert.Empty(negate.Tests);
        Assert.False(negate.IsStale);
    }

    [Fact]
    public async Task A_failing_test_marks_its_lines_failing_until_the_code_is_fixed()
    {
        await _solution.RunAsync();
        var subtractLine = LiveSolution.LineOf(LiveSolution.Library, "Subtract(int a, int b)") + 2;
        var file = _solution.Engine.Index.GetFile(_solution.LibraryFile)!;

        var subtract = Assert.Single(file.Lines, l => l.Line == subtractLine);
        Assert.Equal(LineCoverageState.Failing, subtract.State);
        var test = Assert.Single(subtract.Tests);
        Assert.Equal("Subtracts", test.Test.MethodName);
        Assert.Equal(TestOutcome.Failed, test.Outcome);

        _solution.EditLibrary(LiveSolution.Library.Replace(
            "public static int Subtract(int a, int b)\n    {\n        return a + b;",
            "public static int Subtract(int a, int b)\n    {\n        return a - b;",
            StringComparison.Ordinal));
        await _solution.RunAsync();

        Assert.Equal(new[] { "Subtracts" }, _solution.QueuedMethods());
        Assert.Equal(LineCoverageState.Covered, _solution.LibraryLine("return a - b;").State);
    }

    [Fact]
    public async Task Editing_one_method_reruns_only_the_tests_that_covered_it()
    {
        await _solution.RunAsync();

        var text = LiveSolution.Library.Replace("return a + b;\n    }\n\n    public static int Subtract", "return b + a;\n    }\n\n    public static int Subtract", StringComparison.Ordinal);
        _solution.EditLibrary(text);
        await _solution.RunAsync();
        Assert.Equal(new[] { "Adds", "Adds_pairs" }, _solution.QueuedMethods());

        _solution.EditLibrary(text.Replace("return -value;", "return 0 - value;", StringComparison.Ordinal));
        await _solution.RunAsync();
        Assert.Empty(_solution.QueuedMethods());
    }

    [Fact]
    public async Task Comments_and_moved_lines_run_nothing_and_markers_follow_their_lines()
    {
        await _solution.RunAsync();

        _solution.EditLibrary("// Arithmetic for the calculator.\n// It has a bug.\n" + LiveSolution.Library);
        await _solution.RunAsync();

        Assert.Empty(_solution.QueuedMethods());
        var add = _solution.LibraryLine("return a + b;");
        Assert.Equal(LiveSolution.LineOf(LiveSolution.Library, "return a + b;") + 2, add.Line);
        Assert.Equal(3, add.Tests.Count);
    }

    [Fact]
    public async Task A_change_outside_function_bodies_runs_every_test()
    {
        await _solution.RunAsync();

        _solution.EditLibrary(LiveSolution.Library.Replace("{\n    public static int Add", "{\n    public const int Zero = 0;\n\n    public static int Add", StringComparison.Ordinal));
        await _solution.RunAsync();

        Assert.Equal(new[] { "Adds", "Adds_pairs", "Subtracts" }, _solution.QueuedMethods());
    }

    [Fact]
    public async Task New_and_changed_tests_run_and_excluded_tests_never_do()
    {
        await _solution.RunAsync();

        _solution.EditTests(LiveSolution.Tests.Replace(
            "    [Theory]",
            "    [Fact]\n    public void Negates() => Assert.Equal(-2, Arithmetic.Negate(2));\n\n    [Theory]",
            StringComparison.Ordinal));
        await _solution.RunAsync();
        Assert.Equal(new[] { "Negates" }, _solution.QueuedMethods());
        Assert.Equal(LineCoverageState.Covered, _solution.LibraryLine("return -value;").State);

        _solution.Engine.Inclusion.Set(_solution.TestProject.FilePath, "Calc.Tests.ArithmeticTests.Adds", include: false);
        _solution.EditLibrary(LiveSolution.Library.Replace("return a + b;\n    }\n\n    public static int Subtract", "return b + a;\n    }\n\n    public static int Subtract", StringComparison.Ordinal));
        await _solution.RunAsync();
        Assert.Equal(new[] { "Adds_pairs" }, _solution.QueuedMethods());
    }

    [Fact]
    public async Task Code_a_class_fixture_runs_counts_for_every_test_of_the_class()
    {
        _solution.EditTests(LiveSolution.Tests.Replace(
            "public class ArithmeticTests\n{\n",
            "public class Fixture\n{\n    public Fixture() => Value = Arithmetic.Negate(1);\n\n    public int Value { get; }\n}\n\n" +
            "public class ArithmeticTests : IClassFixture<Fixture>\n{\n    public ArithmeticTests(Fixture fixture) => _ = fixture;\n\n",
            StringComparison.Ordinal));

        await _solution.RunAsync();

        // The fixture is created once, for the class: its code counts for each of the class's tests, the failing one too.
        var negate = _solution.LibraryLine("return -value;");
        Assert.Equal(4, negate.Tests.Count);
        Assert.Equal(new[] { "Adds", "Adds_pairs", "Subtracts" }, negate.Tests.Select(t => t.Test.MethodName).Distinct().Order(StringComparer.Ordinal));
        Assert.Equal(LineCoverageState.Failing, negate.State);
    }

    [Fact]
    public async Task Tests_asked_for_run_whatever_changed()
    {
        await _solution.RunAsync();

        await _solution.RunAsync(new TestKey(_solution.TestProject.FilePath, "Calc.Tests.ArithmeticTests.Subtracts"));

        Assert.Equal(new[] { "Subtracts" }, _solution.QueuedMethods());
        Assert.Single(_solution.Listener.Finished);
    }

    [Fact]
    public async Task Lines_are_stale_while_their_tests_wait_to_run()
    {
        await _solution.RunAsync();

        _solution.Engine.Invalidate(_solution.TestProject.FilePath, ["Calc.Tests.ArithmeticTests.Adds"]);

        Assert.True(_solution.LibraryLine("return a + b;").IsStale);
        Assert.False(_solution.LibraryLine("return -value;").IsStale);
    }

    [Fact]
    public async Task A_build_error_stops_the_run_and_keeps_the_last_results()
    {
        await _solution.RunAsync();

        _solution.EditLibrary(LiveSolution.Library.Replace("return -value;", "return -missing;", StringComparison.Ordinal));
        var summary = await _solution.RunAsync();

        var failed = Assert.Single(summary.FailedBuilds);
        Assert.Contains(failed.Result.Diagnostics, d => d.Id == "CS0103");
        Assert.Equal(0, summary.TestsRun);
        Assert.Equal(LineCoverageState.Covered, _solution.LibraryLine("return a + b;").State);
    }

    [Fact]
    public async Task An_excluded_project_is_not_instrumented()
    {
        _solution.Engine.Inclusion.Set(_solution.LibraryProject.FilePath, string.Empty, include: false);

        var summary = await _solution.RunAsync();

        Assert.Equal(4, summary.TestsRun);
        Assert.Null(_solution.Engine.Index.GetFile(_solution.LibraryFile));
        Assert.Empty(Assert.Single(_solution.Listener.Builds).Modules);
    }

    [Fact]
    public async Task Visual_basic_projects_get_coverage_and_rerun_only_the_tests_an_edit_affects()
    {
        using var solution = new LiveSolution(visualBasic: true);
        var library = LiveSolution.VisualBasicLibrary;

        var summary = await solution.RunAsync();

        Assert.Empty(summary.FailedBuilds);
        Assert.Equal(4, summary.TestsRun);
        Assert.Contains(solution.Listener.Queued, t => t.Id == "Calc.Tests.ArithmeticTests.Adds");
        var add = solution.LibraryLine("Return a + b");
        Assert.Equal(LiveSolution.LineOf(library, "Return a + b"), add.Line);
        Assert.Equal(LineCoverageState.Covered, add.State);
        Assert.Equal(
            new[] { "Adds", "Adds_pairs(a: 1, b: 1, sum: 2)", "Adds_pairs(a: 2, b: 2, sum: 4)" },
            add.Tests.Select(t => t.Test.DisplayName));
        Assert.Equal(LineCoverageState.NotCovered, solution.LibraryLine("Return -value").State);
        var subtractLine = LiveSolution.LineOf(library, "Public Function Subtract") + 1;
        var file = solution.Engine.Index.GetFile(solution.LibraryFile)!;
        Assert.Equal(LineCoverageState.Failing, Assert.Single(file.Lines, l => l.Line == subtractLine).State);

        var fixedLibrary = library.Replace(
            "Subtract(a As Integer, b As Integer) As Integer\n        Return a + b",
            "Subtract(a As Integer, b As Integer) As Integer\n        Return a - b",
            StringComparison.Ordinal);
        solution.EditLibrary(fixedLibrary);
        await solution.RunAsync();
        Assert.Equal(new[] { "Subtracts" }, solution.QueuedMethods());
        Assert.Equal(LineCoverageState.Covered, solution.LibraryLine("Return a - b").State);

        solution.EditLibrary("' Arithmetic for the calculator.\n" + fixedLibrary);
        await solution.RunAsync();
        Assert.Empty(solution.QueuedMethods());
        Assert.Equal(LiveSolution.LineOf(library, "Return a + b") + 1, solution.LibraryLine("Return a + b").Line);

        solution.EditTests(LiveSolution.VisualBasicTests.Replace(
            "    <Theory>",
            "    <Fact>\n    Public Sub Negates()\n        Assert.True(Arithmetic.Negate(2) = -2)\n    End Sub\n\n    <Theory>",
            StringComparison.Ordinal));
        await solution.RunAsync();
        Assert.Equal(new[] { "Negates" }, solution.QueuedMethods());
        Assert.Equal(LineCoverageState.Covered, solution.LibraryLine("Return -value").State);
    }

    public void Dispose() => _solution.Dispose();
}
