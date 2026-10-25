using CommunityToolkit.Mvvm.Input;
using Microsoft.CodeAnalysis.CSharp;
using TabStudio.App.Services;
using TabStudio.App.ViewModels;
using TabStudio.Core.Workspaces;
using TabStudio.Testing;
using TabStudio.TestRunner;

namespace TabStudio.App.Tests.ViewModels;

/// <summary>
/// The Test Explorer's view model on its own, with a fake IDE: how it merges tests from source and builds, shows
/// results as they arrive, selects tests to run and groups them. No build or package restore is involved.
/// </summary>
public sealed class TestExplorerViewModelTests
{
    private const string TestFile = "/work/Calc.Tests/MathTests.cs";

    private const string Source = """
        using Xunit;

        namespace Calc.Tests;

        public class MathTests
        {
            [Fact]
            [Trait("Category", "Fast")]
            public void Adds() { }

            [Fact]
            public void Divides() { }

            [Theory]
            [MemberData(nameof(Rows))]
            public void Squares(int value) { }

            public static IEnumerable<object[]> Rows => [];
        }
        """;

    private static readonly ProjectInfo Project = new() { Name = "Calc.Tests", FilePath = "/work/Calc.Tests/Calc.Tests.csproj" };

    private readonly FakeHost _host = new();
    private readonly TestExplorerViewModel _explorer;

    public TestExplorerViewModelTests()
    {
        _explorer = new TestExplorerViewModel(_host, new ImmediateDispatcher());
        _explorer.SetProjects([Project]);
        _explorer.SetSourceTests(Project, Discover(Source));
    }

    [Fact]
    public void A_method_whose_rows_come_from_code_stands_for_all_of_them_until_a_build()
    {
        Assert.Equal(new[] { "Adds", "Divides", "Squares" }, _explorer.Entries.Select(e => e.DisplayName));
        Assert.True(_explorer.Entries.Single(e => e.MethodName == "Squares").IsPlaceholder);

        _explorer.SetBuiltTests(Project, [Case("Adds"), Case("Divides"), Case("Squares", "(2)"), Case("Squares", "(3)")]);

        Assert.Equal(new[] { "Adds", "Divides", "Squares(value: 2)", "Squares(value: 3)" }, _explorer.Entries.Select(e => e.DisplayName));
        Assert.Equal("(2)", _explorer.Rows.Single(r => r.Name == "Squares").CountText);
    }

    [Fact]
    public void Tests_written_after_the_build_appear_and_results_survive_rediscovery()
    {
        _explorer.SetBuiltTests(Project, [Case("Adds"), Case("Divides")]);
        _explorer.ReportFinished(Project.FilePath, Result("Adds", TestOutcome.Passed));

        _explorer.SetSourceTests(Project, Discover(Source.Replace("public void Divides() { }", "public void Divides() { }\n\n    [Fact]\n    public void Multiplies() { }", StringComparison.Ordinal)));

        Assert.Contains(_explorer.Entries, e => e.MethodName == "Multiplies" && e.State == TestState.NotRun);
        Assert.Equal(TestState.Passed, _explorer.Entries.Single(e => e.MethodName == "Adds").State);
        Assert.Equal((1, 3), (_explorer.PassedCount, _explorer.NotRunCount));
    }

    [Fact]
    public async Task Results_arrive_live_and_unfinished_tests_go_back_to_not_run()
    {
        var tests = new[] { Case("Adds"), Case("Divides") };
        _host.OnRun = _ =>
        {
            _explorer.SetBuiltTests(Project, tests);
            _explorer.ReportQueued(Project.FilePath, tests);
            Assert.All(_explorer.Entries.Where(e => e.MethodName != "Squares"), e => Assert.Equal(TestState.Queued, e.State));

            _explorer.ReportStarted(Project.FilePath, tests[0]);
            Assert.Equal(TestState.Running, _explorer.Rows.Single(r => r.Name == "MathTests").State);

            _explorer.ReportFinished(Project.FilePath, Result("Adds", TestOutcome.Failed, "Expected 4"));
            Assert.Equal(0.5, _explorer.Progress);
            Assert.Equal("Running 1 of 2 tests…", _explorer.RunStatusText);
            return Task.CompletedTask;
        };

        await _explorer.RunAllCommand.ExecuteAsync(null);

        Assert.Equal(TestState.Failed, _explorer.Entries.Single(e => e.MethodName == "Adds").State);
        Assert.Equal(TestState.NotRun, _explorer.Entries.Single(e => e.MethodName == "Divides").State);
        Assert.Equal(TestState.Failed, _explorer.Rows.Single(r => r.Name == "Calc.Tests" && r.Kind == TestNodeKind.Project).State);
        Assert.False(_explorer.IsRunning);
        Assert.Equal(new TestProjectSelection(Project.FilePath, null), Assert.Single(Assert.Single(_host.Requests).Projects));
    }

    [Fact]
    public async Task Each_row_runs_the_tests_under_it()
    {
        await RunAsync(_explorer.Rows.Single(r => r.Kind == TestNodeKind.Project));
        await RunAsync(_explorer.Rows.Single(r => r.Kind == TestNodeKind.Namespace));
        await RunAsync(_explorer.Rows.Single(r => r.Kind == TestNodeKind.Class));
        await RunAsync(_explorer.Rows.Single(r => r.Name == "Adds"));

        Assert.Equal(
            new[] { "*", "Calc.Tests.MathTests", "Calc.Tests.MathTests", "Calc.Tests.MathTests.Adds" },
            _host.Requests.Select(r => r.Projects.Single().Names is { } names ? string.Join(",", names) : "*"));
    }

