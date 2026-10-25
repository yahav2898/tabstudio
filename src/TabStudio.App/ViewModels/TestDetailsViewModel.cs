using System.Globalization;
using CommunityToolkit.Mvvm.Input;
using TabStudio.TestRunner;

namespace TabStudio.App.ViewModels;

/// <summary>One line of a failed test's stack trace in the details pane.</summary>
/// <param name="Method">The method, as the runtime prints it.</param>
/// <param name="Location">"MathTests.cs, line 12", or <see langword="null"/> when the frame has no source.</param>
/// <param name="IsUserCode">Whether the frame is in the user's code rather than a framework or .NET.</param>
/// <param name="OpenCommand">Opens the frame's line in the editor, or <see langword="null"/> when it has none.</param>
public sealed record TestStackFrameViewModel(string Method, string? Location, bool IsUserCode, IRelayCommand? OpenCommand)
{
    /// <summary>Whether tapping the frame opens its source.</summary>
    public bool CanOpen => OpenCommand is not null;

    /// <summary>Framework frames are drawn faded, so the user's own frames stand out.</summary>
    public double Opacity => IsUserCode ? 1 : 0.6;
}

/// <summary>How many tests of a group ended one way, such as "2 Failed".</summary>
/// <param name="State">The outcome.</param>
/// <param name="Text">The count and outcome.</param>
public sealed record TestOutcomeCountViewModel(TestState State, string Text);

/// <summary>
/// The Test Explorer's details pane: for a test, its outcome, duration, message, stack trace with links to the
/// source, and standard output; for a group, how its tests ended.
/// </summary>
public sealed class TestDetailsViewModel
{
    private static readonly TestState[] SummaryOrder =
        [TestState.Failed, TestState.Running, TestState.Queued, TestState.Skipped, TestState.NotRun, TestState.Passed];

    private TestDetailsViewModel()
    {
    }

    /// <summary>The test's or group's name.</summary>
    public required string Title { get; init; }

    /// <summary>The outcome, for the icon next to the title.</summary>
    public TestState State { get; init; }

    /// <summary>The outcome and duration, such as "Failed · 12 ms", or a group's size.</summary>
    public required string Summary { get; init; }

    /// <summary>Whether the result is out of date, because code changed after the test ran.</summary>
    public bool IsStale { get; init; }

    /// <summary>"MathTests.cs, line 12": where the test or class is.</summary>
    public string? SourceText { get; init; }

    /// <summary>Opens <see cref="SourceText"/> in the editor.</summary>
    public IRelayCommand? GoToSourceCommand { get; init; }

    /// <summary>The assertion or exception message, or why the test was skipped.</summary>
    public string? Message { get; init; }

    /// <summary>Whether there is a message.</summary>
    public bool HasMessage => !string.IsNullOrEmpty(Message);

    /// <summary>The stack trace, frame by frame.</summary>
    public IReadOnlyList<TestStackFrameViewModel> StackFrames { get; init; } = [];

    /// <summary>Whether there is a stack trace.</summary>
    public bool HasStackFrames => StackFrames.Count > 0;

    /// <summary>What the test wrote to the console or to xUnit's output helper.</summary>
    public string? Output { get; init; }

    /// <summary>Whether the test wrote anything.</summary>
    public bool HasOutput => !string.IsNullOrEmpty(Output);

    /// <summary>A picture of the screen a recorded UI test failed on, read from its PNG.</summary>
    public Avalonia.Media.Imaging.Bitmap? Screenshot { get; init; }

    /// <summary>Whether there is a picture of the failure.</summary>
    public bool HasScreenshot => Screenshot is not null;

    /// <summary>For a group, how many of its tests ended each way.</summary>
    public IReadOnlyList<TestOutcomeCountViewModel> Outcomes { get; init; } = [];

    /// <summary>Whether this describes a group rather than one test.</summary>
    public bool IsGroup => Outcomes.Count > 0;

