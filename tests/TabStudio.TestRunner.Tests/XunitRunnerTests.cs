namespace TabStudio.TestRunner.Tests;

public sealed class XunitRunnerTests
{
    private const string BasicSource = """
        using Xunit;

        namespace Calculator.Tests;

        public class BasicTests
        {
            [Fact]
            public void Passes() => Assert.Equal(4, 2 + 2);

            [Fact]
            public void Fails() => Assert.Equal(5, 2 + 2);

            [Fact(Skip = "Not ready")]
            public void Skipped() { }

            [Fact]
            public void Throws() => throw new InvalidOperationException("boom");

            [Fact(DisplayName = "A friendly name")]
            [Trait("Category", "Fast")]
            public void Named() { }

            [Fact]
            public async Task FailsAfterAwait()
            {
                await Task.Delay(10);
                Assert.True(false, "after await");
            }
        }
        """;

    private const string TheorySource = """
        using System.Collections;
        using Xunit;

        namespace Calculator.Tests;

        public class TheoryTests
        {
            public static IEnumerable<object[]> Pairs => new[] { new object[] { 1, 1, 2 }, new object[] { 2, 3, 5 } };

            public static IEnumerable<object[]> Scaled(int factor) => new[] { new object[] { factor, factor * 2 } };

            [Theory]
            [InlineData(1, 2, 3)]
            [InlineData(-1, 1, 0)]
            public void AddsInts(int a, int b, int sum) => Assert.Equal(sum, a + b);

            [Theory]
            [InlineData(1, 2.5, "3.5")]
            public void ConvertsArguments(long a, double b, decimal sum) => Assert.Equal(sum, a + (decimal)b);

            [Theory]
            [MemberData(nameof(Pairs))]
            public void AddsPairs(int a, int b, int sum) => Assert.Equal(sum, a + b);

            [Theory]
            [MemberData(nameof(Scaled), 3)]
            public void Doubles(int value, int doubled) => Assert.Equal(doubled, value * 2);

            [Theory]
            [ClassData(typeof(SquareData))]
            public void Squares(int value, int square) => Assert.Equal(square, value * value);

            [Theory]
            [InlineData(1)]
            [InlineData(2)]
            public void FailsForTwo(int value) => Assert.NotEqual(2, value);
        }

        public class SquareData : IEnumerable<object[]>
        {
            public IEnumerator<object[]> GetEnumerator()
            {
                yield return new object[] { 3, 9 };
                yield return new object[] { 4, 16 };
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }
        """;

    private const string LifecycleSource = """
        using Xunit;
        using Xunit.Abstractions;

        namespace Lifecycle.Tests;

        public sealed class Counter : IDisposable
        {
            public static int Created;

            public Counter() => Created++;

            public void Dispose() { }
        }

        public class FixtureTests : IClassFixture<Counter>, IAsyncLifetime, IDisposable
        {
            private readonly ITestOutputHelper _output;

            public FixtureTests(Counter counter, ITestOutputHelper output)
            {
                _output = output;
                Console.WriteLine("constructor");
            }

            public Task InitializeAsync()
            {
                Console.WriteLine("initialize");
                return Task.CompletedTask;
            }

            public Task DisposeAsync()
            {
                Console.WriteLine("dispose async");
                return Task.CompletedTask;
            }

            public void Dispose() => Console.WriteLine("dispose");

            [Fact]
            public async Task First()
            {
                await Task.Delay(20);
                _output.WriteLine("first via helper");
                Console.WriteLine("first via console");
                Assert.Equal(1, Counter.Created);
            }

            [Fact]
            public void Second()
            {
                _output.WriteLine("second {0}", 2);
                Assert.Equal(1, Counter.Created);
            }
        }
        """;

    private const string SlowSource = """
        using Xunit;

        namespace Slow.Tests;

        public class SlowTests
        {
            [Fact]
            public void Hangs() => Thread.Sleep(Timeout.Infinite);

            [Fact]
            public void Quick() { }
        }
        """;

    [Fact]
    public void Discovers_facts_with_names_traits_and_locations()
    {
        using var session = UserProject.Load(BasicSource, UserProject.XunitAssemblies);

        Assert.Equal(6, session.Tests.Count);
        var passes = Assert.Single(session.Tests, t => t.MethodName == "Passes");
        Assert.Equal("Calculator.Tests.BasicTests.Passes", passes.Id);
        Assert.Equal("Calculator.Tests.BasicTests.Passes", passes.FullyQualifiedName);
        Assert.Equal("Calculator.Tests", passes.NamespaceName);
        Assert.Equal("BasicTests", passes.TypeName);
        Assert.Equal(TestFramework.XUnit, passes.Framework);
        Assert.Equal(UserProject.FilePath, passes.Location?.FilePath);
        Assert.Equal(UserProject.LineOf(BasicSource, "Assert.Equal(4, 2 + 2)"), passes.Location?.Line);

        var named = Assert.Single(session.Tests, t => t.MethodName == "Named");
        Assert.Equal("A friendly name", named.DisplayName);
        Assert.Equal(new TestTrait("Category", "Fast"), Assert.Single(named.Traits));
        Assert.Equal("Not ready", Assert.Single(session.Tests, t => t.MethodName == "Skipped").SkipReason);
    }

