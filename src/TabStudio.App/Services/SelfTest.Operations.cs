using System.Diagnostics;
using TabStudio.App.Platform;
using TabStudio.App.ViewModels;

namespace TabStudio.App.Services;

public static partial class SelfTest
{
    /// <summary>How long Android gave a service started with startForegroundService to call startForeground.</summary>
    private static readonly TimeSpan ForegroundServiceDeadline = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Begins and ends the keep-running service's operations many times in quick succession, as one toolchain install
    /// after another does, then waits past Android's ten-second foreground deadline. Android ended TabStudio with an
    /// ANR here when a stop landed between the service's start and its startForeground, in the middle of the native
    /// shard's Java install; if that comes back, the app dies during the wait and the self test has no verdict.
    /// </summary>
    internal static async Task<SelfTestResult> CheckOperationsServiceAsync(WorkspaceViewModel workspace, CancellationToken cancellationToken)
    {
        var clock = Stopwatch.StartNew();
        var notifications = workspace.Operations.Notifications;
        if (!notifications.KeepsRunning)
        {
            return new(true, "Operations service: not used on this platform", clock.Elapsed);
        }

        for (var i = 0; i < 40; i++)
        {
            notifications.KeepRunning(new SystemNotification("operation:running", $"Self test {i}", "Starting", IsFailure: false)
            {
                IsProgress = true,
                ProgressPercent = i % 3 == 0 ? null : i,
            });

            // Some ends land before the service's start is handled, some after.
            if (i % 2 == 0)
            {
                await Task.Yield();
            }

            notifications.KeepRunning(null);
            await Task.Delay(i % 4 * 15, cancellationToken);
        }

        notifications.KeepRunning(new SystemNotification("operation:running", "Self test", "Last", IsFailure: false) { IsProgress = true });
        notifications.KeepRunning(null);
        await Task.Delay(ForegroundServiceDeadline + TimeSpan.FromSeconds(2), cancellationToken);
        return new(true, $"Operations service: 41 starts and stops, alive after {ForegroundServiceDeadline.TotalSeconds:0} s", clock.Elapsed);
    }
}
