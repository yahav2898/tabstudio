using Android.Util;

namespace TabStudio.Android.Probe;

/// <summary>
/// Writes the probe's progress and verdict to logcat under the <c>TabStudio</c> tag, where CI looks for it.
/// </summary>
/// <remarks>
/// The verdict is exactly one line that starts with <see cref="OkMarker"/> or <see cref="FailMarker"/>, so a script
/// can wait for it with <c>adb logcat -d -s TabStudio | grep TABSTUDIO_PROBE_</c>.
/// </remarks>
internal static class ProbeLog
{
    /// <summary>The logcat tag.</summary>
    public const string Tag = "TabStudio";

    /// <summary>Starts the verdict line of a probe that passed. The program's output follows.</summary>
    public const string OkMarker = "TABSTUDIO_PROBE_OK";

    /// <summary>Starts the verdict line of a probe that failed. The exception follows.</summary>
    public const string FailMarker = "TABSTUDIO_PROBE_FAIL";

    /// <summary>Logs one completed step.</summary>
    public static void Step(ProbeStep step) =>
        Log.Info(Tag, $"TABSTUDIO_PROBE_STEP {step.Name}: {step.Detail} ({step.Duration.TotalMilliseconds:0} ms)");

    /// <summary>Logs that the probe passed, with the program's output.</summary>
    public static void Passed(ProbeReport report) => Log.Info(Tag, $"{OkMarker} {OneLine(report.Output)}");

    /// <summary>Logs that the probe failed, with the whole exception on one line.</summary>
    public static void Failed(Exception exception) => Log.Error(Tag, $"{FailMarker} {OneLine(exception.ToString())}");

    private static string OneLine(string text) => text.Trim().ReplaceLineEndings(" | ");
}
