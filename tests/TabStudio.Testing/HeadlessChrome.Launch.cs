using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;

namespace TabStudio.Testing;

/// <summary>
/// Launching and stopping the browser: its output read to the end, its DevTools port found from its output or its
/// profile, a launch that does not come up stopped and tried again, and none of its processes left behind.
/// </summary>
public sealed partial class HeadlessChrome
{
    /// <summary>How many times <see cref="StartAsync"/> launches a browser that does not come up, the first included.</summary>
    private const int Launches = 2;

    /// <summary>
    /// How long a launch may take to open its DevTools port. A CI runner busy with other test hosts has been seen to
    /// take more than 30 s.
    /// </summary>
    private static readonly TimeSpan LaunchTimeout = TimeSpan.FromSeconds(60);

    /// <summary>How long each later step of the start may take: listing the pages, connecting to one.</summary>
    private static readonly TimeSpan StepTimeout = TimeSpan.FromSeconds(30);

    private static Process Launch(string executable, string profile, int width, int height, BrowserLog log)
    {
        var start = new ProcessStartInfo(executable)
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        foreach (var argument in new[]
        {
            "--headless=new", "--no-sandbox", "--disable-gpu", "--no-first-run", "--no-default-browser-check",
            "--disable-crash-reporter", "--remote-debugging-port=0", $"--user-data-dir={profile}",
            $"--window-size={width},{height}", "about:blank",
        })
        {
            start.ArgumentList.Add(argument);
        }

        // Both streams are read to their end for as long as the browser runs: a full pipe would stop it the next time
        // it logs, in the middle of a test.
        var process = new Process { StartInfo = start };
        try
        {
            process.OutputDataReceived += (_, e) => log.Add(e.Data);
            process.ErrorDataReceived += (_, e) => log.Add(e.Data);
            if (!process.Start())
            {
                throw new InvalidOperationException($"{executable} did not start.");
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            return process;
        }
        catch
        {
            process.Dispose();
            throw;
        }
    }

    /// <summary>
    /// The browser's DevTools address: from the line it prints at startup, or from the <c>DevToolsActivePort</c> file it
    /// writes into its profile, whichever comes first.
    /// </summary>
    private static async Task<Uri> WaitForDevToolsAsync(Process process, string profile, BrowserLog log)
    {
        var portFile = Path.Combine(profile, "DevToolsActivePort");
        var waited = Stopwatch.StartNew();
        while (true)
        {
            if ((log.DevToolsEndpoint ?? ReadPortFile(portFile)) is { } endpoint)
            {
                return endpoint;
            }

            if (process.HasExited)
            {
                throw new InvalidOperationException($"Chrome exited with code {process.ExitCode} before it opened its DevTools port.");
            }

            if (waited.Elapsed > LaunchTimeout)
            {
                throw new TimeoutException($"Chrome did not open its DevTools port in {LaunchTimeout.TotalSeconds:0} s.");
            }

            await Task.Delay(100);
        }
    }

    /// <summary>Reads <c>DevToolsActivePort</c>: the port on the first line, the browser's path on the second.</summary>
    private static Uri? ReadPortFile(string path)
    {
        string[] lines;
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            lines = File.ReadAllLines(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Still being written.
            return null;
        }

        return lines.Length >= 2
            && int.TryParse(lines[0], NumberStyles.None, CultureInfo.InvariantCulture, out var port)
            && lines[1].StartsWith('/')
            ? new Uri(string.Create(CultureInfo.InvariantCulture, $"ws://127.0.0.1:{port}{lines[1]}"))
            : null;
    }

    /// <summary>Stops a browser that did not come up, with whatever of it was set up.</summary>
    private static async Task AbandonAsync(HeadlessChrome? chrome, Process? process, TempDirectory profile)
    {
        if (chrome is not null)
        {
            await chrome.DisposeAsync();
        }
        else if (process is not null)
        {
            await StopAsync(process, profile);
        }
        else
        {
            profile.Dispose();
        }
    }

    /// <summary>Kills a browser and every process it started, waits for it, and deletes its profile.</summary>
    private static async Task StopAsync(Process process, TempDirectory profile)
    {
        try
        {
            process.Kill(entireProcessTree: true);
            using var timeout = new CancellationTokenSource(StepTimeout);
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (Exception ex) when (ex is InvalidOperationException or OperationCanceledException or Win32Exception)
        {
            // Already exited, or killed below.
        }

        KillProfileProcesses(profile.Path);
        process.Dispose();
        profile.Dispose();
    }

    /// <summary>
    /// Kills what is left of a browser on Linux: every process whose command line names its profile. Chrome's crash
    /// handler and zygotes leave the browser's process tree, so killing the tree alone leaves them running after the
    /// test, one set per browser started.
    /// </summary>
    private static void KillProfileProcesses(string profile)
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        List<string> folders;
        try
        {
            folders = Directory.EnumerateDirectories("/proc").ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return;
        }

        foreach (var folder in folders)
        {
            if (!int.TryParse(Path.GetFileName(folder), NumberStyles.None, CultureInfo.InvariantCulture, out var id) || id == Environment.ProcessId)
            {
                continue;
            }

            try
            {
                if (File.ReadAllText(Path.Combine(folder, "cmdline")).Contains(profile, StringComparison.Ordinal))
                {
                    using var leftover = Process.GetProcessById(id);
                    leftover.Kill();
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or Win32Exception)
            {
                // It exited meanwhile, or is not ours to kill.
            }
        }
    }

    /// <summary>What a browser printed: its last lines, for the message of a launch that failed, and its DevTools address.</summary>
    private sealed class BrowserLog
    {
        private const string DevToolsPrefix = "DevTools listening on ";
        private const int KeptLines = 40;
        private readonly ConcurrentQueue<string> _lines = new();
        private Uri? _devToolsEndpoint;

        /// <summary>The address the browser announced, once it has.</summary>
        public Uri? DevToolsEndpoint => Volatile.Read(ref _devToolsEndpoint);

        /// <summary>Adds a line the browser printed; <see langword="null"/> marks the end of a stream.</summary>
        public void Add(string? line)
        {
            if (line is null)
            {
                return;
            }

            if (line.StartsWith(DevToolsPrefix, StringComparison.Ordinal) && Uri.TryCreate(line[DevToolsPrefix.Length..].Trim(), UriKind.Absolute, out var endpoint))
            {
                Volatile.Write(ref _devToolsEndpoint, endpoint);
            }

            _lines.Enqueue(line);
            while (_lines.Count > KeptLines && _lines.TryDequeue(out _))
            {
                // Only the last lines matter.
            }
        }

        /// <inheritdoc />
        public override string ToString() => _lines.IsEmpty ? "(nothing)" : string.Join('\n', _lines);
    }
}
