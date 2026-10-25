using System.Diagnostics;
using System.Globalization;
using TabStudio.App.ViewModels;
using TabStudio.Toolchains.Processes;

namespace TabStudio.App.Services;

/// <summary>Temporary: the Windows Compatibility spike on the emulator (Wine, Xvfb and .NET in TabStudio's Linux).</summary>
public static partial class SelfTest
{
    private static readonly string[] SpikeFolders =
    [
        "/storage/emulated/0/Android/data/com.tabstudio.ide/files/spike",
        "/sdcard/Android/data/com.tabstudio.ide/files/spike",
    ];

    /// <summary>The spike's files when CI pushed them to the device, or <see langword="null"/>.</summary>
    private static string? SpikeFolder => SpikeFolders.FirstOrDefault(f => File.Exists(Path.Combine(f, "spike.py")));

    private static void Spike(string line) => Console.WriteLine("SPIKE-CS " + line);

    private static async Task<SelfTestResult> RunWindowsSpikeAsync(MainViewModel main, SampleLibrary samples, string folder, CancellationToken cancellationToken)
    {
        var clock = Stopwatch.StartNew();
        await main.OpenWorkspaceAsync(await samples.EnsureExtractedAsync(cancellationToken));
        if (main.Workspace is not { } workspace)
        {
            return new SelfTestResult(false, "Spike: the sample did not open.", clock.Elapsed);
        }

        var manager = workspace.Toolchains.Manager;
        var environment = workspace.Toolchains.Environment;
        if (!manager.CanInstall || environment is null)
        {
            return new SelfTestResult(false, $"Spike: cannot install: {manager.Problem}", clock.Elapsed);
        }

        await manager.PrepareRuntimeAsync(null, cancellationToken);
        Spike(string.Create(CultureInfo.InvariantCulture, $"linux_ready {clock.Elapsed.TotalSeconds:0.0}"));
        var installClock = Stopwatch.StartNew();
        await manager.RefreshAsync(checkForUpdates: true, cancellationToken);
        var before = manager.Status("windows-spike");
        Spike(string.Create(CultureInfo.InvariantCulture, $"plan download={before.DownloadSize} install={before.InstallSize} note={before.Note}"));
        await manager.InstallAsync("windows-spike", cancellationToken);
        var status = manager.Status("windows-spike");
        Spike(string.Create(CultureInfo.InvariantCulture, $"installed {status.State} {status.Version} in {installClock.Elapsed.TotalSeconds:0.0} s, size={status.InstalledSize} error={status.Error}"));
        if (!manager.IsReady("windows-spike"))
        {
            return new SelfTestResult(false, $"Spike: install failed: {status.Error}", clock.Elapsed);
        }

        var usage = manager.DiskUsage;
        Spike(string.Create(CultureInfo.InvariantCulture, $"disk toolchains={usage.Toolchains} base={usage.Base} free={usage.Free}"));
        var target = Path.Combine(environment.Rootfs, "spike");
        CopyFolder(folder, target);
        Spike(string.Create(CultureInfo.InvariantCulture, $"copied {clock.Elapsed.TotalSeconds:0.0}"));

        var result = await CommandRunner.RunAsync(
            workspace.Toolchains.Runtime,
            new ProcessStartSpec { Program = "python3", Arguments = ["-u", "/spike/spike.py", "/spike", "/spike/out", "--arch", "x64"] },
            line => Console.WriteLine(line),
            cancellationToken);
        Spike($"spike.py exit {result.Exit.Description}");

        var pictures = Path.Combine(Path.GetDirectoryName(folder)!, "selftest", "windows-spike");
        CopyFolder(Path.Combine(target, "out"), pictures);
        var results = Path.Combine(target, "out", "results.txt");
        var summary = File.Exists(results)
            ? string.Join(" | ", File.ReadAllLines(results).Where(l => l.StartsWith("SPIKE ", StringComparison.Ordinal)).Select(l => l[6..]))
            : "no results";
        var done = result.Output.Contains("SPIKE-DONE", StringComparison.Ordinal) && !result.Output.Contains("SPIKE-DONE (failed)", StringComparison.Ordinal);
        return new SelfTestResult(done, $"Spike: {summary}", clock.Elapsed);
    }

    private static void CopyFolder(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }
}
