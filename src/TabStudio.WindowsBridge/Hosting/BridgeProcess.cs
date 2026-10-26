using System.ComponentModel;
using System.Diagnostics;

namespace TabStudio.WindowsBridge.Hosting;

/// <summary>What the bridge starts: a build, a program it runs, or the debug adapter.</summary>
/// <param name="FileName">The program, a full path or a name on the PATH.</param>
/// <param name="Arguments">Its arguments, each passed as one argument.</param>
/// <param name="WorkingDirectory">The folder it starts in.</param>
/// <param name="Environment">Variables to set on top of the bridge's environment; a <see langword="null"/> value removes one.</param>
/// <param name="BinaryStreams">Whether its standard input and output carry bytes, as a debug adapter's do, rather than lines of text.</param>
public sealed record BridgeProcessStart(
    string FileName,
    IReadOnlyList<string> Arguments,
    string WorkingDirectory,
    IReadOnlyDictionary<string, string?>? Environment = null,
    bool BinaryStreams = false)
{
    /// <summary>Exact Windows cmd command line for a vendor batch entrypoint; never used for normal executable arguments.</summary>
    public string? WindowsCommandLine { get; init; }
}

/// <summary>A process the bridge started could not start.</summary>
public sealed class BridgeProcessException : Exception
{
    /// <summary>Creates the exception.</summary>
    public BridgeProcessException()
        : base("The program could not be started.")
    {
    }

    /// <summary>Creates the exception with a message.</summary>
    /// <param name="message">What went wrong, in words for the user.</param>
    public BridgeProcessException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with a message and its cause.</summary>
    /// <param name="message">What went wrong, in words for the user.</param>
    /// <param name="innerException">The cause.</param>
    public BridgeProcessException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// A process the bridge started, with its output as lines (or, for the debug adapter, its standard streams as bytes)
/// and its exit. Disposing it ends the process and every process it started.
/// </summary>
public sealed class BridgeProcess : IDisposable
{
    private readonly Process _process;
    private readonly TaskCompletionSource<int> _exited = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _disposed;

    private BridgeProcess(Process process)
    {
        _process = process;
    }

    /// <summary>Raised on the thread pool with each line the process writes to its standard output, in text mode.</summary>
    public event EventHandler<string>? OutputLine;

    /// <summary>Raised on the thread pool with each line the process writes to its standard error.</summary>
    public event EventHandler<string>? ErrorLine;

    /// <summary>The process's id.</summary>
    public int Id { get; private set; }

    /// <summary>Completes with the exit code once the process exited and its output was read.</summary>
    public Task<int> Exited => _exited.Task;

    /// <summary>Its standard input, in binary mode.</summary>
    public Stream StandardInput => _process.StandardInput.BaseStream;

    /// <summary>Its standard output, in binary mode.</summary>
    public Stream StandardOutput => _process.StandardOutput.BaseStream;

    /// <summary>Starts a process. Read its lines by subscribing before calling <see cref="BeginReading"/>.</summary>
    /// <param name="start">What to start.</param>
    /// <exception cref="BridgeProcessException">It could not start, such as when the program is not installed.</exception>
    public static BridgeProcess Start(BridgeProcessStart start)
    {
        ArgumentNullException.ThrowIfNull(start);
        var info = new ProcessStartInfo(start.FileName)
        {
            WorkingDirectory = start.WorkingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            CreateNoWindow = true,
        };
        if (start.WindowsCommandLine is { } commandLine)
        {
            if (!OperatingSystem.IsWindows()) throw new BridgeProcessException("A Windows batch entrypoint requires Windows.");
            info.Arguments = commandLine;
        }
        else
        {
            foreach (var argument in start.Arguments) info.ArgumentList.Add(argument);
        }

        if (!start.BinaryStreams)
        {
            info.StandardOutputEncoding = System.Text.Encoding.UTF8;
            info.StandardErrorEncoding = System.Text.Encoding.UTF8;
        }

        // Builds and programs answer in English, as the error codes and the Output panel's other lines are.
        info.Environment["DOTNET_CLI_UI_LANGUAGE"] = "en";
        info.Environment["VSLANG"] = "1033";
        info.Environment["DOTNET_NOLOGO"] = "1";
        info.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        info.Environment["MSBUILDTERMINALLOGGER"] = "off";
        if (start.Environment is { } environment)
        {
            foreach (var (name, value) in environment)
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
        }

        var process = new Process { StartInfo = info, EnableRaisingEvents = true };
        try
        {
            if (!process.Start())
            {
                throw new BridgeProcessException($"{Path.GetFileName(start.FileName)} did not start.");
            }
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or PlatformNotSupportedException)
        {
            process.Dispose();
            throw new BridgeProcessException($"{Path.GetFileName(start.FileName)} could not be started: {ex.Message}", ex);
        }

        var started = new BridgeProcess(process) { Id = process.Id };
        if (start.BinaryStreams)
        {
            process.ErrorDataReceived += (_, e) => started.OnLine(e.Data, isError: true);
            process.BeginErrorReadLine();
            process.Exited += (_, _) => started.OnExited();
        }

        return started;
    }

    /// <summary>Starts reading the process's lines and watching for its exit, in text mode.</summary>
    public void BeginReading()
    {
        var output = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var error = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is null)
            {
                output.TrySetResult();
            }
            else
            {
                OutputLine?.Invoke(this, e.Data);
            }
        };
        _process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is null)
            {
                error.TrySetResult();
            }
            else
            {
                ErrorLine?.Invoke(this, e.Data);
            }
        };
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();
        _ = Task.Run(async () =>
        {
            try
            {
                await _process.WaitForExitAsync().ConfigureAwait(false);
                await Task.WhenAny(Task.WhenAll(output.Task, error.Task), Task.Delay(TimeSpan.FromSeconds(5))).ConfigureAwait(false);
            }
            catch (InvalidOperationException)
            {
                // Disposed while waiting.
            }

            OnExited();
        });
    }

    /// <summary>Ends the process and the processes it started, at once.</summary>
    public void Kill()
    {
        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception or NotSupportedException)
        {
            // It ended meanwhile, or cannot be ended; either way nothing more can be done.
        }
    }

    /// <summary>Ends the process, if it still runs, and releases it.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        Kill();
        _process.Dispose();
        _exited.TrySetResult(-1);
    }

    private void OnLine(string? line, bool isError)
    {
        if (line is not null)
        {
            (isError ? ErrorLine : OutputLine)?.Invoke(this, line);
        }
    }

    private void OnExited()
    {
        int code;
        try
        {
            code = _process.ExitCode;
        }
        catch (InvalidOperationException)
        {
            code = -1;
        }

        _exited.TrySetResult(code);
    }
}
