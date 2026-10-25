using System.Diagnostics;
using System.Globalization;
using TabStudio.App.Platform;
using TabStudio.App.Swarm;
using TabStudio.App.Swarm.Web;
using TabStudio.App.ViewModels;

namespace TabStudio.App.Services;

/// <summary>
/// The self test's web screen part: the running web app's page as a Swarm screen, a name typed and Greet tapped
/// through the browser's own input, the answer read back, a picture taken and the layout checked.
/// </summary>
public static partial class SelfTest
{
    /// <summary>How long the page may take to load as a screen, and to answer.</summary>
    private static readonly TimeSpan WebScreenTimeout = TimeSpan.FromSeconds(60);

    /// <summary>
    /// With the web app running in the web preview: waits for its page as a screen, taps the name box, types the
    /// self test's name and taps Greet through <see cref="IAppScreen.SendInputAsync"/>, reads the API's answer on the
    /// page, and checks that none of it came back as a person's input, that the screen takes a picture, and that the
    /// layout checks find the template's buttons too small for a finger.
    /// </summary>
    /// <returns>Whether the page greeted the name, with a one-line summary.</returns>
    private static async Task<SelfTestResult> CheckWebScreenAsync(WorkspaceViewModel workspace, CancellationToken cancellationToken)
    {
        var clock = Stopwatch.StartNew();
        if (workspace.WebPreview.WebViews is NoWebViewFactory)
        {
            return new SelfTestResult(true, "Web screen: no embedded browser on this platform", clock.Elapsed);
        }

        var screen = workspace.WebScreen;
        while (!(screen.IsRunning && workspace.Screens.Screens.Contains(screen)) && clock.Elapsed < WebScreenTimeout)
        {
            await Task.Delay(PollInterval, cancellationToken);
        }

        if (!screen.IsRunning || screen.WebView is not { } browser)
        {
            var why = workspace.WebPreview.WebView is null ? "the web preview made no browser" : "its page did not load";
            return new SelfTestResult(false, $"Web screen: {why}.", clock.Elapsed);
        }

        var echoed = new List<AppInput>();
        screen.InputCaptured += OnCaptured;
        try
        {
            // A person's steps, aimed at elements: the name box, the name, and the Greet button.
            var tapped = await screen.SendInputAsync(new AppInput(AppInputKind.Tap, "body/main[0]/form#form/input#name"), cancellationToken);
            await Task.Delay(TimeSpan.FromMilliseconds(600), cancellationToken);
            var typed = await screen.SendInputAsync(new AppInput(AppInputKind.Text, string.Empty, Text: Name), cancellationToken);
            var greeted = await screen.SendInputAsync(new AppInput(AppInputKind.Tap, "body/main[0]/form#form/button[0]"), cancellationToken);
            string? answer = null;
            while (clock.Elapsed < WebScreenTimeout)
            {
                answer = await browser.EvaluateAsync("document.getElementById('answer').textContent", cancellationToken);
                if (answer?.Contains($"Hello, {Name}!", StringComparison.Ordinal) == true)
                {
                    break;
                }

                await Task.Delay(PollInterval, cancellationToken);
            }

            // Long enough for the screen to read the page's inputs: replayed ones must not come back.
            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            var picture = await screen.CaptureAsync(cancellationToken);
            var issues = await screen.InspectLayoutAsync(cancellationToken);
            var answered = answer?.Contains($"Hello, {Name}!", StringComparison.Ordinal) == true;
            var small = issues.Count(i => i.Kind == LayoutIssueKind.SmallTouchTarget);
            var success = tapped && typed && greeted && answered && echoed.Count == 0 && picture is { Length: > 0 } && small > 0;
            var message = success
                ? string.Create(
                    CultureInfo.InvariantCulture,
                    $"Web screen: typed \"{Name}\" and tapped Greet through the browser's input on a {screen.Profile.Width:0} × {screen.Profile.Height:0} page, {issues.Count} layout findings, in {clock.Elapsed.TotalSeconds:0.0} s")
                : string.Create(
                    CultureInfo.InvariantCulture,
                    $"Web screen: tap {tapped}, text {typed}, Greet {greeted}, answer {answer ?? "none"}, echoed {echoed.Count}, picture {picture?.Length ?? 0} bytes, {small} small targets");
            return new SelfTestResult(success, message, clock.Elapsed);
        }
        finally
        {
            screen.InputCaptured -= OnCaptured;
        }

        void OnCaptured(object? sender, AppInput input) => echoed.Add(input);
    }
}