    [Fact]
    public async Task Reports_passed_failed_skipped_and_errored_tests()
    {
        var summary = await UserProject.RunAllAsync(BasicSource, UserProject.XunitAssemblies);

        Assert.Equal(6, summary.Total);
        Assert.Equal(TestOutcome.Passed, summary.Result("Passes").Outcome);
        Assert.Equal(TestOutcome.Passed, summary.Result("A friendly name").Outcome);

        var failed = summary.Result("Fails");
        Assert.Equal(TestOutcome.Failed, failed.Outcome);
        Assert.Contains("Assert.Equal() Failure", failed.Message, StringComparison.Ordinal);
        Assert.Equal("Xunit.Sdk.EqualException", failed.ExceptionType);

        var skipped = summary.Result("Skipped");
        Assert.Equal(TestOutcome.Skipped, skipped.Outcome);
        Assert.Equal("Not ready", skipped.Message);

        var errored = summary.Result("Throws");
        Assert.Equal(TestOutcome.Errored, errored.Outcome);
        Assert.Equal("System.InvalidOperationException: boom", errored.Message);

        Assert.Equal(2, summary.Passed);
        Assert.Equal(2, summary.Failed);
        Assert.Equal(1, summary.Skipped);
        Assert.Equal(1, summary.Errored);
        Assert.Equal(0, summary.NotRun);
        Assert.False(summary.Succeeded);
    }

    [Fact]
    public async Task Stack_traces_are_trimmed_to_user_code_and_point_at_the_failing_line()
    {
        var summary = await UserProject.RunAllAsync(BasicSource, UserProject.XunitAssemblies);

        var failed = summary.Result("Fails");
        Assert.Equal(new TestSourceLocation(UserProject.FilePath, UserProject.LineOf(BasicSource, "Assert.Equal(5, 2 + 2)")), failed.FailureLocation);
        Assert.True(failed.StackFrames[0].IsUserCode);
        Assert.StartsWith("Calculator.Tests.BasicTests.Fails()", failed.StackFrames[0].Method, StringComparison.Ordinal);
        Assert.DoesNotContain("Xunit.Assert", failed.StackTrace, StringComparison.Ordinal);
        Assert.DoesNotContain("TabStudio.TestRunner", failed.StackTrace, StringComparison.Ordinal);
        Assert.Contains($"{UserProject.FilePath}:line {failed.FailureLocation!.Line}", failed.StackTrace, StringComparison.Ordinal);

        var afterAwait = summary.Result("FailsAfterAwait");
        Assert.Equal(TestOutcome.Failed, afterAwait.Outcome);
        Assert.Contains("after await", afterAwait.Message, StringComparison.Ordinal);
        Assert.Equal(UserProject.LineOf(BasicSource, "Assert.True(false, \"after await\")"), afterAwait.FailureLocation?.Line);
    }

    [Fact]
    public async Task Theories_run_every_row_of_inline_member_and_class_data()
    {
        using var session = UserProject.Load(TheorySource, UserProject.XunitAssemblies);
        var summary = await session.RunAllAsync();

        Assert.Equal(10, summary.Total);
        Assert.Equal(9, summary.Passed);
        var failed = Assert.Single(summary.Results, r => r.Outcome == TestOutcome.Failed);
        Assert.Equal("FailsForTwo(value: 2)", failed.Test.DisplayName);
        Assert.Equal("Calculator.Tests.TheoryTests.FailsForTwo(2)", failed.Test.Id);
        Assert.Equal("(2)", failed.Test.DataRow);

        Assert.Equal(
            new[] { "Calculator.Tests.TheoryTests.AddsInts(1, 2, 3)", "Calculator.Tests.TheoryTests.AddsInts(-1, 1, 0)" },
            session.Tests.Where(t => t.MethodName == "AddsInts").Select(t => t.Id));
        Assert.Equal("AddsInts(a: 1, b: 2, sum: 3)", session.Tests.First(t => t.MethodName == "AddsInts").DisplayName);
        Assert.Equal(2, session.Tests.Count(t => t.MethodName == "AddsPairs"));
        Assert.Equal("Doubles(value: 3, doubled: 6)", Assert.Single(session.Tests, t => t.MethodName == "Doubles").DisplayName);
        Assert.Equal(2, session.Tests.Count(t => t.MethodName == "Squares"));
        Assert.Equal(TestOutcome.Passed, summary.Result("ConvertsArguments(a: 1, b: 2.5, sum: \"3.5\")").Outcome);
    }

    [Fact]
    public async Task Class_fixtures_async_lifetime_and_output_helpers_follow_xunit_rules()
    {
        var summary = await UserProject.RunAllAsync(LifecycleSource, UserProject.XunitAssemblies);

        Assert.True(summary.Succeeded, string.Join('\n', summary.Results.Select(r => $"{r.Test.DisplayName}: {r.Message}")));
        Assert.Equal(
            "constructor\ninitialize\nfirst via helper\nfirst via console\ndispose async\ndispose\n",
            summary.Result("First").Output.ReplaceLineEndings("\n"));
        Assert.Equal(
            "constructor\ninitialize\nsecond 2\ndispose async\ndispose\n",
            summary.Result("Second").Output.ReplaceLineEndings("\n"));
    }

