using System.Globalization;
using CommunityToolkit.Mvvm.Input;
using TabStudio.App.Editing;
using TabStudio.LiveTesting.Results;
using TabStudio.TestRunner;

namespace TabStudio.App.ViewModels;

/// <summary>A test in a Live Unit Testing marker's popup: its outcome and duration, with Run and Go To.</summary>
public sealed partial class CoveringTestViewModel
{
    private readonly ILiveTestingEditor? _editor;

    /// <summary>Creates the row.</summary>
    /// <param name="test">The test that runs the line.</param>
    /// <param name="editor">Runs and opens the test, or <see langword="null"/> when nothing can.</param>
    public CoveringTestViewModel(CoveringTest test, ILiveTestingEditor? editor)
    {
        ArgumentNullException.ThrowIfNull(test);
        Test = test;
        _editor = editor;
    }

    /// <summary>The test.</summary>
    public CoveringTest Test { get; }

    /// <summary>The test's name, with a data row's arguments.</summary>
    public string Name => Test.Test.DisplayName;

    /// <summary>The test's class, without its namespace.</summary>
    public string ClassName => Test.Test.TypeName;

    /// <summary>The outcome shown with the test's icon.</summary>
    public TestState State => Test.IsPending
        ? TestState.Queued
        : Test.Outcome switch
        {
            TestOutcome.Passed => TestState.Passed,
            TestOutcome.Failed or TestOutcome.Errored => TestState.Failed,
            TestOutcome.Skipped => TestState.Skipped,
            _ => TestState.NotRun,
        };

    /// <summary>How long the test's latest live run took, such as "4 ms".</summary>
    public string? DurationText => Test.Outcome is null ? null : TestDisplay.Duration(Test.Duration);

    /// <summary>The row's tooltip.</summary>
    public string Description => Test.Test.FullyQualifiedName + Test.Test.DataRow + (Test.IsPending ? " (waiting to run again)" : string.Empty);

    [RelayCommand]
    private Task RunAsync() => _editor?.RunTestsAsync([Test.Key]) ?? Task.CompletedTask;

    [RelayCommand]
    private Task GoToAsync() => _editor?.GoToTestAsync(Test.Key) ?? Task.CompletedTask;
}

/// <summary>
/// The popup of a Live Unit Testing marker: what the marker means and the tests that run its line, each of which can
/// be run again or opened.
/// </summary>
public sealed partial class LiveTestPopupViewModel
{
    private readonly ILiveTestingEditor? _editor;

    /// <summary>Creates the popup's content.</summary>
    /// <param name="line">The one-based line of the marker.</param>
    /// <param name="marker">The marker.</param>
    /// <param name="editor">Runs and opens tests.</param>
    public LiveTestPopupViewModel(int line, LiveTestMarker marker, ILiveTestingEditor? editor)
    {
        ArgumentNullException.ThrowIfNull(marker);
        Line = line;
        Marker = marker;
        _editor = editor;
        Tests = [.. marker.Tests.Select(t => new CoveringTestViewModel(t, editor))];
    }

    /// <summary>The one-based line of the marker.</summary>
    public int Line { get; }

    /// <summary>The marker.</summary>
    public LiveTestMarker Marker { get; }

    /// <summary>The tests that run the line, failing first.</summary>
    public IReadOnlyList<CoveringTestViewModel> Tests { get; }

    /// <summary>Whether any test runs the line.</summary>
    public bool HasTests => Tests.Count > 0;

    /// <summary>The marker's state, for the header's icon.</summary>
    public LineCoverageState State => Marker.State;

    /// <summary>The header, such as "Covered by 3 tests, 1 failing".</summary>
    public string Title => Marker.State switch
    {
        LineCoverageState.NotCovered => "Not covered by any test",
        _ => string.Create(CultureInfo.InvariantCulture, $"Covered by {TestDisplay.Tests(Tests.Count)}"),
    };

    /// <summary>The line under the header: the outcomes, and whether they are out of date.</summary>
    public string Summary
    {
        get
        {
            var passed = Tests.Count(t => t.State == TestState.Passed);
            var failed = Tests.Count(t => t.State == TestState.Failed);
            var counts = Marker.State == LineCoverageState.NotCovered
                ? "Line " + Line.ToString(CultureInfo.InvariantCulture) + " runs in no test that Live Unit Testing includes."
                : string.Create(CultureInfo.InvariantCulture, $"Line {Line}: {passed} passed, {failed} failed");
            return Marker.IsStale ? counts + " · out of date, the tests run again shortly" : counts;
        }
    }

    /// <summary>Whether "Run All" is offered: more than one test runs the line.</summary>
    public bool CanRunAll => Tests.Count > 1;

    [RelayCommand]
    private Task RunAllAsync() => _editor?.RunTestsAsync([.. Tests.Select(t => t.Test.Key)]) ?? Task.CompletedTask;
}
