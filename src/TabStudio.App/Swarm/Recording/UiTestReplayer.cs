using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace TabStudio.App.Swarm.Recording;

/// <summary>How a recorded UI test ended on one screen.</summary>
public enum UiTestReplayOutcome
{
    /// <summary>Every step was delivered and every check held.</summary>
    Passed,

    /// <summary>A step could not be delivered, or a check did not hold.</summary>
    Failed,

    /// <summary>The test could not run on the screen, which says why.</summary>
    Skipped,
}

/// <summary>A recorded UI test's run on one screen.</summary>
/// <param name="Outcome">How it ended.</param>
/// <param name="Message">What failed, or why it did not run; <see langword="null"/> when it passed.</param>
/// <param name="Output">Every step as it went, one per line.</param>
/// <param name="Screenshot">A PNG of the screen when the test failed, if the screen could take one.</param>
/// <param name="Duration">How long it took.</param>
public sealed record UiTestReplayResult(UiTestReplayOutcome Outcome, string? Message, string Output, byte[]? Screenshot, TimeSpan Duration);

/// <summary>
/// Replays a recorded UI test on a screen of the <see cref="AppScreenRegistry"/>: starts the app again when the screen
/// can (<see cref="IAppScreenRestart"/>), gives it the test's inputs as a person would, and checks what its elements
/// show (<see cref="IAppScreenElements"/>). A failing test takes a picture of the screen.
/// </summary>
public static class UiTestReplayer
{
    /// <summary>How long a check waits for the element to show the text, since the app may update it after the input.</summary>
    internal static readonly TimeSpan CheckPatience = TimeSpan.FromSeconds(3);

    /// <summary>How long the app gets to take in an input before the next step.</summary>
    internal static readonly TimeSpan Settle = TimeSpan.FromMilliseconds(150);

    /// <summary>Replays a test on a screen.</summary>
    /// <param name="test">The test.</param>
    /// <param name="screen">The screen.</param>
    /// <param name="cancellationToken">Stops the test.</param>
    public static async Task<UiTestReplayResult> ReplayAsync(UiTestDefinition test, IAppScreen screen, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(test);
        ArgumentNullException.ThrowIfNull(screen);
        var clock = Stopwatch.StartNew();
        var output = new StringBuilder();
        var name = screen.Profile.Name;
        UiTestReplayResult End(UiTestReplayOutcome outcome, string? message, byte[]? screenshot = null) =>
            new(outcome, message, output.ToString(), screenshot, clock.Elapsed);

        if (test.Unsupported.Count > 0)
        {
            return End(
                UiTestReplayOutcome.Skipped,
                $"The test does more than tap, type and check ({test.Unsupported[0]}), so it runs only with dotnet test, not on the screens of the swarm.");
        }

        if (screen is IAppScreenRestart restart)
        {
            if (!await restart.RestartAppAsync(cancellationToken).ConfigureAwait(false))
            {
                return End(UiTestReplayOutcome.Failed, $"The app did not start again on {name}.");
            }

            output.Append(CultureInfo.InvariantCulture, $"Started the app again on {name} ({SwarmScreens.DescribeSize(screen.Profile.Width, screen.Profile.Height)}).\n");
        }
        else if (!screen.IsRunning)
        {
            return End(UiTestReplayOutcome.Skipped, $"The app is not running on {name}. Run it there, then run the test again.");
        }
        else
        {
            output.Append(CultureInfo.InvariantCulture, $"Replaying on the app as {name} shows it ({SwarmScreens.DescribeSize(screen.Profile.Width, screen.Profile.Height)}).\n");
        }

        var skippedChecks = 0;
        for (var i = 0; i < test.Steps.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var step = test.Steps[i];
            var number = (i + 1).ToString(CultureInfo.InvariantCulture);
            string? failure;
            if (step.ToInput() is { } input)
            {
                failure = await screen.SendInputAsync(input, cancellationToken).ConfigureAwait(false)
                    ? null
                    : $"Step {number}, {step.Description}: there is no element '{ElementPaths.ShortName(step.ElementPath)}' on {name}, or the app did not take the input.";
                await Task.Delay(Settle, cancellationToken).ConfigureAwait(false);
            }
            else if (screen is IAppScreenElements elements)
            {
                failure = await CheckAsync(elements, step, number, name, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                skippedChecks++;
                output.Append(CultureInfo.InvariantCulture, $"  - {step.Description}: not checked, {name} cannot read what elements show\n");
                continue;
            }

            if (failure is not null)
            {
                output.Append(CultureInfo.InvariantCulture, $"  x {step.Description}\n");
                var screenshot = await CaptureAsync(screen, cancellationToken).ConfigureAwait(false);
                return End(UiTestReplayOutcome.Failed, failure, screenshot);
            }

            output.Append(CultureInfo.InvariantCulture, $"  v {step.Description}\n");
        }

        return skippedChecks > 0
            ? End(UiTestReplayOutcome.Skipped, $"Every input was delivered, but {name} cannot read what elements show, so {skippedChecks} {(skippedChecks == 1 ? "check" : "checks")} did not run.")
            : End(UiTestReplayOutcome.Passed, null);
    }

    /// <summary>Waits a moment for an element to show the expected text, as the app may update it after the input.</summary>
    private static async Task<string?> CheckAsync(IAppScreenElements elements, UiTestStep step, string number, string screen, CancellationToken cancellationToken)
    {
        var expected = step.Text ?? string.Empty;
        var waited = Stopwatch.StartNew();
        string? actual;
        while (true)
        {
            actual = await elements.ReadTextAsync(step.ElementPath, cancellationToken).ConfigureAwait(false);
            if (actual == expected || waited.Elapsed >= CheckPatience)
            {
                break;
            }

            await Task.Delay(100, cancellationToken).ConfigureAwait(false);
        }

        var element = ElementPaths.ShortName(step.ElementPath);
        return actual switch
        {
            null => $"Step {number}, {step.Description}: there is no element '{element}' on {screen}.",
            _ when actual == expected => null,
            _ => $"Step {number}: {element} shows \"{actual}\" on {screen}, not \"{expected}\".",
        };
    }

    private static async Task<byte[]?> CaptureAsync(IAppScreen screen, CancellationToken cancellationToken)
    {
        try
        {
            return await screen.CaptureAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not (OperationCanceledException or OutOfMemoryException))
        {
            // A failed test is reported without its picture.
            return null;
        }
    }
}
