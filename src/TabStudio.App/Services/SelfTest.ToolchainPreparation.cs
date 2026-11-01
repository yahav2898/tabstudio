using TabStudio.App.Services.Toolchains;

namespace TabStudio.App.Services;

public static partial class SelfTest
{
    private static readonly string[] SelfTestToolchainIds = ["python", "node", "go", "rust", "cpp", "java"];
    private static readonly string[] ManagedSelfTestToolchainIds = ["go", "rust"];
    private static readonly string[] NativeSelfTestToolchainIds = ["python", "node"];
    private static readonly string[] CompiledSelfTestToolchainIds = ["cpp", "java"];

    // Kept in one place so the partition can be checked without downloading SDKs in a unit test.
    internal static IReadOnlyList<string> SelfTestToolchainIdsForShard(SelfTestShard shard) => shard switch
    {
        SelfTestShard.All => SelfTestToolchainIds,
        SelfTestShard.Managed => ManagedSelfTestToolchainIds,
        SelfTestShard.Native => NativeSelfTestToolchainIds,
        SelfTestShard.Compiled => CompiledSelfTestToolchainIds,
        SelfTestShard.Ui => [],
        _ => throw new ArgumentOutOfRangeException(nameof(shard), shard, "Unknown self-test shard."),
    };

    /// <summary>
    /// Installs only the current shard's toolchains while its independent .NET and UI checks run. The manager
    /// serializes package operations; callers await this task before running the tools. Separate emulator jobs
    /// never install packages solely to support assertions assigned to a different shard.
    /// </summary>
    private static async Task PrepareSelfTestToolchainsAsync(ToolchainServices toolchains, SelfTestShard shard, CancellationToken cancellationToken)
    {
        await toolchains.EnsureStatusAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
        if (!toolchains.Manager.CanInstall)
        {
            return;
        }

        var missing = SelfTestToolchainIdsForShard(shard)
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
