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
        private readonly ShellReadStream _output;
        private readonly TaskCompletionSource<(int, int?)> _exit = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ShellHandle(ShellStream stream)
        {
            _stream = stream;
            _output = new ShellReadStream(stream);
            stream.Closed += (_, _) => _exit.TrySetResult((0, null));
            stream.ErrorOccurred += (_, _) => _exit.TrySetResult((1, null));
        }
        public int Id => 0;
        public bool IsTerminal => true;
        public Stream Input => _stream;
        public Stream Output => _output;
        public Stream? Error => null;
        public Task<(int ExitCode, int? Signal)> WaitForExitAsync() => _exit.Task;
        public void Resize(TerminalSize size) => _stream.ChangeWindowSize((uint)size.Columns, (uint)size.Rows, 0, 0);
        public void CloseInput() => _stream.Write("\u0004");
        public void KillTree() { _stream.Dispose(); _exit.TrySetResult((130, null)); }
        public void Dispose() { KillTree(); _output.Dispose(); }
    }

    // ShellStream is synchronous. Its inherited Stream.ReadAsync and WriteAsync share an async lock, so a
    // pending read can prevent the next key from being written. Wait for data events and read only available
    // bytes instead, on a separate stream, preserving raw UTF-8 bytes and cancellation without blocking input.
    private sealed class ShellReadStream : Stream
    {
        private readonly ShellStream _shell;
        private readonly SemaphoreSlim _available = new(0);
        private readonly Lock _readGate = new();
        private bool _closed;
        public ShellReadStream(ShellStream shell)
        {
            _shell = shell;
            shell.DataReceived += OnData;
            shell.Closed += OnClosed;
            shell.ErrorOccurred += OnError;
            _closed = !shell.CanWrite;
        }
        private void OnData(object? sender, Renci.SshNet.Common.ShellDataEventArgs args) => _available.Release();
        private void OnClosed(object? sender, EventArgs args) { lock (_readGate) { _closed = true; } _available.Release(); }
        private void OnError(object? sender, Renci.SshNet.Common.ExceptionEventArgs args) => OnClosed(sender, args);
        public override bool CanRead => true;
        public override bool CanWrite => false;
        public override bool CanSeek => false;
        public override long Length => _shell.Length;
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (buffer.Length == 0) { return 0; }
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                lock (_readGate)
                {
                    var count = (int)Math.Min(buffer.Length, _shell.Length);
                    if (count > 0) { return _shell.Read(buffer.Span[..count]); }
                    if (_closed || !_shell.CanWrite) { return 0; }
                }
                await _available.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _shell.DataReceived -= OnData; _shell.Closed -= OnClosed; _shell.ErrorOccurred -= OnError;
                OnClosed(this, EventArgs.Empty);
            }
            base.Dispose(disposing);
        }
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
        public void CloseInput()
        {
            if (_input is null)
            {
                if (_completion.IsCompleted) { return; }
                try { _input = _command.CreateInputStream(); }
                // SSH.NET 2026.0.0 closes its channel before completing ExecuteAsync. A command can
                // exit between the task check and stream creation; its closed channel already has EOF.
                // Match only that state: an existing stream or other invalid operation must still fail.
                catch (InvalidOperationException ex) when (ex.Message == "The input stream can be used only during execution.") { return; }
            }
            _input.Dispose();
        }
        public void KillTree() => _stop.Cancel();
        public void Dispose() { _stop.Cancel(); _input?.Dispose(); _command.Dispose(); _stop.Dispose(); }
    }
}
