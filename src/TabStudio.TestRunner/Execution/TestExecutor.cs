using System.Diagnostics;
using System.Globalization;
using TabStudio.Compilation.Running;

namespace TabStudio.TestRunner.Execution;

/// <summary>
/// Runs a list of tests: class by class, each test on its own thread, with timeouts, cancellation, console
/// capture and progress callbacks.
/// </summary>
internal sealed class TestExecutor
{
    private readonly TestRunOptions _options;
    private readonly StackTraceCleaner _stackTraces;
    private readonly Action<TestCase> _started;
    private readonly Action<TestResult> _finished;
    private readonly Lock _gate = new();

    /// <summary>Creates an executor.</summary>
    /// <param name="options">How to run.</param>
    /// <param name="stackTraces">Trims stack traces to the user's code.</param>
    /// <param name="started">Called when a test starts, on a runner thread.</param>
    /// <param name="finished">Called when a test finishes, on a runner thread.</param>
    public TestExecutor(TestRunOptions options, StackTraceCleaner stackTraces, Action<TestCase> started, Action<TestResult> finished)
    {
        _options = options;
        _stackTraces = stackTraces;
        _started = started;
        _finished = finished;
    }

    /// <summary>Runs tests and returns the summary.</summary>
    /// <param name="tests">The tests, in the order to run them.</param>
    /// <param name="cancellationToken">Stops the run; the test in progress is abandoned and reported as not run.</param>
    public async Task<TestRunSummary> RunAsync(IReadOnlyList<RunnableTest> tests, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var results = new List<TestResult>();
        var messages = new List<string>();

        ConsoleLease console;
        try
        {
            console = await ProcessConsole.AcquireAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return Summary(tests.Count, results, messages, stopwatch, cancelled: true);
        }

        using (console)
        {
            ConsoleCapture.Redirect(console);
            var scope = new RunScope(_options);
            var batches = Batch(tests);
            if (_options.MaxParallelClasses <= 1 || batches.Count == 1)
            {
                foreach (var batch in batches)
                {
                    await RunBatchAsync(batch, scope, results, messages, cancellationToken).ConfigureAwait(false);
                }
            }
            else
            {
                using var throttle = new SemaphoreSlim(_options.MaxParallelClasses);
                await Task.WhenAll(batches.Select(async batch =>
                {
                    await throttle.WaitAsync(CancellationToken.None).ConfigureAwait(false);
                    try
                    {
                        await RunBatchAsync(batch, scope, results, messages, cancellationToken).ConfigureAwait(false);
                    }
                    finally
                    {
                        throttle.Release();
                    }
                })).ConfigureAwait(false);
            }

            var finish = await TestThread.RunAsync(scope.Finish, "TabStudio test cleanup", _options.StackSize, null, _options.DefaultTimeout, CancellationToken.None).ConfigureAwait(false);
            lock (_gate)
            {
                messages.AddRange(finish.Value ?? []);
                if (finish.Status == ThreadRunStatus.TimedOut)
                {
                    messages.Add("The run's final cleanup did not finish within the timeout.");
                }
            }
        }

        return Summary(tests.Count, results, messages, stopwatch, cancellationToken.IsCancellationRequested);
    }

    /// <summary>
    /// Groups tests into batches that run one after another: one batch per class, except that xUnit classes of
    /// the same collection share a batch, so they never run in parallel.
    /// </summary>
    private static List<List<List<RunnableTest>>> Batch(IReadOnlyList<RunnableTest> tests)
    {
        var classes = tests
            .GroupBy(t => (t.Adapter, t.TestClass))
            .Select(g => g.ToList())
            .ToList();

        return classes
            .GroupBy(c => c[0].CollectionName is { } collection ? (object)(c[0].TestClass.Assembly, collection) : c[0].TestClass)
            .Select(g => g.ToList())
            .ToList();
    }

