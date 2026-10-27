using TabStudio.Toolchains.Embedded.Installation;
using TabStudio.Toolchains.Installation;
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
    }
}
