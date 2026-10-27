using TabStudio.Toolchains.Embedded;
using TabStudio.Toolchains.Embedded.Installation;
using TabStudio.Toolchains.Processes;
using TabStudio.Toolchains.Runtimes;

internal static class NordicDfuCheck
{
    internal static async Task VerifyAsync(IToolchainRuntime runtime, string project, string output,
        CancellationToken cancellationToken)
    {
        var script = Path.GetFullPath("build/embedded/nordic-serial-check.py");
        if (!File.Exists(script))
        {
            throw new FileNotFoundException("Run the native DFU proof from the repository root.", script);
        }
        var evidence = Path.Combine(output, "nordic-dfu");
        Directory.CreateDirectory(evidence);
        // Keep this executable fixture in the same already shared workspace used by every frontend proof.
        // An extra deep bind under the checkout is not reliably visible with PRoot's link2symlink mapping.
        var guestScript = Path.Combine(project, "nordic-serial-check.py");
        File.Copy(script, guestScript, overwrite: true);
        runtime.ShareFolder(evidence);
        var result = await CommandRunner.RunAsync(runtime, new ProcessStartSpec
        {
            Program = "python3",
            Arguments = [guestScript,
                "--binary", EmbeddedNordicInstallation.Prefix + "/lib/nrfutil-nrf5sdk-tools/pc_nrfutil_legacy_v6.1.7",
                "--shim", EmbeddedNordicInstallation.X64SerialShim,
                "--native-shim", UsbBridgeInstallation.ToolsDirectory + "/serial-posix.so",
                "--launcher", "/usr/local/bin/nrfutil",
                "--bridge", UsbBridgeInstallation.ToolsDirectory + "/serial_bridge.py",
                "--output", evidence],
            WorkingDirectory = project,
            Environment = new Dictionary<string, string?> { [EmbeddedGlibcRuntime.RoutingMarker] = "1" },
        }, Console.WriteLine, cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(output, "nordic-dfu.log"), result.Output, cancellationToken);
        if (!result.Succeeded || !result.Output.Contains("NORDIC_SERIAL_BRIDGE_PASSED", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The unchanged Nordic client did not pass its production launcher/serial bridge proof: " + result.Tail(20));
        }
    }
}
