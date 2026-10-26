using TabStudio.App.Services.Toolchains;

namespace TabStudio.App.Services;

public static partial class SelfTest
{
    private static readonly string[] SelfTestToolchainIds = ["python", "node", "go", "rust", "cpp", "java"];

    /// <summary>
    /// Installs the self test's toolchains while its independent .NET and UI checks run. The manager serializes
    /// package operations; the caller awaits this task before running any of the toolchain checks.
    /// </summary>
    private static async Task PrepareSelfTestToolchainsAsync(ToolchainServices toolchains, CancellationToken cancellationToken)
    {
        await toolchains.EnsureStatusAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
        if (!toolchains.Manager.CanInstall)
        {
            return;
        }

        var missing = SelfTestToolchainIds
            .Where(id => !toolchains.Manager.IsReady(id));
        await toolchains.Manager.InstallManyAsync(missing, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Stops and observes preparation on every exit path, including a failed earlier UI check.</summary>
    private static async Task FinishSelfTestPreparationAsync(Task? preparation, CancellationTokenSource stop)
    {
        if (preparation is null)
        {
            return;
        }

        await stop.CancelAsync().ConfigureAwait(false);
        try
        {
            await preparation.ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // The normal path awaits preparation and reports its error. An early UI failure already fails the test.
        }
    }
}
