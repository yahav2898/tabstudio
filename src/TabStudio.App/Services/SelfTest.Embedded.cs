using System.Diagnostics;
using TabStudio.App.ViewModels;
using TabStudio.Toolchains.Embedded;
using TabStudio.Toolchains.Processes;

namespace TabStudio.App.Services;

public static partial class SelfTest
{
    private static async Task<SelfTestResult> CheckEmbeddedBridgeAsync(WorkspaceViewModel workspace, CancellationToken cancellationToken)
    {
        var clock = Stopwatch.StartNew();
        if (workspace.Toolchains.Environment is null)
            return new(true, "Embedded USB bridge: Android-only self-test; host native contracts run separately.", clock.Elapsed);
        await workspace.Toolchains.PrepareEmbeddedSupportAsync(line => workspace.Output.WriteLine(line, OutputKind.Info), cancellationToken);
        var result = await CommandRunner.RunAsync(workspace.Toolchains.Runtime, UsbBridgeProcess.CreateVirtualSelfTest(), null, cancellationToken);
        var passed = result.Succeeded && result.Output.Contains("EMBEDDED_BRIDGE_PASSED", StringComparison.Ordinal);
        return new(passed, passed
            ? "Embedded bridge: native PTY byte transport, baud, modem lines and hangup passed with virtual USB; no hardware claim."
            : "Embedded bridge failed: " + result.Tail(), clock.Elapsed);
    }
}
