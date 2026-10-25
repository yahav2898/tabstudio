using System.Text;
using TabStudio.Toolchains.Processes;

namespace TabStudio.WindowsCompatibility.Sessions;

/// <summary>A line a Windows program wrote to its console.</summary>
/// <param name="Text">The line, without its line break.</param>
/// <param name="IsError">Whether it came from standard error.</param>
public readonly record struct ProgramLine(string Text, bool IsError);

/// <summary>
/// A Windows app running under Wine: the real <c>dotnet.exe</c> host started on the app's assembly, with what it
/// writes to its console as lines, and its exit.
/// </summary>
public sealed class WindowsProgram : IDisposable
{
    private readonly ToolchainProcess _process;
    private readonly Task _readers;

    internal WindowsProgram(ToolchainProcess process, string name)
    {
        _process = process;
        Name = name;
        _readers = Task.WhenAll(ReadAsync(process.Output, isError: false), process.Error is { } error ? ReadAsync(error, isError: true) : Task.CompletedTask);
        Exited = WaitAsync();
    }

    /// <summary>The app's name.</summary>
    public string Name { get; }

    /// <summary>A line of the app's console output. Raised on a background thread.</summary>
    public event EventHandler<ProgramLine>? LineWritten;

    /// <summary>Completes with the exit code once the app has exited and its output has been read.</summary>
    public Task<ProcessExit> Exited { get; }

    /// <summary>Whether the app has exited.</summary>
    public bool HasExited => _process.HasExited;

    /// <summary>Stops the app: its process and everything it started.</summary>
    public void Stop() => _process.Stop();

    /// <summary>Sends text to the app's standard input.</summary>
    /// <param name="text">The text, such as a line with its line break.</param>
    public async Task WriteInputAsync(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        await _process.Input.WriteAsync(bytes).ConfigureAwait(false);
        await _process.Input.FlushAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_process.HasExited)
        {
            _process.Stop();
        }

        _process.Dispose();
    }

    private async Task<ProcessExit> WaitAsync()
    {
        var exit = await _process.Completion.ConfigureAwait(false);
        try
        {
            await _readers.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            // Something the app started keeps its console open; its exit is what counts.
        }

        return exit;
    }

    private async Task ReadAsync(Stream stream, bool isError)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8);
        try
        {
            while (await reader.ReadLineAsync().ConfigureAwait(false) is { } line)
            {
                if (!WineMessages.IsNoise(line))
                {
                    LineWritten?.Invoke(this, new ProgramLine(line, isError));
                }
            }
        }
        catch (IOException)
        {
            // The process ended.
        }
        catch (ObjectDisposedException)
        {
            // Disposed.
        }
    }
}

/// <summary>Wine's own messages on a program's console, which are not the program's output.</summary>
internal static class WineMessages
{
    /// <summary>
    /// Whether a line is one of Wine's debug messages rather than the app's output, such as
    /// <c>0024:fixme:ole:CoInitializeSecurity stub</c>. Wine's other messages, such as a crash report, stay.
    /// </summary>
    /// <param name="line">The line.</param>
    public static bool IsNoise(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        var colon = line.IndexOf(':', StringComparison.Ordinal);
        if (colon is < 4 or > 8 || line.AsSpan(0, colon).ContainsAnyExcept("0123456789abcdef"))
        {
            return false;
        }

        var rest = line.AsSpan(colon + 1);
        return rest.StartsWith("fixme:", StringComparison.Ordinal) || rest.StartsWith("err:", StringComparison.Ordinal)
            || rest.StartsWith("warn:", StringComparison.Ordinal) || rest.StartsWith("trace:", StringComparison.Ordinal);
    }
}
