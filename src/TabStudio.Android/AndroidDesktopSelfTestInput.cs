using System.Globalization;
using TabStudio.App.Services;

namespace TabStudio.Android;

/// <summary>
/// The desktop mode self test's keyboard, mouse and screenshots on an emulator: each is a line in the log that the CI
/// script (build/ci/android-smoke-test.sh) answers with adb, typing with <c>input text</c>, clicking the external
/// display with <c>input mouse -d</c> and taking screenshots with <c>screencap</c>.
/// </summary>
/// <param name="log">Writes a line to the log the script reads.</param>
internal sealed class AndroidDesktopSelfTestInput(Action<string> log) : IDesktopSelfTestInput
{
    /// <summary>The start of the log lines that ask the script to act.</summary>
    public const string StepMarker = "TABSTUDIO_DESKTOP_STEP";

    /// <summary>How long the script may take to take its screenshots.</summary>
    private static readonly TimeSpan ScreenshotTime = TimeSpan.FromSeconds(6);

    /// <inheritdoc />
    /// <remarks>Spaces are written as underscores, which the script types as spaces.</remarks>
    public void Type(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        log($"{StepMarker} type {text.Replace(' ', '_')}");
    }

    /// <inheritdoc />
    public void Click(int displayId, int x, int y) =>
        log(string.Create(CultureInfo.InvariantCulture, $"{StepMarker} click {displayId} {x} {y}"));

    /// <inheritdoc />
    public async Task ScreenshotAsync(CancellationToken cancellationToken)
    {
        log($"{StepMarker} screenshot");
        await Task.Delay(ScreenshotTime, cancellationToken);
    }
}