    [Fact]
    public async Task Run_failed_runs_the_failed_tests_by_id()
    {
        _explorer.ReportFinished(Project.FilePath, Result("Divides", TestOutcome.Errored, "boom"));

        Assert.True(_explorer.RunFailedCommand.CanExecute(null));
        await _explorer.RunFailedCommand.ExecuteAsync(null);

        Assert.Equal(new[] { "Calc.Tests.MathTests.Divides" }, _host.Requests.Single().Projects.Single().Names);
        Assert.Equal("failed tests", _explorer.LastRequest?.Description);
    }

    [Fact]
    public void Saving_code_marks_results_stale_until_they_run_again()
    {
        _explorer.ReportFinished(Project.FilePath, Result("Adds", TestOutcome.Passed));

        _explorer.MarkStale([Project.FilePath]);

        var row = _explorer.Rows.Single(r => r.Name == "Adds");
        Assert.True(row.IsStale);
        Assert.Equal(0.45, row.IconOpacity);
        _explorer.SelectedNode = row;
        Assert.True(_explorer.Details?.IsStale);

        _explorer.ReportFinished(Project.FilePath, Result("Adds", TestOutcome.Passed));

        Assert.False(_explorer.Rows.Single(r => r.Name == "Adds").IsStale);
    }

    [Fact]
    public void Groups_by_traits_and_duration()
    {
        _explorer.ReportFinished(Project.FilePath, Result("Adds", TestOutcome.Passed, duration: TimeSpan.FromSeconds(2)));

        _explorer.SetGroupByCommand.Execute(TestGroupBy.Traits);

        Assert.Equal(new[] { "Category [Fast]", "No Traits" }, _explorer.Rows.Where(r => r.Depth == 0).Select(r => r.Name));

        _explorer.SetGroupByCommand.Execute(TestGroupBy.Duration);

        Assert.Equal(new[] { "Slow (> 1 sec)", "Not Run" }, _explorer.Rows.Where(r => r.Depth == 0).Select(r => r.Name));
        Assert.Equal("2.0 sec", _explorer.Rows[0].DurationText);
        Assert.Equal("Group By: Duration", _explorer.GroupByToolTip);
    }

    [Fact]
    public async Task The_details_of_a_failure_link_its_stack_frames_to_the_source()
    {
        using var temp = new TempDirectory();
        var file = temp.Write("MathTests.cs", "// The test's source.");
        _explorer.ReportFinished(Project.FilePath, Result("Adds", TestOutcome.Failed, "Expected 4", frames:
        [
            new TestStackFrame("Calc.Tests.MathTests.Adds()", file, 12, IsUserCode: true),
            new TestStackFrame("Xunit.Assert.Equal()", null, null, IsUserCode: false),
        ]));
        _explorer.SelectedNode = _explorer.Rows.Single(r => r.Name == "Adds");

        var details = _explorer.Details!;
        Assert.Equal("Failed · 5 ms", details.Summary);
        Assert.Equal("Expected 4", details.Message);
        Assert.Equal(new[] { true, false }, details.StackFrames.Select(f => f.CanOpen));
        await ((IAsyncRelayCommand)details.StackFrames[0].OpenCommand!).ExecuteAsync(null);
        Assert.Equal((file, 12, 1), Assert.Single(_host.Navigations));

        _explorer.SelectedNode = _explorer.Rows.Single(r => r.Kind == TestNodeKind.Class);

        Assert.Equal(new[] { "1 Failed", "2 Not run" }, _explorer.Details!.Outcomes.Select(o => o.Text));
    }

    private static List<SourceTestClass> Discover(string source) =>
        [.. SourceTestDiscoverer.Discover(CSharpSyntaxTree.ParseText(source, path: TestFile))];

    private static TestCase Case(string method, string? row = null) => new()
    {
        Id = "Calc.Tests.MathTests." + method + row,
        FullyQualifiedName = "Calc.Tests.MathTests." + method,
        DisplayName = row is null ? method : method + "(value: " + row[1..^1] + ")",
        Framework = TestFramework.XUnit,
        AssemblyName = "Calc.Tests",
        TypeFullName = "Calc.Tests.MathTests",
        MethodName = method,
        DataRow = row,
    };

    private static TestResult Result(string method, TestOutcome outcome, string? message = null, TimeSpan? duration = null, IReadOnlyList<TestStackFrame>? frames = null) => new()
    {
        Test = Case(method),
        Outcome = outcome,
        Duration = duration ?? TimeSpan.FromMilliseconds(5),
        Message = message,
        StackFrames = frames ?? [],
    };

    private static Task RunAsync(TestNodeViewModel node) => node.RunCommand.ExecuteAsync(null);

    /// <summary>An IDE that records what the Test Explorer asks of it.</summary>
    private sealed class FakeHost : ITestRunHost
    {
        public Func<TestRunRequest, Task>? OnRun { get; set; }

        public List<TestRunRequest> Requests { get; } = [];

        public List<(string Path, int Line, int Column)> Navigations { get; } = [];

        public bool CanStartTestRun => true;

        public Task RunTestsAsync(TestRunRequest request)
        {
            Requests.Add(request);
            return OnRun?.Invoke(request) ?? Task.CompletedTask;
        }

        public void CancelTestRun()
        {
        }

        public Task NavigateToAsync(string path, int line, int column)
        {
            Navigations.Add((path, line, column));
            return Task.CompletedTask;
        }
    }

    /// <summary>Runs posted work at once: these tests have no UI thread.</summary>
    private sealed class ImmediateDispatcher : IUiDispatcher
    {
        public bool CheckAccess() => true;

        public void Post(Action action) => action();

        public Task InvokeAsync(Action action)
        {
            action();
            return Task.CompletedTask;
        }
    }
}
