using Avalonia.Headless.XUnit;
using CommunityToolkit.Mvvm.Input;
using TabStudio.App.Platform;
using TabStudio.App.Services;
using TabStudio.App.ViewModels;
using TabStudio.Testing;

namespace TabStudio.App.Tests.ViewModels;

/// <summary>
/// The Test Explorer against a real xUnit project, restored from nuget.org, built through the IDE's build and run
/// in-process. The tests of this class share one packages folder, so xUnit is downloaded once.
/// </summary>
public sealed class TestExplorerTests : IClassFixture<SharedToolchain>, IDisposable
{
    private readonly SharedToolchain _toolchain;
    private readonly TempDirectory _temp = new();
    private readonly List<WorkspaceViewModel> _workspaces = [];

    public TestExplorerTests(SharedToolchain toolchain)
    {
        _toolchain = toolchain;
    }

    private string TestFile => _temp.Resolve(CalculatorSolution.TestFile);

    [AvaloniaFact]
    public async Task Lists_the_tests_from_source_before_the_first_build()
    {
        var workspace = await OpenAsync();
        var tests = workspace.Tests;

        Assert.Equal(
            new[] { "Calculator.Tests", "Calculator.Tests", "ArithmeticTests", "Adds", "Subtracts_with_a_mistake", "Divides_by_zero", "Adds_pairs" },
            tests.Rows.Select(r => r.Name));
        Assert.Equal(5, tests.NotRunCount);
        Assert.All(tests.Rows, r => Assert.Equal(TestState.NotRun, r.State));

        var pairs = tests.Rows.Single(r => r.Name == "Adds_pairs");
        Assert.Equal("(2)", pairs.CountText);
        tests.Toggle(pairs);
        Assert.Contains(tests.Rows, r => r.Name == "Adds_pairs(a: 1, b: 2, sum: 3)");
        Assert.Contains(tests.Rows, r => r.Name == "Adds_pairs(a: -1, b: 1, sum: 0)");
    }

    [AvaloniaFact]
    public async Task Run_all_shows_passed_failed_and_skipped_tests_with_the_failure_location()
    {
        var workspace = await OpenAsync();
        var tests = workspace.Tests;

        await tests.RunAllCommand.ExecuteAsync(null);

        Assert.Equal((3, 1, 1, 0), (tests.PassedCount, tests.FailedCount, tests.SkippedCount, tests.NotRunCount));
        Assert.Contains("3 passed, 1 failed, 1 skipped", tests.RunStatusText, StringComparison.Ordinal);
        Assert.Contains("========== Test run finished: 5 Tests (3 Passed, 1 Failed, 1 Skipped)", workspace.Output.Text, StringComparison.Ordinal);
        Assert.Equal(IdeState.Ready, workspace.State);
        Assert.True(workspace.IsTestExplorerSelected);

        var failed = tests.Entries.Single(e => e.MethodName == "Subtracts_with_a_mistake");
        Assert.Equal(TestState.Failed, failed.State);
        Assert.Equal(TestFile, failed.Result?.FailureLocation?.FilePath);
        Assert.Equal(CalculatorSolution.FailingLine, failed.Result?.FailureLocation?.Line);

        tests.SelectedNode = tests.Rows.Single(r => r.Entry == failed);
        var details = tests.Details!;
        Assert.Equal(TestState.Failed, details.State);
        Assert.Contains("Assert.Equal() Failure", details.Message, StringComparison.Ordinal);
        Assert.Contains("Checking 5 - 3", details.Output, StringComparison.Ordinal);
        Assert.Equal("ArithmeticTests.cs, line " + CalculatorSolution.LineOf("Subtracts_with_a_mistake"), details.SourceText);

        var frame = Assert.Single(details.StackFrames, f => f.CanOpen);
        Assert.True(frame.IsUserCode);
        Assert.Equal($"ArithmeticTests.cs, line {CalculatorSolution.FailingLine}", frame.Location);
        await ((IAsyncRelayCommand)frame.OpenCommand!).ExecuteAsync(null);
        Assert.Equal(TestFile, workspace.ActiveDocument?.FilePath);
        Assert.Equal((CalculatorSolution.FailingLine, 1), workspace.ActiveDocument?.PendingNavigation);
    }

    [AvaloniaFact]
    public async Task Run_failed_and_repeat_last_run_run_only_the_tests_that_failed()
    {
        var workspace = await OpenAsync();
        var tests = workspace.Tests;
        await tests.RunAllCommand.ExecuteAsync(null);
        var passed = tests.Entries.Single(e => e.MethodName == "Adds").Result;
        var firstFailure = tests.Entries.Single(e => e.MethodName == "Subtracts_with_a_mistake").Result;

        await tests.RunFailedCommand.ExecuteAsync(null);

        Assert.Contains("1 Tests (0 Passed, 1 Failed, 0 Skipped)", workspace.Output.Text, StringComparison.Ordinal);
        Assert.Same(passed, tests.Entries.Single(e => e.MethodName == "Adds").Result);
        var failed = tests.Entries.Single(e => e.MethodName == "Subtracts_with_a_mistake");
        Assert.NotSame(firstFailure, failed.Result);
        Assert.Equal(TestState.Failed, failed.State);
        Assert.Equal((3, 1, 1), (tests.PassedCount, tests.FailedCount, tests.SkippedCount));

        await tests.RepeatLastRunCommand.ExecuteAsync(null);

        Assert.Contains("1 Tests (0 Passed, 1 Failed, 0 Skipped)", workspace.Output.Text, StringComparison.Ordinal);
        Assert.Equal("failed tests", tests.LastRequest?.Description);
    }

