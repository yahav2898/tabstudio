using System.Globalization;

namespace TabStudio.App.ViewModels;

/// <summary>How the Test Explorer words durations, counts and outcomes, following Visual Studio.</summary>
internal static class TestDisplay
{
    /// <summary>A duration such as "&lt; 1 ms", "45 ms", "1.2 sec" or "2 min 5 sec".</summary>
    /// <param name="duration">The duration.</param>
    public static string Duration(TimeSpan duration)
    {
        if (duration.TotalMilliseconds < 1)
        {
            return "< 1 ms";
        }

        if (duration.TotalSeconds < 1)
        {
            return duration.TotalMilliseconds.ToString("0", CultureInfo.InvariantCulture) + " ms";
        }

        return duration.TotalMinutes < 1
            ? duration.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture) + " sec"
            : $"{((int)duration.TotalMinutes).ToString(CultureInfo.InvariantCulture)} min {duration.Seconds.ToString(CultureInfo.InvariantCulture)} sec";
    }

    /// <summary>"1 test" or "5 tests".</summary>
    /// <param name="count">The number of tests.</param>
    public static string Tests(int count) =>
        count.ToString(CultureInfo.InvariantCulture) + (count == 1 ? " test" : " tests");

    /// <summary>The name of a state, as the details pane shows it.</summary>
    /// <param name="state">The state.</param>
    public static string State(TestState state) => state switch
    {
        TestState.Passed => "Passed",
        TestState.Failed => "Failed",
        TestState.Skipped => "Skipped",
        TestState.Running => "Running",
        TestState.Queued => "Waiting to run",
        _ => "Not run",
    };
}
