using System.Diagnostics;
using System.Globalization;
using TabStudio.App.Services.Link;
using TabStudio.App.ViewModels;

namespace TabStudio.App.Services;

/// <summary>
/// The self test's TabStudio Link part: the sample compiled and debugged by a Link host, over an encrypted, paired
/// connection, then the host going away and the device compiling on its own again.
/// </summary>
public static partial class SelfTest
{
    /// <summary>The name of the host the self test links to.</summary>
    public const string LinkHostName = "Self-test host";

    private static readonly TimeSpan LinkSwitchTimeout = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Starts a host with an identity of its own inside the app, pairs with it as a tablet scanning its QR code does,
    /// debugs the sample with the host compiling, then stops the host and builds on this device.
    /// </summary>
    /// <returns>Whether the link worked, with a one-line summary.</returns>
    private static async Task<SelfTestResult> LinkSampleAsync(MainViewModel main, WorkspaceViewModel workspace, CancellationToken cancellationToken)
    {
        var clock = Stopwatch.StartNew();
        var link = main.Link.Service;
        if (workspace.LanguageService is not LinkedLanguageService language)
        {
            return new SelfTestResult(false, "Link: the workspace does not follow TabStudio Link.", clock.Elapsed);
        }

        var host = link.CreateLoopbackHost(LinkHostName);
        var fingerprint = host.Fingerprint;
        try
        {
            if (!await link.PairAsync(host.Invitation, cancellationToken))
            {
                return new SelfTestResult(false, $"Link: pairing failed: {link.ClientError}", clock.Elapsed);
            }

            var security = link.ConnectedHost?.Security ?? "TLS";
            if (!await WaitForAsync(() => language.IsLinked, cancellationToken))
            {
                return new SelfTestResult(false, "Link: the workspace did not move to the host.", clock.Elapsed);
            }

            var debugged = await DebugSampleAsync(workspace, cancellationToken);
            var requests = host.RequestCount;
            if (!debugged.Success || !language.IsLinked || requests == 0)
            {
                var why = !debugged.Success ? debugged.Message : language.IsLinked ? "the host answered nothing" : "the link dropped";
                return new SelfTestResult(false, $"Link: {why}", clock.Elapsed);
            }

            await host.StopAsync();
            if (!await WaitForAsync(() => !language.IsLinked, cancellationToken))
            {
                return new SelfTestResult(false, "Link: the workspace did not fall back after the host left.", clock.Elapsed);
            }

            await workspace.BuildCommand.ExecuteAsync(null).WaitAsync(cancellationToken);
            var built = workspace.Output.Text.Contains(" 0 failed ==========", StringComparison.Ordinal);
            var count = requests.ToString(CultureInfo.InvariantCulture);
            return built
                ? new SelfTestResult(true, $"Link: debugged on {LinkHostName} over {security} ({count} requests), built on this device after it left", clock.Elapsed)
                : new SelfTestResult(false, $"Link: after the host left, the build failed: {workspace.StatusText}", clock.Elapsed);
        }
        finally
        {
            await host.DisposeAsync();
            if (link.PairedHosts.Find(fingerprint) is { } paired)
            {
                await link.ForgetHostAsync(paired);
            }
        }
    }

    /// <summary>Waits until a condition holds, for at most <see cref="LinkSwitchTimeout"/>.</summary>
    private static async Task<bool> WaitForAsync(Func<bool> condition, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(LinkSwitchTimeout);
        try
        {
            await WaitAsync(condition, timeout.Token);
            return true;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }
}
