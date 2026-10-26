using TabStudio.LanguageServers.Hosting;
using TabStudio.Toolchains.Processes;
using TabStudio.Toolchains.Runtimes;

namespace TabStudio.App.Services.Toolchains;

/// <summary>
/// Starts language servers and debug adapters where the toolchains run their programs: in TabStudio's Linux
/// environment on Android, with the servers and adapters the Toolchain Store installed, and on a desktop computer
/// from its <c>PATH</c>. A thin adapter from the language servers' <see cref="IServerProcessLauncher"/> to the
/// Toolchain Store's <see cref="IToolchainRuntime"/>.
/// </summary>
/// <remarks>
/// In the Linux environment, programs see their own files at other paths than TabStudio does: <see cref="Resolve"/>
/// returns the path a program sees, such as <c>/usr/local/bin/pyright-langserver</c>, and
/// <see cref="ToLocalPath"/> finds that file on the device. Workspace folders are shared with the environment
/// before a server starts in them, so they are at the same path for the server as for the IDE.
/// </remarks>
/// <param name="runtime">Where the programs run.</param>
public sealed class ToolchainServerLauncher(IToolchainRuntime runtime) : IServerProcessLauncher
{
    private readonly IToolchainRuntime _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));

    /// <inheritdoc />
    public string? Resolve(string command) => string.IsNullOrWhiteSpace(command) ? null : _runtime.Resolve(command);

    /// <inheritdoc />
    public IServerProcess Start(ServerStartInfo startInfo)
    {
        ArgumentNullException.ThrowIfNull(startInfo);
        if (Directory.Exists(startInfo.WorkingDirectory))
        {
            _runtime.ShareFolder(startInfo.WorkingDirectory);
        }

        foreach (var address in startInfo.Arguments.Where(a => a.StartsWith("unix:", StringComparison.Ordinal)))
        {
            if (Path.GetDirectoryName(address[5..]) is { } folder && Directory.Exists(folder))
            {
                _runtime.ShareFolder(folder);
            }
        }

        foreach (var folder in startInfo.Arguments.Where(a => Path.IsPathRooted(a) && Directory.Exists(a)))
        {
            _runtime.ShareFolder(folder);
        }

        try
        {
            var process = _runtime.Start(new ProcessStartSpec
            {
                Program = startInfo.FileName,
                Arguments = startInfo.Arguments,
                WorkingDirectory = startInfo.WorkingDirectory,
                Environment = startInfo.Environment,
            });
            return new ToolchainServerProcess(process);
        }
        catch (ToolchainRuntimeException ex)
        {
            throw new ServerStartException(ex.Message, ex);
        }
    }

    /// <inheritdoc />
    public string? ToLocalPath(string programPath)
    {
        ArgumentNullException.ThrowIfNull(programPath);
        return _runtime.ToDevicePath(programPath, workingDirectory: null);
    }

    /// <summary>A server's or adapter's program, as the toolchain runtime started it.</summary>
    /// <param name="process">The program.</param>
    private sealed class ToolchainServerProcess(ToolchainProcess process) : IServerProcess
    {
        public Stream StandardInput => process.Input;

        public Stream StandardOutput => process.Output;

        // Servers always run on pipes, so there is a standard error; a terminal would have merged it into Output.
        public Stream StandardError => process.Error ?? Stream.Null;

        public Task<int> Exited => process.Exited;

        public void Kill() => process.Stop();

        public void Dispose() => process.Dispose();
    }
}