    [Fact]
    public async Task A_test_that_runs_past_its_timeout_is_abandoned_and_reported_as_errored()
    {
        var options = new TestRunOptions { DefaultTimeout = TimeSpan.FromMilliseconds(300) };

        var summary = await UserProject.RunAllAsync(SlowSource, UserProject.XunitAssemblies, options);

        var hung = summary.Result("Hangs");
        Assert.Equal(TestOutcome.Errored, hung.Outcome);
        Assert.Contains("timeout", hung.Message, StringComparison.Ordinal);
        Assert.True(hung.Duration < TimeSpan.FromSeconds(10));
        Assert.Equal(TestOutcome.Passed, summary.Result("Quick").Outcome);
    }

    [Fact]
    public async Task Stopping_a_run_reports_the_running_test_as_not_run()
    {
        using var session = UserProject.Load(SlowSource, UserProject.XunitAssemblies, new TestRunOptions { DefaultTimeout = null });
        using var stop = new CancellationTokenSource();
        session.TestStarted += (_, e) =>
        {
            if (e.Test.MethodName == "Hangs")
            {
                stop.CancelAfter(100);
            }
        };

        var summary = await session.RunAllAsync(stop.Token);

        Assert.True(summary.WasCancelled);
        Assert.Equal(TestOutcome.NotRun, Assert.Single(summary.Results).Outcome);
        Assert.Equal(2, summary.NotRun);
        Assert.False(session.IsRunning);
    }

    [Fact]
    public async Task Run_selected_and_run_failed_run_only_those_tests()
    {
        using var session = UserProject.Load(TheorySource, UserProject.XunitAssemblies);

        var selected = await session.RunSelectedAsync(["Calculator.Tests.TheoryTests.AddsInts", "Calculator.Tests.TheoryTests.Squares(3, 9)"]);
        Assert.Equal(3, selected.Total);
        Assert.True(selected.Succeeded);

        await session.RunAllAsync();
        var failed = await session.RunFailedAsync();
        Assert.Equal("Calculator.Tests.TheoryTests.FailsForTwo(2)", Assert.Single(failed.Results).Test.Id);

        var byClass = session.FindTests(["Calculator.Tests.TheoryTests"]);
        Assert.Equal(10, byClass.Count);
        Assert.Equal(10, session.FindTests(["Calculator"]).Count);
    }

    [Fact]
    public async Task Events_report_every_test_and_the_run()
    {
        using var session = UserProject.Load(BasicSource, UserProject.XunitAssemblies);
        var started = new List<string>();
        var finished = new List<TestResult>();
        TestRunSummary? reported = null;
        session.TestStarted += (_, e) => { lock (started) { started.Add(e.Test.Id); } };
        session.TestFinished += (_, e) => { lock (finished) { finished.Add(e.Result); } };
        session.RunFinished += (_, e) => reported = e.Summary;

        var summary = await session.RunAllAsync();

        Assert.Equal(6, started.Count);
        Assert.Equal(started.Order(StringComparer.Ordinal), finished.Select(r => r.Test.Id).Order(StringComparer.Ordinal));
        Assert.Same(summary, reported);
        Assert.Equal(6, session.LatestResults.Count);
    }

    [Fact]
    public async Task Classes_can_run_in_parallel_with_output_kept_per_test()
    {
        var source = """
            using Xunit;

            namespace Parallel.Tests;

            public class A
            {
                [Fact] public async Task One() { Console.WriteLine("A1"); await Task.Delay(50); Console.WriteLine("A1 done"); }
                [Fact] public void Two() => Console.WriteLine("A2");
            }

            public class B
            {
                [Fact] public async Task One() { Console.WriteLine("B1"); await Task.Delay(50); Console.WriteLine("B1 done"); }
            }
            """;

        var summary = await UserProject.RunAllAsync(source, UserProject.XunitAssemblies, new TestRunOptions { MaxParallelClasses = 2 });

        Assert.True(summary.Succeeded);
        Assert.Equal("A1\nA1 done\n", Assert.Single(summary.Results, r => r.Test.Id == "Parallel.Tests.A.One").Output.ReplaceLineEndings("\n"));
        Assert.Equal("B1\nB1 done\n", Assert.Single(summary.Results, r => r.Test.Id == "Parallel.Tests.B.One").Output.ReplaceLineEndings("\n"));
        Assert.Equal("A2\n", Assert.Single(summary.Results, r => r.Test.Id == "Parallel.Tests.A.Two").Output.ReplaceLineEndings("\n"));
    }

    [Fact]
    public async Task The_console_is_restored_after_a_run()
    {
        var before = Console.Out;

        await UserProject.RunAllAsync(BasicSource, UserProject.XunitAssemblies);

        Assert.Same(before, Console.Out);
    }
}
