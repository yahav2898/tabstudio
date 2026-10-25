using System.ComponentModel;
using System.Diagnostics;

namespace TabStudio.LanguageServers.Hosting;

/// <summary>
/// The <see cref="IServerProcessLauncher"/> that runs programs with <see cref="Process"/> and finds them on the
/// <c>PATH</c>, as on a desktop and in tests. On Android the Toolchain Store provides the launcher instead, since
/// an app may only run programs from the locations Android permits.
/// </summary>
public sealed class ProcessServerLauncher : IServerProcessLauncher
{
    /// <summary>The shared instance.</summary>
    public static ProcessServerLauncher Instance { get; } = new();

    /// <inheritdoc />
    public string? Resolve(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return null;
        }

        if (Path.IsPathRooted(command) || command.Contains('/', StringComparison.Ordinal) || command.Contains(Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            var full = Path.GetFullPath(command);
            return IsExecutableFile(full) ? full : null;
        }

        var extensions = OperatingSystem.IsWindows() && !Path.HasExtension(command)
            ? (Environment.GetEnvironmentVariable("PATHEXT") ?? ".EXE;.CMD;.BAT").Split(';', StringSplitOptions.RemoveEmptyEntries)
            : new[] { string.Empty };
        var folders = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var folder in folders)
        {
            foreach (var extension in extensions)
            {
                string candidate;
                try
                {
                    candidate = Path.Combine(folder.Trim('"'), command + extension);
                }
                catch (ArgumentException)
                {
                    continue;
                }

                if (IsExecutableFile(candidate))
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    /// <inheritdoc />
    public IServerProcess Start(ServerStartInfo startInfo)
    {
        ArgumentNullException.ThrowIfNull(startInfo);
        var info = new ProcessStartInfo(startInfo.FileName)
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = startInfo.WorkingDirectory,
        };

        foreach (var argument in startInfo.Arguments)
        {
            info.ArgumentList.Add(argument);
        }

        foreach (var (name, value) in startInfo.Environment)
        {
            if (value is null)
            {
                info.Environment.Remove(name);
            }
            else
            {
                info.Environment[name] = value;
            }
        }

        try
        {
            var process = Process.Start(info) ?? throw new ServerStartException($"{Path.GetFileName(startInfo.FileName)} did not start.");
            return new LocalServerProcess(process);
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException or PlatformNotSupportedException)
        {
            throw new ServerStartException($"{Path.GetFileName(startInfo.FileName)} could not be started: {ex.Message}", ex);
        }
    }

    private static bool IsExecutableFile(string path)
    {
        if (!File.Exists(path))
        {
            return false;
        }

        if (OperatingSystem.IsWindows())
        {
            return true;
        }

        try
        {
            const UnixFileMode Execute = UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;
            return (File.GetUnixFileMode(path) & Execute) != 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>A program started with <see cref="Process"/>.</summary>
    private sealed class LocalServerProcess : IServerProcess
    {
        private readonly Process _process;

        public LocalServerProcess(Process process)
        {
            _process = process;
            Exited = WaitForExitAsync();
        }

        public Stream StandardInput => _process.StandardInput.BaseStream;

        public Stream StandardOutput => _process.StandardOutput.BaseStream;

        public Stream StandardError => _process.StandardError.BaseStream;

        public Task<int> Exited { get; }

        public void Kill()
        {
            try
            {
                _process.Kill(entireProcessTree: true);
            }
            catch (Exception ex) when (ex is InvalidOperationException or Win32Exception or NotSupportedException)
            {
                // It has exited already, or the platform cannot end a process tree: nothing more to do.
            }
        }

        public void Dispose() => _process.Dispose();

        private async Task<int> WaitForExitAsync()
        {
            await _process.WaitForExitAsync().ConfigureAwait(false);
            return _process.ExitCode;
        }
    }
}
