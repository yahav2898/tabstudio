using TabStudio.Toolchains.Embedded.Installation;
using TabStudio.Toolchains.Installation;
using TabStudio.Toolchains.Linux;
using TabStudio.Toolchains.Processes;
using TabStudio.Toolchains.Runtimes;

internal static class Box64Check
{
    internal static async Task VerifyAsync(EmbeddedGlibcToolchainInstaller installer, string project,
        string output, CancellationToken cancellationToken)
    {
        await installer.PrepareBox64Async(Console.WriteLine, cancellationToken);
        var source = Path.GetFullPath("build/embedded/box64-check.py");
        var script = Path.Combine(project, "box64-check.py");
        File.Copy(source, script, overwrite: true);
        File.Copy(Path.GetFullPath("build/embedded/platformio-policy-check.py"), Path.Combine(project, "platformio-policy-check.py"), overwrite: true);
        var inherited = Path.Combine(project, "box64-inherited-fd");
        using var handle = File.OpenHandle(inherited, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
        var result = await CommandRunner.RunAsync(installer.Runtime, new ProcessStartSpec
        {
            Program = "/opt/tabstudio/platformio/bin/python", Arguments = ["-P", script, "--spawn", Path.Combine(project, "boxed-spawn")],
            WorkingDirectory = project,
            Environment = new Dictionary<string, string?> { [EmbeddedGlibcRuntime.RoutingMarker] = "1", ["TABSTUDIO_USB_FD"] = "3" },
            InheritedDescriptors = new Dictionary<int, int> { [checked((int)handle.DangerousGetHandle())] = 3 },
        }, Console.WriteLine, cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(output, "box64.log"), result.Output, cancellationToken);
        if (!result.Succeeded || !result.Output.Contains("BOX64_COMPILER_CHILD_FD_PASSED", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The production Box64 compiler/child-descriptor proof failed: " + result.Tail(20));
        }
        File.Copy(Path.Combine(project, "box64-proof", "firmware.elf"), Path.Combine(output, "boxed-avr-firmware.elf"), overwrite: true);
        File.Copy(Path.Combine(project, "box64-proof", "summary.json"), Path.Combine(output, "box64-summary.json"), overwrite: true);
        await VerifySerialAsync(installer, project, output, cancellationToken);
    }

    private static async Task VerifySerialAsync(EmbeddedGlibcToolchainInstaller installer, string project,
        string output, CancellationToken cancellationToken)
    {
        await EmbeddedBox64Installation.PrepareSerialShimAsync(ToolchainPlatform.CurrentArchitecture!, installer.Runtime,
            Console.WriteLine, cancellationToken);
        var script = Path.Combine(project, "serial-x64-check.py");
        File.Copy(Path.GetFullPath("build/embedded/serial-x64-check.py"), script, overwrite: true);
        var result = await CommandRunner.RunAsync(installer.Runtime, new ProcessStartSpec
        {
            Program = "/opt/tabstudio/platformio/bin/python",
            Arguments = ["-P", script, "--client", Path.Combine(project, "serial-client-x64"),
                "--source", Path.Combine(project, "serial-client.c"),
                "--native-shim", "/opt/tabstudio/embedded/serial-posix.so",
                "--x64-shim", "/opt/tabstudio/embedded/serial-posix-x86_64.so"],
            WorkingDirectory = project,
            Environment = new Dictionary<string, string?> { [EmbeddedGlibcRuntime.RoutingMarker] = "1" },
        }, Console.WriteLine, cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(output, "serial-x64.log"), result.Output, cancellationToken);
        if (!result.Succeeded || !result.Output.Contains("SERIAL_X64_ABI_PASSED", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The production x64 serial control proof failed: " + result.Tail(20));
        }
        File.Copy(Path.Combine(project, "serial-x64-evidence.json"), Path.Combine(output, "serial-x64-evidence.json"), overwrite: true);
        File.Copy(Path.Combine(project, "serial-x64-symbol-versions.txt"), Path.Combine(output, "serial-x64-symbol-versions.txt"), overwrite: true);
    }
}
