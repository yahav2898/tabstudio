using System.ComponentModel;
using System.Diagnostics;

namespace TabStudio.Toolchains.Processes.Windows;

/// <summary>
/// A process on pipes started with .NET's <see cref="Process"/>, for Windows, where there is no <c>posix_spawn</c>.
/// </summary>
internal sealed class DotNetProcessHandle : IProcessHandle
{
    private readonly Process _process;
    private int _inputClosed;

    private DotNetProcessHandle(Process process)
    {
        _process = process;
    }

    /// <inheritdoc />
    public int Id => _process.Id;

    /// <inheritdoc />
    public bool IsTerminal => false;

    /// <inheritdoc />
    public Stream Input => _process.StandardInput.BaseStream;

    /// <inheritdoc />
    public Stream Output => _process.StandardOutput.BaseStream;

    /// <inheritdoc />
    public Stream? Error => _process.StandardError.BaseStream;

    /// <summary>Starts a program with redirected standard streams.</summary>
    /// <param name="path">The program.</param>
    /// <param name="arguments">Its arguments, without the program's name.</param>
    /// <param name="environment">The complete environment.</param>
    /// <param name="workingDirectory">The folder to start in, or <see langword="null"/>.</param>
    /// <exception cref="ToolchainRuntimeException">The program could not be started.</exception>
    public static DotNetProcessHandle Start(string path, IEnumerable<string> arguments, IReadOnlyDictionary<string, string> environment, string? workingDirectory)
    {
        var info = new ProcessStartInfo(path)
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = workingDirectory ?? string.Empty,
        };
        foreach (var argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        info.Environment.Clear();
        foreach (var (name, value) in environment)
        {
            info.Environment[name] = value;
        }

        try
        {
            return new DotNetProcessHandle(Process.Start(info) ?? throw new ToolchainRuntimeException($"Could not start {path}."));
        }
        catch (Win32Exception ex)
        {
            throw new ToolchainRuntimeException($"Could not start {path}: {ex.Message}", ex.NativeErrorCode, ex);
        }
    }

    /// <inheritdoc />
    public async Task<(int ExitCode, int? Signal)> WaitForExitAsync()
    {
        await _process.WaitForExitAsync().ConfigureAwait(false);
        return (_process.ExitCode, null);
    }

    /// <inheritdoc />
    public void Resize(TerminalSize size)
    {
        // Pipes have no size.
    }

    /// <inheritdoc />
    public void CloseInput()
    {
        if (Interlocked.Exchange(ref _inputClosed, 1) == 0)
        {
            _process.StandardInput.Close();
        }
    }

    /// <inheritdoc />
    public void KillTree()
    {
        try
        {
            _process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // It has already exited.
        }
    }

    /// <inheritdoc />
    public void Dispose() => _process.Dispose();
}