    private async Task RunBatchAsync(List<List<RunnableTest>> batch, RunScope scope, List<TestResult> results, List<string> messages, CancellationToken cancellationToken)
    {
        foreach (var classTests in batch)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            await RunClassAsync(classTests, scope, results, messages, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task RunClassAsync(List<RunnableTest> tests, RunScope scope, List<TestResult> results, List<string> messages, CancellationToken cancellationToken)
    {
        var first = tests[0];
        var testClass = first.TestClass;
        var className = testClass.FullName ?? testClass.Name;
        var lifecycle = first.Adapter.CreateLifecycle(testClass, scope);

        var runnable = tests.Where(t => t.Case.SkipReason is null && t.DiscoveryError is null).ToList();
        StageFailure? setUpFailure = null;
        var setUpRan = false;
        if (runnable.Count > 0)
        {
            var setUp = await TestThread.RunAsync(lifecycle.SetUpClass, $"TabStudio: {className} setup", _options.StackSize, null, _options.DefaultTimeout, cancellationToken).ConfigureAwait(false);
            setUpRan = true;
            setUpFailure = setUp.Status switch
            {
                ThreadRunStatus.Completed => setUp.Value,
                ThreadRunStatus.Faulted => new StageFailure("Class setup", setUp.Exception!),
                ThreadRunStatus.TimedOut => new StageFailure("Class setup", new TimeoutException($"The class setup did not finish within {Describe(_options.DefaultTimeout)}.")),
                _ => null,
            };
        }

        foreach (var test in tests)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            if (test.Case.SkipReason is { } skipReason)
            {
                Report(results, test.Case, new TestStepOutcome(TestOutcome.Skipped, skipReason), TimeSpan.Zero, string.Empty, announce: true);
            }
            else if (test.DiscoveryError is { } discoveryError)
            {
                Report(results, test.Case, new TestStepOutcome(TestOutcome.Errored, discoveryError), TimeSpan.Zero, string.Empty, announce: true);
            }
            else if (setUpFailure is not null)
            {
                var failures = new FailureCollector();
                failures.Add(setUpFailure.Exception, setUpFailure.Stage);
                Report(results, test.Case, failures.ToOutcome() with { Outcome = TestOutcome.Errored }, TimeSpan.Zero, string.Empty, announce: true);
            }
            else
            {
                await RunTestAsync(lifecycle, test, results, cancellationToken).ConfigureAwait(false);
            }
        }

        if (setUpRan)
        {
            var tearDown = await TestThread.RunAsync(lifecycle.TearDownClass, $"TabStudio: {className} cleanup", _options.StackSize, null, _options.DefaultTimeout, CancellationToken.None).ConfigureAwait(false);
            var failure = tearDown.Status switch
            {
                ThreadRunStatus.Completed => tearDown.Value,
                ThreadRunStatus.Faulted => new StageFailure("Class cleanup", tearDown.Exception!),
                ThreadRunStatus.TimedOut => new StageFailure("Class cleanup", new TimeoutException("The class cleanup did not finish within the timeout.")),
                _ => null,
            };

            if (failure is not null)
            {
                lock (_gate)
                {
                    messages.Add($"{className}: {failure.Stage} failed: {OutcomeClassifier.Describe(OutcomeClassifier.Unwrap(failure.Exception))}");
                }
            }
        }
    }

    private async Task RunTestAsync(ClassLifecycle lifecycle, RunnableTest test, List<TestResult> results, CancellationToken cancellationToken)
    {
        _started(test.Case);
        var sink = new TestOutputSink(_options.MaxOutputLength);
        var timeout = test.Case.Timeout ?? _options.DefaultTimeout;
        var stopwatch = Stopwatch.StartNew();
        var run = await TestThread.RunAsync(() => lifecycle.RunTest(test, sink), $"TabStudio: {test.Case.DisplayName}", _options.StackSize, sink, timeout, cancellationToken).ConfigureAwait(false);
        stopwatch.Stop();

        var outcome = run.Status switch
        {
            ThreadRunStatus.Completed => run.Value!,
            ThreadRunStatus.TimedOut => new TestStepOutcome(TestOutcome.Errored, $"The test did not finish within its timeout of {Describe(timeout)}, so TabStudio stopped waiting for it."),
            ThreadRunStatus.Cancelled => new TestStepOutcome(TestOutcome.NotRun, "The run was stopped before the test finished."),
            _ => Faulted(run.Exception!),
        };

        Report(results, test.Case, outcome, stopwatch.Elapsed, sink.Close(), announce: false);
    }

    private static TestStepOutcome Faulted(Exception exception)
    {
        var failures = new FailureCollector();
        failures.Add(exception);
        return failures.ToOutcome();
    }

    /// <summary>Records a result and raises the finish event, preceded by the start event when <paramref name="announce"/> is set.</summary>
    private void Report(List<TestResult> results, TestCase test, TestStepOutcome outcome, TimeSpan duration, string output, bool announce)
    {
        var clean = outcome.Exception is { } exception && outcome.Outcome is TestOutcome.Failed or TestOutcome.Errored
            ? _stackTraces.Clean(exception)
            : null;

        var result = new TestResult
        {
            Test = test,
            Outcome = outcome.Outcome,
            Duration = duration,
            Message = string.IsNullOrEmpty(outcome.Message) ? null : outcome.Message,
            ExceptionType = outcome.Exception?.GetType().FullName,
            StackTrace = clean?.Text,
            StackFrames = clean?.Frames ?? [],
            FailureLocation = clean?.FailureLocation,
            Output = output,
        };

        lock (_gate)
        {
            results.Add(result);
        }

        if (announce)
        {
            // Tests that never ran still get a start event, so every finish has a matching start.
            _started(test);
        }

        _finished(result);
    }

    private TestRunSummary Summary(int total, List<TestResult> results, List<string> messages, Stopwatch stopwatch, bool cancelled)
    {
        lock (_gate)
        {
            return new TestRunSummary
            {
                Total = total,
                Results = [.. results],
                Duration = stopwatch.Elapsed,
                WasCancelled = cancelled,
                Messages = [.. messages],
            };
        }
    }

    private static string Describe(TimeSpan? timeout) =>
        timeout is { } value
            ? value.TotalSeconds >= 1
                ? value.TotalSeconds.ToString("0.#", CultureInfo.InvariantCulture) + " s"
                : value.TotalMilliseconds.ToString("0", CultureInfo.InvariantCulture) + " ms"
            : "no limit";
}