    [AvaloniaFact]
    public async Task A_build_error_stops_the_run_and_shows_the_Error_List()
    {
        var workspace = await OpenAsync();
        var document = (await workspace.OpenDocumentAsync(TestFile))!;
        document.Document.Insert(document.Document.Text.IndexOf("public class", StringComparison.Ordinal), "this does not compile;\n");

        await workspace.Tests.RunAllCommand.ExecuteAsync(null);

        Assert.Equal(IdeState.Failed, workspace.State);
        Assert.Equal(BottomPanel.ErrorList, workspace.SelectedPanel);
        Assert.True(workspace.ErrorList.ErrorCount > 0);
        Assert.All(workspace.Tests.Entries, e => Assert.Equal(TestState.NotRun, e.State));
        Assert.StartsWith("The build failed", workspace.Tests.RunStatusText, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task Saving_marks_results_stale_and_lists_new_tests()
    {
        var workspace = await OpenAsync();
        var tests = workspace.Tests;
        await tests.RunAllCommand.ExecuteAsync(null);
        var document = (await workspace.OpenDocumentAsync(TestFile))!;

        document.Document.Insert(document.Document.Text.LastIndexOf('}'), "\n    [Fact]\n    public void Multiplies() => Assert.Equal(6, 2 * 3);\n");
        await workspace.SaveCommand.ExecuteAsync(null);
        await UiTestHelpers.WaitUntilAsync(() => tests.TotalCount == 6, because: "the new test");

        Assert.Equal(TestState.NotRun, tests.Entries.Single(e => e.MethodName == "Multiplies").State);
        Assert.All(tests.Entries.Where(e => e.Result is not null), e => Assert.True(e.IsStale));
        Assert.True(tests.Rows.Single(r => r.Name == "ArithmeticTests").IsStale);

        await tests.RunAllCommand.ExecuteAsync(null);

        Assert.Equal((4, 1, 1), (tests.PassedCount, tests.FailedCount, tests.SkippedCount));
        Assert.DoesNotContain(tests.Entries, e => e.IsStale);
    }

    [AvaloniaFact]
    public async Task Run_tests_in_context_runs_the_test_at_the_caret()
    {
        var workspace = await OpenAsync();
        var document = (await workspace.OpenDocumentAsync(TestFile))!;
        document.CaretOffset = document.Document.Text.IndexOf("Adds_pairs", StringComparison.Ordinal);

        await workspace.RunTestsInContextCommand.ExecuteAsync(null);

        var ran = workspace.Tests.Entries.Where(e => e.Result is not null).Select(e => e.DisplayName).Order(StringComparer.Ordinal);
        Assert.Equal(new[] { "Adds_pairs(a: -1, b: 1, sum: 0)", "Adds_pairs(a: 1, b: 2, sum: 3)" }, ran);
        Assert.Equal("Adds_pairs", workspace.Tests.LastRequest?.Description);
    }

    [AvaloniaFact]
    public async Task Grouping_by_outcome_and_searching_rearrange_the_tree()
    {
        var workspace = await OpenAsync();
        var tests = workspace.Tests;
        await tests.RunAllCommand.ExecuteAsync(null);

        tests.SetGroupByCommand.Execute(TestGroupBy.Outcome);

        Assert.Equal(new[] { "Failed Tests", "Skipped Tests", "Passed Tests" }, tests.Rows.Where(r => r.Depth == 0).Select(r => r.Name));
        Assert.Single(tests.GroupByOptions, o => o.IsSelected && o.Value == TestGroupBy.Outcome);

        tests.SearchText = "pairs";

        Assert.Equal(new[] { "Passed Tests", "ArithmeticTests", "Adds_pairs", "Adds_pairs(a: 1, b: 2, sum: 3)", "Adds_pairs(a: -1, b: 1, sum: 0)" }, tests.Rows.Select(r => r.Name));

        tests.SearchText = string.Empty;
        tests.FilterCommand.Execute(TestState.Failed);

        Assert.Equal(new[] { "Failed Tests", "ArithmeticTests", "Subtracts_with_a_mistake" }, tests.Rows.Select(r => r.Name));
        Assert.True(tests.IsShowingFailed);
    }

    public void Dispose()
    {
        foreach (var workspace in _workspaces)
        {
            workspace.Dispose();
        }

        _temp.Dispose();
    }

    private async Task<WorkspaceViewModel> OpenAsync()
    {
        var device = new DefaultDeviceStatus(new DeviceInfo(8L * 1024 * 1024 * 1024, 8, IsLowRamDevice: false));
        var workspace = new WorkspaceViewModel(
            _toolchain.Compiler, device, DeviceProfile.For(device.Device), new AvaloniaUiDispatcher(), new SessionStore(_toolchain.Paths), _toolchain.Paths);
        _workspaces.Add(workspace);

        await workspace.LoadAsync(CalculatorSolution.Write(_temp));
        await UiTestHelpers.WaitUntilAsync(() => workspace.Tests.TotalCount == 5, because: "the tests found in source");
        return workspace;
    }
}