    /// <summary>Describes the selected row.</summary>
    /// <param name="node">The row.</param>
    /// <param name="navigate">Opens a file at a line and column.</param>
    public static TestDetailsViewModel For(TestNodeViewModel node, Func<string, int, int, Task> navigate)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(navigate);
        return node.Entry is { } entry ? ForTest(entry, navigate) : ForGroup(node, navigate);
    }

    private static TestDetailsViewModel ForTest(TestEntry entry, Func<string, int, int, Task> navigate)
    {
        var result = entry.Result;
        var state = TestDisplay.State(entry.State);
        if (result?.Outcome == TestOutcome.Errored)
        {
            state = result.ExceptionType is null ? "Failed with an error" : "Failed with " + ShortTypeName(result.ExceptionType);
        }

        var finished = result is not null && entry.State is TestState.Passed or TestState.Failed or TestState.Skipped;
        return new TestDetailsViewModel
        {
            Title = entry.DisplayName,
            State = entry.State,
            Summary = finished ? $"{state} · {TestDisplay.Duration(result!.Duration)}" : state,
            IsStale = entry.IsStale && finished,
            SourceText = Describe(entry.Location),
            GoToSourceCommand = Open(entry.Location, navigate),
            Message = finished ? result!.Message : SkipMessage(entry),
            StackFrames = finished ? result!.StackFrames.Select(f => Frame(f, navigate)).ToList() : [],
            Output = finished ? result!.Output : null,
            Screenshot = finished && entry.State == TestState.Failed ? LoadScreenshot(entry.ScreenshotPath) : null,
        };
    }

    private static Avalonia.Media.Imaging.Bitmap? LoadScreenshot(string? path)
    {
        if (path is null || !File.Exists(path))
        {
            return null;
        }

        try
        {
            return new Avalonia.Media.Imaging.Bitmap(path);
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or InvalidOperationException)
        {
            // A picture that cannot be read leaves the details without it.
            return null;
        }
    }

    private static TestDetailsViewModel ForGroup(TestNodeViewModel node, Func<string, int, int, Task> navigate)
    {
        var counts = node.Entries.GroupBy(e => e.State).ToDictionary(g => g.Key, g => g.Count());
        var summary = TestDisplay.Tests(node.Entries.Count);
        if (node.DurationText is { } duration)
        {
            summary += " · " + duration;
        }

        return new TestDetailsViewModel
        {
            Title = node.Name,
            State = node.State,
            Summary = node.Kind is TestNodeKind.Class or TestNodeKind.Method && node.Description is { } fullName ? $"{summary} · {fullName}" : summary,
            IsStale = node.IsStale,
            SourceText = node.Kind == TestNodeKind.Class ? Describe(node.Location) : null,
            GoToSourceCommand = node.Kind == TestNodeKind.Class ? Open(node.Location, navigate) : null,
            Outcomes = SummaryOrder
                .Select(s => (State: s, Count: counts.GetValueOrDefault(s)))
                .Where(c => c.Count > 0)
                .Select(c => new TestOutcomeCountViewModel(c.State, c.Count.ToString(CultureInfo.InvariantCulture) + " " + TestDisplay.State(c.State)))
                .ToList(),
        };
    }

    private static TestStackFrameViewModel Frame(TestStackFrame frame, Func<string, int, int, Task> navigate)
    {
        var location = frame.FilePath is { } file && frame.Line is { } line ? new TestSourceLocation(file, line) : null;
        return new TestStackFrameViewModel(
            frame.Method,
            Describe(location),
            frame.IsUserCode,
            location is not null && File.Exists(location.FilePath) ? Open(location, navigate) : null);
    }

    private static string? SkipMessage(TestEntry entry) =>
        entry.SkipReason is { } reason ? "Skipped: " + reason : null;

    private static string? Describe(TestSourceLocation? location) =>
        location is null ? null : $"{Path.GetFileName(location.FilePath)}, line {location.Line.ToString(CultureInfo.InvariantCulture)}";

    private static AsyncRelayCommand? Open(TestSourceLocation? location, Func<string, int, int, Task> navigate) =>
        location is null ? null : new AsyncRelayCommand(() => navigate(location.FilePath, location.Line, location.Column));

    private static string ShortTypeName(string fullName) =>
        fullName[(fullName.LastIndexOf('.') + 1)..];
}
