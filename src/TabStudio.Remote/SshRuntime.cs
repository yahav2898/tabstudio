using Renci.SshNet;
using TabStudio.Toolchains.Processes;
using TabStudio.Toolchains.Runtimes;

namespace TabStudio.Remote;

/// <summary>Runs SSH commands and interactive PTYs through the IDE's existing output and terminal pipelines.</summary>
public sealed class SshRuntime(RemoteWorkspace workspace) : IToolchainRuntime
{
    public string DisplayName => workspace.Connection.Host.DisplayName;
    public RuntimeKind Kind => RuntimeKind.Remote;
    public bool HasCommand(string command) => true;
    public string? Resolve(string command) => command;
    public void ShareFolder(string folder) { }
    public string? ToDevicePath(string programPath, string? workingDirectory) => workspace.ToLocalPath(programPath);
    public ProcessStartSpec Shell(string workingDirectory, TerminalSize size) => new() { Program = "sh", WorkingDirectory = workingDirectory, Terminal = size };

    public ToolchainProcess Start(ProcessStartSpec spec)
    {
        try
        {
            if (!workspace.Connection.IsConnected) { throw new IOException("The SSH connection ended. Reconnect in Remote Explorer."); }
            var folder = spec.WorkingDirectory is null ? workspace.RemoteRoot : workspace.ToRemotePath(spec.WorkingDirectory);
            if (spec.Terminal is { } size)
            {
                var stream = workspace.Connection.Ssh.CreateShellStream("xterm-256color", (uint)size.Columns, (uint)size.Rows, 0, 0, 65536);
                stream.WriteLine("cd " + Quote(folder));
                return new ToolchainProcess(new ShellHandle(stream), spec);
            }
            var environment = string.Join(' ', spec.Environment.Select(v => v.Value is null ? "-u " + Quote(v.Key) : Quote(v.Key + "=" + v.Value)));
            var command = "cd " + Quote(folder) + " && exec env " + environment + " " + string.Join(' ', new[] { spec.Program }.Concat(spec.Arguments).Select(Quote));
            return new ToolchainProcess(new CommandHandle(workspace.Connection.Ssh.CreateCommand(command)), spec);
        }
        catch (Exception ex) when (ex is IOException or Renci.SshNet.Common.SshException or InvalidOperationException)
        {
            throw new ToolchainRuntimeException("Could not start the remote program: " + ex.Message, ex);
        }
    }

    public static string Quote(string text) => "'" + text.Replace("'", "'\\''", StringComparison.Ordinal) + "'";

    private sealed class ShellHandle : IProcessHandle
    {
        private readonly ShellStream _stream;
        private readonly TaskCompletionSource<(int, int?)> _exit = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ShellHandle(ShellStream stream)
        {
            _stream = stream;
            stream.Closed += (_, _) => _exit.TrySetResult((0, null));
            stream.ErrorOccurred += (_, _) => _exit.TrySetResult((1, null));
        }
        public int Id => 0;
        public bool IsTerminal => true;
        public Stream Input => _stream;
        public Stream Output => _stream;
        public Stream? Error => null;
        public Task<(int ExitCode, int? Signal)> WaitForExitAsync() => _exit.Task;
        public void Resize(TerminalSize size) => _stream.ChangeWindowSize((uint)size.Columns, (uint)size.Rows, 0, 0);
        public void CloseInput() => _stream.Write("\u0004");
        public void KillTree() { _stream.Dispose(); _exit.TrySetResult((130, null)); }
        public void Dispose() => KillTree();
    }

    private sealed class CommandHandle : IProcessHandle
    {
        private readonly SshCommand _command;
        private readonly Task _completion;
        private readonly CancellationTokenSource _stop = new();
        private Stream? _input;
        public CommandHandle(SshCommand command)
        {
            _command = command;
            _completion = command.ExecuteAsync(_stop.Token);
        }
        public int Id => 0;
        public bool IsTerminal => false;
        public Stream Input => _input ??= _command.CreateInputStream();
        public Stream Output => _command.OutputStream;
        public Stream? Error => _command.ExtendedOutputStream;
        public async Task<(int ExitCode, int? Signal)> WaitForExitAsync()
        {
            try { await _completion.ConfigureAwait(false); return (_command.ExitStatus ?? 1, null); }
            catch (OperationCanceledException) { return (130, null); }
            catch (Renci.SshNet.Common.SshException) { return (255, null); }
        }
        public void Resize(TerminalSize size) { }
        public void CloseInput() { (_input ??= _command.CreateInputStream()).Dispose(); }
        public void KillTree() => _stop.Cancel();
        public void Dispose() { _stop.Cancel(); _input?.Dispose(); _command.Dispose(); _stop.Dispose(); }
    }
}
