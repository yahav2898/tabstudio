using System.Globalization;
using TabStudio.App.Services;

namespace TabStudio.Android;

/// <summary>
/// The desktop mode self test's keyboard, mouse and screenshots on an emulator. Typing and screenshots are lines in
/// the log that the CI script (build/ci/android-smoke-test.sh) answers with adb: <c>input text</c>, which Android
/// sends to the tablet's window as a keyboard connected to the tablet would, and <c>screencap</c>. The mouse clicks
/// the display in the app, with the events a mouse on the display gives its window.
/// </summary>
/// <param name="log">Writes a line to the log the script reads.</param>
/// <param name="displays">The external displays, whose presentation the mouse clicks.</param>
internal sealed class AndroidDesktopSelfTestInput(Action<string> log, AndroidExternalDisplays? displays) : IDesktopSelfTestInput
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
    public void Click(int displayId, int x, int y)
    {
        var clicked = displays?.ClickWithMouse(x, y) == true;
        log(string.Create(CultureInfo.InvariantCulture, $"Desktop self test: {(clicked ? "clicked" : "could not click")} display {displayId} at {x},{y} with the mouse"));
    }

    /// <inheritdoc />
    public async Task ScreenshotAsync(CancellationToken cancellationToken)
    {
        log($"{StepMarker} screenshot");
        await Task.Delay(ScreenshotTime, cancellationToken);
    }
}
