using System.Diagnostics;
using TabStudio.App.ViewModels;
using TabStudio.Remote;

namespace TabStudio.App.Services;

public static partial class SelfTest
{
    private static async Task<SelfTestResult> CheckRemoteSshAsync(WorkspaceViewModel workspace, CancellationToken cancellationToken)
    {
        var clock = Stopwatch.StartNew();
        try
        {
            // On Android this creates and signs with Android Keystore, not the desktop file implementation.
            await workspace.CheckSshDeviceKeyAsync(cancellationToken);
            var hosts = SshConfig.Parse("Host self-test\n HostName localhost\n User tablet\n Port 2222", "/home/tablet", "tablet");
            if (hosts.Count != 1 || hosts[0].Port != 2222) { return new(false, "SSH config import failed", clock.Elapsed); }
            return new(true, "SSH: device key generation, non-exportable signing provider and config import verified", clock.Elapsed);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not OperationCanceledException) { return new(false, "SSH: " + ex.Message, clock.Elapsed); }
    }
}
