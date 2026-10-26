using System.Collections;
using TabStudio.Toolchains.Processes;
using TabStudio.Toolchains.Processes.Unix;
using TabStudio.Toolchains.Processes.Windows;

namespace TabStudio.Toolchains.Runtimes;

/// <summary>
/// Runs programs on the computer TabStudio itself runs on, with the toolchains installed there: the desktop build
/// and the tests use it, so they exercise the IDE's toolchain features off the device.
/// </summary>
/// <remarks>
/// Programs get TabStudio's environment with <see cref="ProcessStartSpec.Environment"/> applied on top, and are
/// found on its <c>PATH</c>. On Linux and macOS they start with <c>posix_spawn</c>, on pipes or in a pseudo
/// terminal; on Windows they start on pipes, and the Terminal uses a pseudo console.
/// </remarks>
public sealed class HostRuntime : IToolchainRuntime
{
    /// <inheritdoc />
    public string DisplayName => "This computer";

    /// <inheritdoc />
    public RuntimeKind Kind => RuntimeKind.Host;

    /// <inheritdoc />
    public ToolchainProcess Start(ProcessStartSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        var environment = BuildEnvironment(spec);
        var path = FindProgram(spec.Program, environment)
            ?? throw new ToolchainRuntimeException($"'{spec.Program}' was not found on this computer. Install it, or add its folder to PATH.");
        var workingDirectory = spec.WorkingDirectory is { } folder && Directory.Exists(folder) ? folder : null;

        if (OperatingSystem.IsWindows())
        {
            if (spec.InheritedDescriptors.Count > 0)
            {
                throw new ToolchainRuntimeException("USB file descriptors are supported by the Android/Linux runtime.");
            }
            if (spec.Terminal is { } size)
            {
                return OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763)
                    ? new ToolchainProcess(WindowsPseudoConsole.Start(path, spec.Arguments, environment, workingDirectory, size), spec)
                    : throw new ToolchainRuntimeException("The Terminal needs Windows 10 version 1809 or later.");
            }

            return new ToolchainProcess(DotNetProcessHandle.Start(path, spec.Arguments, environment, workingDirectory), spec);
        }

        var arguments = new List<string> { spec.Program };
        arguments.AddRange(spec.Arguments);
        var variables = environment.Select(e => $"{e.Key}={e.Value}").ToList();
        if (workingDirectory is not null && !UnixSpawner.CanChangeDirectory)
        {
            // Without posix_spawn_file_actions_addchdir_np, a shell changes folder and then becomes the program.
            arguments = ["/bin/sh", "-c", "cd -- \"$0\" && exec \"$@\"", workingDirectory, path, .. spec.Arguments];
            path = "/bin/sh";
            workingDirectory = null;
        }

        return new ToolchainProcess(UnixSpawner.Start(path, arguments, variables, workingDirectory, spec.Terminal, spec.InheritedDescriptors), spec);
    }

    /// <inheritdoc />
    public bool HasCommand(string command) => Resolve(command) is not null;

    /// <inheritdoc />
    public string? Resolve(string command)
    {
        ArgumentNullException.ThrowIfNull(command);
        return FindProgram(command, CurrentEnvironment()) is { } path ? Path.GetFullPath(path) : null;
    }

    /// <inheritdoc />
    public ProcessStartSpec Shell(string workingDirectory, TerminalSize size)
    {
        if (OperatingSystem.IsWindows())
        {
            var shell = FindProgram("pwsh", CurrentEnvironment()) ?? FindProgram("powershell", CurrentEnvironment()) ?? "cmd.exe";
            return new ProcessStartSpec { Program = shell, WorkingDirectory = workingDirectory, Terminal = size };
        }

        var preferred = Environment.GetEnvironmentVariable("SHELL");
        var program = preferred is { Length: > 0 } && File.Exists(preferred) ? preferred : File.Exists("/bin/bash") ? "/bin/bash" : "/bin/sh";
        return new ProcessStartSpec
        {
            Program = program,
            Arguments = ["-i"],
            WorkingDirectory = workingDirectory,
            Terminal = size,
            Environment = new Dictionary<string, string?> { ["TERM"] = "xterm-256color", ["COLORTERM"] = "truecolor" },
        };
    }

    /// <inheritdoc />
    public void ShareFolder(string folder)
    {
        // Every folder of the computer is visible to its programs.
    }

    /// <inheritdoc />
    public string? ToDevicePath(string programPath, string? workingDirectory)
    {
        ArgumentNullException.ThrowIfNull(programPath);
        var path = Path.IsPathRooted(programPath) || workingDirectory is null ? programPath : Path.Combine(workingDirectory, programPath);
        return File.Exists(path) ? Path.GetFullPath(path) : null;
    }

    /// <summary>Finds a program on a <c>PATH</c>, trying Windows' executable extensions there.</summary>
    /// <param name="program">A command name or a path.</param>
    /// <param name="environment">The environment whose <c>PATH</c> to search.</param>
    internal static string? FindProgram(string program, IReadOnlyDictionary<string, string> environment)
    {
        string[] extensions = OperatingSystem.IsWindows()
            ? ["", .. (Value(environment, "PATHEXT") ?? ".COM;.EXE;.BAT;.CMD").Split(';', StringSplitOptions.RemoveEmptyEntries)]
            : [""];
        if (program.Contains('/', StringComparison.Ordinal) || program.Contains(Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            return extensions.Select(e => program + e).FirstOrDefault(IsExecutable);
        }

        foreach (var folder in (Value(environment, "PATH") ?? string.Empty).Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (var extension in extensions)
            {
                var candidate = Path.Combine(folder, program + extension);
                if (IsExecutable(candidate))
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    private static bool IsExecutable(string path)
    {
        if (!File.Exists(path))
        {
            return false;
        }

        return OperatingSystem.IsWindows() || (File.GetUnixFileMode(path) & (UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute)) != 0;
    }

    private static string? Value(IReadOnlyDictionary<string, string> environment, string name) =>
        environment.FirstOrDefault(e => string.Equals(e.Key, name, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)).Value;

    private static Dictionary<string, string> CurrentEnvironment()
    {
        var result = new Dictionary<string, string>(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            if (entry.Key is string name && entry.Value is string value)
            {
                result[name] = value;
            }
        }

        return result;
    }

    private static Dictionary<string, string> BuildEnvironment(ProcessStartSpec spec)
    {
        var environment = CurrentEnvironment();
        foreach (var (name, value) in spec.Environment)
        {
            if (value is null)
            {
                environment.Remove(name);
            }
            else if (string.Equals(name, "PATH", StringComparison.OrdinalIgnoreCase) && value.Contains("$PATH", StringComparison.Ordinal))
            {
                environment[name] = value.Replace("$PATH", Value(environment, "PATH") ?? string.Empty, StringComparison.Ordinal);
            }
            else
            {
                environment[name] = value;
            }
        }

        if (spec.Terminal is null)
        {
            // Programs on pipes are not in a terminal; tell the ones that check.
            environment.TryAdd("TERM", "dumb");
        }

        return environment;
    }
}
