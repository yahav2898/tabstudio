using System.Diagnostics;
using System.Globalization;
using System.Text;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using TabStudio.App.Services;
using TabStudio.App.Tests.Extensions;
using TabStudio.App.ViewModels;
using TabStudio.App.Views;
using TabStudio.Core.Workspaces;
using TabStudio.TestRunner;

namespace TabStudio.App.Tests.Performance;

/// <summary>
/// Run All on a test project with 5,000 quick tests, Test Explorer shown: the runner reports each start and result from
/// its own threads, as the workspace passes them on, and the explorer must keep up without freezing the IDE. Writes
/// <c>smoothness-test-explorer.md</c>.
/// </summary>
[Collection(ExtensionsTestGroup.Name)]
public sealed class TestExplorerSmoothnessTests
{
    private const int Namespaces = 10;
    private const int ClassesPerNamespace = 10;
    private const int TestsPerClass = 50;
    private const int Tests = Namespaces * ClassesPerNamespace * TestsPerClass;

    private static readonly ProjectInfo Project = new() { Name = "Big.Tests", FilePath = "/work/Big.Tests/Big.Tests.csproj" };

    [AvaloniaFact]
    public async Task Five_thousand_results_arriving_one_by_one_leave_the_IDE_responsive()
    {
        if (Smoothness.IsLightPass)
        {
            return;
        }

        var cases = Enumerable.Range(0, Tests).Select(Case).ToList();
        var (beforeTime, beforeStalls, _) = await RunAllAsync(cases, batched: false);
        var (afterTime, afterStalls, discovery) = await RunAllAsync(cases, batched: true);

        var report = new StringBuilder("# Test Explorer: 5,000 results arriving one by one\n\n");
        var culture = CultureInfo.InvariantCulture;
        report.AppendLine(culture, $"- {Namespaces} namespaces, {ClassesPerNamespace} classes in each, {TestsPerClass} tests in each class; Test Explorer shown in a 1280 × 800 window.");
        report.AppendLine(culture, $"- Discovery (the build's 5,000 tests arrive): {discovery}.\n");
        report.AppendLine("| Run All, the runner reporting from its threads | From the first start to the last result shown | The UI thread meanwhile |");
        report.AppendLine("| --- | --- | --- |");
        report.AppendLine(culture, $"| Each start and result posted on its own, as before 0.21.7 | {beforeTime.TotalSeconds:0.00} s | {beforeStalls} |");
        report.AppendLine(culture, $"| Queued and shown in batches | {afterTime.TotalSeconds:0.00} s | {afterStalls} |");
        Smoothness.Write("test-explorer", report.ToString());
    }

    /// <summary>Run All with every test passing, reported as the workspace reported them before 0.21.7 or as it does now.</summary>
    private static async Task<(TimeSpan Ran, string Stalls, UiCost Discovery)> RunAllAsync(List<TestCase> cases, bool batched)
    {
        var ui = new AvaloniaUiDispatcher();
        var host = new RunHost();
        var explorer = new TestExplorerViewModel(host, ui);
        explorer.SetProjects([Project]);
        var window = new Window { Width = 1280, Height = 800, Content = new TestExplorerView { DataContext = explorer } };
        window.Show();
        var discovery = Smoothness.Measure(window, () => explorer.SetBuiltTests(Project, cases));
        Assert.Equal(Tests, explorer.TotalCount);
        host.OnRun = async _ =>
        {
            explorer.ReportQueued(Project.FilePath, cases);
            await Task.Run(() =>
            {
                foreach (var test in cases)
                {
                    var result = new TestResult { Test = test, Outcome = TestOutcome.Passed, Duration = TimeSpan.FromMilliseconds(1) };
                    if (batched)
                    {
                        explorer.QueueStarted(Project.FilePath, test);
                        explorer.QueueFinished(Project.FilePath, result);
                    }
                    else
                    {
                        ui.Post(() => explorer.ReportStarted(Project.FilePath, test));
                        ui.Post(() => explorer.ReportFinished(Project.FilePath, result));
                    }
                }
            });
            await UiTestHelpers.WaitUntilAsync(() => explorer.PassedCount == Tests, TimeSpan.FromMinutes(5), "every result");
        };

        using var stalls = new UiStallMonitor(Dispatcher.UIThread, TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(4));
        var clock = Stopwatch.StartNew();
        await explorer.RunAllCommand.ExecuteAsync(null);
        var ran = clock.Elapsed;
        stalls.Dispose();
        Assert.Equal(Tests, explorer.PassedCount);
        window.Close();
        return (ran, stalls.Summary(), discovery);
    }

    private static TestCase Case(int index)
    {
        var ns = string.Create(CultureInfo.InvariantCulture, $"Big.Tests.Area{index / (ClassesPerNamespace * TestsPerClass)}");
        var type = string.Create(CultureInfo.InvariantCulture, $"{ns}.Feature{index / TestsPerClass % ClassesPerNamespace}Tests");
        var method = string.Create(CultureInfo.InvariantCulture, $"Behaves_{index % TestsPerClass}");
        return new TestCase
        {
            Id = type + "." + method,
            FullyQualifiedName = type + "." + method,
            DisplayName = method,
            Framework = TestFramework.XUnit,
            AssemblyName = "Big.Tests",
            TypeFullName = type,
            MethodName = method,
        };
    }

    /// <summary>An IDE whose run reports what the test sets up.</summary>
    private sealed class RunHost : ITestRunHost
    {
        public Func<TestRunRequest, Task>? OnRun { get; set; }

        public bool CanStartTestRun => true;

        public Task RunTestsAsync(TestRunRequest request) => OnRun?.Invoke(request) ?? Task.CompletedTask;

        public void CancelTestRun()
        {
        }

        public Task NavigateToAsync(string path, int line, int column) => Task.CompletedTask;
    }
}
