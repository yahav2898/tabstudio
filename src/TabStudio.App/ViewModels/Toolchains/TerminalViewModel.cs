using System.Collections.Concurrent;
using System.Text;
using System.Threading.Channels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.App.Services;
using TabStudio.App.Services.Toolchains;
using TabStudio.Toolchains.Installation;
using TabStudio.Toolchains.Linux;
using TabStudio.Toolchains.Processes;
using TabStudio.Toolchains.Terminals;

namespace TabStudio.App.ViewModels;

/// <summary>
/// The Terminal panel (View &gt; Terminal, Ctrl+`): a shell in the workspace folder on a pseudo terminal, drawn
/// by a <see cref="TerminalEmulator"/>, with on-screen keys for what a tablet's keyboard lacks.
/// </summary>
/// <remarks>
/// <para>
/// On Android the shell is Alpine's <c>ash</c> in TabStudio's Linux environment, where the toolchains are; the
/// first time, the environment is set up when the user asks. On a desktop computer it is the computer's own shell.
/// </para>
/// <para>
/// The shell's output is read on a background thread and fed to the emulator on the UI thread in batches, so a
/// program printing fast cannot flood the UI. Keys and pastes are written to the shell in order on a writer task.
/// </para>
/// </remarks>
public sealed partial class TerminalViewModel : ObservableObject, IDisposable
{
    private readonly ToolchainServices _toolchains;
    private readonly IUiDispatcher _ui;
    private readonly Func<string> _workingDirectory;
    private readonly Action<string?> _openStore;
    private readonly ConcurrentQueue<byte[]> _output = new();
    private readonly Channel<byte[]> _input = Channel.CreateUnbounded<byte[]>(new UnboundedChannelOptions { SingleReader = true });
    private ToolchainProcess? _process;
    private CancellationTokenSource? _setup;
    private TerminalSize _size = TerminalSize.Default;
    private int _flushScheduled;
    private bool _disposed;
    private bool _startingRemote;

    /// <summary>Starts a remote PTY when an SSH folder is open, instead of the local shell.</summary>
    public Func<TerminalSize, Task<ToolchainProcess>>? RemoteShellFactory { get; set; }

    /// <summary>Prepares local terminal commands after the Linux runtime is ready.</summary>
    public Action? BeforeLocalShell { get; set; }

    /// <summary>Closes the current shell when changing workspaces or disconnecting SSH.</summary>
    public void StopShell()
    {
        var process = _process;
        _process = null;
        process?.Dispose();
        IsRunning = false;
    }

    /// <summary>Creates the panel; the shell starts when the panel is first shown.</summary>
    /// <param name="toolchains">The device's toolchains, whose runtime runs the shell.</param>
    /// <param name="ui">The UI dispatcher.</param>
    /// <param name="workingDirectory">The folder the shell starts in: the workspace's.</param>
    /// <param name="openStore">Opens the Toolchain Store.</param>
    public TerminalViewModel(ToolchainServices toolchains, IUiDispatcher ui, Func<string> workingDirectory, Action<string?> openStore)
    {
        ArgumentNullException.ThrowIfNull(toolchains);
        ArgumentNullException.ThrowIfNull(ui);
        ArgumentNullException.ThrowIfNull(workingDirectory);
        ArgumentNullException.ThrowIfNull(openStore);
        _toolchains = toolchains;
        _ui = ui;
        _workingDirectory = workingDirectory;
        _openStore = openStore;
        Emulator.Responded += (_, bytes) => Write(bytes);
        _ = Task.Run(WriteInputAsync);
    }

    /// <summary>Raised on the UI thread when the screen changed and should be drawn again.</summary>
    public event EventHandler? Changed;

    /// <summary>Raised when the Terminal should take the keyboard focus, such as when View &gt; Terminal opens it.</summary>
    public event EventHandler? FocusRequested;

    /// <summary>Raised on the UI thread with what the shell wrote, as a TabStudio Live host shares it.</summary>
    public event EventHandler<ReadOnlyMemory<byte>>? ShellOutput;

    /// <summary>
    /// Whether the Terminal shows another device's shell, the host's in a TabStudio Live session, instead of starting
    /// one here: what the host's shell writes is fed to <see cref="ShowMirrored"/>, and typing goes to
    /// <see cref="MirrorInput"/>, which the host takes only when it allows this guest to type.
    /// </summary>
    public bool IsMirror => MirrorInput is not null;

    /// <summary>Where typing goes while the Terminal shows the host's shell; <see langword="null"/> for a shell of its own.</summary>
    public Action<byte[]>? MirrorInput { get; private set; }

    /// <summary>The screen.</summary>
    public TerminalEmulator Emulator { get; } = new(TerminalSize.Default.Rows, TerminalSize.Default.Columns, scrollbackLines: 3000);

    /// <summary>Whether a shell runs.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsMessage))]
    public partial bool IsRunning { get; private set; }

    /// <summary>A message instead of the shell, such as why it cannot start.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsMessage))]
    public partial string? Message { get; private set; }

    /// <summary>Whether the Linux environment must be set up before a shell can start.</summary>
    [ObservableProperty]
    public partial bool NeedsSetup { get; private set; }

    /// <summary>Whether the Linux environment is being set up.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SetUpCommand))]
    public partial bool IsSettingUp { get; private set; }

    /// <summary>The setup's progress, from 0 to 100.</summary>
    [ObservableProperty]
    public partial double SetupProgress { get; private set; }

    /// <summary>What the setup does now.</summary>
    [ObservableProperty]
    public partial string SetupText { get; private set; } = string.Empty;

    /// <summary>Whether the on-screen Ctrl key is down: the next key is sent with Control.</summary>
    [ObservableProperty]
    public partial bool IsControlLatched { get; set; }

    /// <summary>Whether the on-screen Alt key is down: the next key is sent with Alt.</summary>
    [ObservableProperty]
    public partial bool IsAltLatched { get; set; }

    /// <summary>The shell's title, such as the folder it is in, or the shell's name.</summary>
    [ObservableProperty]
    public partial string Title { get; private set; } = "Terminal";

    /// <summary>Whether a message is shown instead of the screen.</summary>
    public bool ShowsMessage => !IsRunning && Message is not null;

    /// <summary>Starts the shell, unless one runs or the environment must be set up first.</summary>
    public void EnsureStarted()
    {
        if (_process is null && !IsSettingUp && !_disposed)
        {
            Start();
        }
    }

    /// <summary>Asks the view to take the keyboard focus.</summary>
    public void RequestFocus() => FocusRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>Runs an interactive tool in this terminal, retaining its caller's resource lifetime until exit.</summary>
    /// <param name="spec">A tool such as the MicroPython REPL, with argument-vector parameters.</param>
    /// <param name="title">The terminal tab title.</param>
    /// <param name="cancellationToken">Stops the program and all its children.</param>
    public async Task<ProcessExit> RunProgramAsync(ProcessStartSpec spec, string title, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(spec);
        // Keys must reach this program, not a host's shell this Terminal is mirroring.
        Mirror(null);
        StopShell();
        var process = _toolchains.Runtime.Start(spec with { Terminal = _size });
        _process = process;
        Message = null;
        NeedsSetup = false;
        IsRunning = true;
        Title = title;
        _ = ReadAsync(process);
        _ = WatchAsync(process);
        RequestFocus();
        return await process.WaitAsync(cancellationToken);
    }

    /// <summary>Shows the host's shell instead of one of this device's, or a shell of its own again.</summary>
    /// <param name="input">Where typing goes, or <see langword="null"/> to stop showing the host's shell.</param>
    public void Mirror(Action<byte[]>? input)
    {
        if (input is not null)
        {
            StopShell();
            MirrorInput = input;
            Message = null;
            NeedsSetup = false;
            IsRunning = true;
            Title = "Host's Terminal";
            Emulator.Feed("\u001bc");
        }
        else if (MirrorInput is not null)
        {
            MirrorInput = null;
            IsRunning = false;
            Title = "Terminal";
            Emulator.Feed("\u001bc");
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Names the host's shell in the Terminal's tab, such as whether this guest may type in it.</summary>
    /// <param name="title">The title.</param>
    public void ShowMirrorTitle(string title)
    {
        if (MirrorInput is not null)
        {
            Title = title;
        }
    }

    /// <summary>Shows what the host's shell wrote. Call on the UI thread.</summary>
    /// <param name="data">The bytes, as the host's shell wrote them.</param>
    public void ShowMirrored(ReadOnlyMemory<byte> data)
    {
        if (MirrorInput is null || data.IsEmpty)
        {
            return;
        }

        Emulator.Feed(data.Span);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Types into this device's shell what a guest typed in the host's Terminal, once the host allowed it.</summary>
    /// <param name="input">The bytes, as the guest's keys encoded them.</param>
    public void TypeFromGuest(ReadOnlyMemory<byte> input)
    {
        if (IsRunning && MirrorInput is null && !input.IsEmpty)
        {
            Write(input.ToArray());
        }
    }

    /// <summary>Sends a key, with the latched on-screen modifiers.</summary>
    /// <param name="key">The key.</param>
    /// <param name="modifiers">The modifiers held on the keyboard.</param>
    public void SendKey(TerminalKey key, TerminalModifiers modifiers = TerminalModifiers.None)
    {
        if (!IsRunning)
        {
            if (key == TerminalKey.Enter && _process is null)
            {
                Start();
            }

            return;
        }

        Send(TerminalKeys.Encode(key, modifiers | TakeLatched(), Emulator.ApplicationCursorKeys));
    }

    /// <summary>Sends typed text, with the latched on-screen modifiers: Ctrl then C sends Ctrl+C.</summary>
    /// <param name="text">The text.</param>
    /// <param name="modifiers">The modifiers held on the keyboard.</param>
    public void SendText(string text, TerminalModifiers modifiers = TerminalModifiers.None)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length == 0)
        {
            return;
        }

        if (!IsRunning)
        {
            if (text is "\r" or "\n" && _process is null)
            {
                Start();
            }

            return;
        }

        Send(TerminalKeys.EncodeText(text, modifiers | TakeLatched()));
    }

    /// <summary>Pastes text, bracketed when the shell asked for it.</summary>
    /// <param name="text">The text.</param>
    public void Paste(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (IsRunning && text.Length > 0)
        {
            Send(TerminalKeys.EncodePaste(text, Emulator.BracketedPaste));
        }
    }

    /// <summary>Sets the screen size from the view's size, and tells the shell.</summary>
    /// <param name="rows">Lines that fit.</param>
    /// <param name="columns">Columns that fit.</param>
    public void Resize(int rows, int columns)
    {
        var size = new TerminalSize(rows, columns).Clamped;
        if (size == _size)
        {
            return;
        }

        _size = size;
        Emulator.Resize(size.Rows, size.Columns);
        _process?.Resize(size);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _disposed = true;
        _setup?.Cancel();
        _input.Writer.TryComplete();
        _process?.Dispose();
        _process = null;
    }

    /// <summary>The on-screen keys: Esc, Tab, the arrows, Home, End, Page Up and Page Down.</summary>
    [RelayCommand]
    private void PressKey(string? name)
    {
        if (Enum.TryParse<TerminalKey>(name, ignoreCase: true, out var key))
        {
            SendKey(key);
        }

        RequestFocus();
    }

    /// <summary>The on-screen characters a tablet keyboard hides, such as <c>|</c> and <c>~</c>.</summary>
    [RelayCommand]
    private void Type(string? text)
    {
        if (!string.IsNullOrEmpty(text))
        {
            SendText(text);
        }

        RequestFocus();
    }

    [RelayCommand]
    private void ToggleControl()
    {
        IsControlLatched = !IsControlLatched;
        RequestFocus();
    }

    [RelayCommand]
    private void ToggleAlt()
    {
        IsAltLatched = !IsAltLatched;
        RequestFocus();
    }

    /// <summary>Ends the shell and starts a new one.</summary>
    [RelayCommand]
    private void Restart()
    {
        if (_process is { } process)
        {
            _process = null;
            process.Dispose();
        }

        IsRunning = false;
        Emulator.Feed("\u001bc");
        Start();
        RequestFocus();
    }

    /// <summary>Clears the screen and the scrollback, as Visual Studio's Clear Terminal does.</summary>
    [RelayCommand]
    private void Clear()
    {
        Emulator.ClearScrollback();
        if (IsRunning)
        {
            // Ctrl+L: the shell redraws its prompt on a clean screen.
            Send("\f");
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Sets up the Linux environment the shell runs in, the first time.</summary>
    [RelayCommand(CanExecute = nameof(CanSetUp))]
    private async Task SetUpAsync()
    {
        IsSettingUp = true;
        Message = null;
        _setup = new CancellationTokenSource();
        try
        {
            var progress = new Progress<InstallProgress>(p =>
            {
                SetupProgress = (p.Fraction ?? 0) * 100;
                SetupText = p.BytesTotal > 0 ? $"{p.Message} · {DiskSpace.Format(p.BytesDone)} of {DiskSpace.Format(p.BytesTotal)}" : p.Message;
            });
            await _toolchains.Manager.PrepareRuntimeAsync(progress, _setup.Token);
            NeedsSetup = false;
            Start();
        }
        catch (ToolchainInstallException ex)
        {
            Message = ex.Message;
        }
        catch (OperationCanceledException)
        {
            Message = SetupMessage;
        }
        finally
        {
            IsSettingUp = false;
            _setup.Dispose();
            _setup = null;
        }
    }

    private bool CanSetUp() => !IsSettingUp;

    [RelayCommand]
    private void OpenStore() => _openStore(null);

    private string SetupMessage =>
        $"The Terminal runs in TabStudio's Linux environment, {_toolchains.Catalog.Linux.DisplayName}, with the toolchains you install. Set it up to start a shell; it downloads about {DiskSpace.Format(_toolchains.Catalog.Linux.Rootfs.GetValueOrDefault(_toolchains.Platform.Architecture ?? string.Empty)?.Size ?? 0)}.";

    private void Start()
    {
        if (MirrorInput is not null)
        {
            return;
        }

        if (RemoteShellFactory is { } remote)
        {
            if (!_startingRemote) { _ = StartRemoteAsync(remote); }
            return;
        }
        if (_toolchains.Manager.Problem is { } problem)
        {
            Message = problem;
            return;
        }

        if (!_toolchains.Manager.IsRuntimeReady)
        {
            NeedsSetup = true;
            Message = SetupMessage;
            return;
        }

        var folder = _workingDirectory();
        try
        {
            BeforeLocalShell?.Invoke();
            _toolchains.ShareFolder(folder);
            var spec = _toolchains.Runtime.Shell(Directory.Exists(folder) ? folder : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), _size);
            _process = _toolchains.Runtime.Start(spec);
        }
        catch (Exception ex) when (ex is ToolchainRuntimeException or IOException or UnauthorizedAccessException or System.Net.Sockets.SocketException)
        {
            Message = ex.Message;
            return;
        }

        Message = null;
        NeedsSetup = false;
        IsRunning = true;
        Title = Path.GetFileName(_process.Spec.Program);
        _ = ReadAsync(_process);
        _ = WatchAsync(_process);
    }

    private async Task StartRemoteAsync(Func<TerminalSize, Task<ToolchainProcess>> factory)
    {
        _startingRemote = true;
        try
        {
            var process = await factory(_size);
            if (_disposed || !ReferenceEquals(RemoteShellFactory, factory)) { process.Dispose(); return; }
            _process = process;
            Message = null; NeedsSetup = false; IsRunning = true; Title = "SSH Terminal";
            _ = ReadAsync(process);
            _ = WatchAsync(process);
        }
        catch (Exception ex) when (ex is ToolchainRuntimeException or IOException or OperationCanceledException or TabStudio.Containers.Runtime.ContainerException) { Message = ex.Message; }
        finally { _startingRemote = false; }
    }

    private async Task ReadAsync(ToolchainProcess process)
    {
        var buffer = new byte[16384];
        try
        {
            int read;
            while ((read = await process.Output.ReadAsync(buffer).ConfigureAwait(false)) > 0)
            {
                _output.Enqueue(buffer.AsSpan(0, read).ToArray());
                if (Interlocked.Exchange(ref _flushScheduled, 1) == 0)
                {
                    _ui.Post(Flush);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
            // The shell ended.
        }
    }

    private async Task WatchAsync(ToolchainProcess process)
    {
        var exit = await process.Completion.ConfigureAwait(false);
        _ui.Post(() =>
        {
            Flush();
            if (!ReferenceEquals(_process, process))
            {
                return;
            }

            _process = null;
            process.Dispose();
            IsRunning = false;
            var reason = exit.WasKilledBySystem
                ? "Android stopped the shell (signal 9), as it does when memory runs low or an app runs many processes"
                : $"The shell {exit.Description}";
            Emulator.Feed($"\r\n\u001b[2m[{reason}. Press Enter to start a new one.]\u001b[m\r\n");
            Changed?.Invoke(this, EventArgs.Empty);
        });
    }

    /// <summary>Feeds what the shell wrote to the emulator. Runs on the UI thread.</summary>
    private void Flush()
    {
        Interlocked.Exchange(ref _flushScheduled, 0);
        var any = false;
        while (_output.TryDequeue(out var chunk))
        {
            Emulator.Feed(chunk);
            ShellOutput?.Invoke(this, chunk);
            any = true;
        }

        if (any)
        {
            if (Emulator.Title is { Length: > 0 } title)
            {
                Title = title;
            }

            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    private TerminalModifiers TakeLatched()
    {
        var modifiers = (IsControlLatched ? TerminalModifiers.Control : TerminalModifiers.None) | (IsAltLatched ? TerminalModifiers.Alt : TerminalModifiers.None);
        IsControlLatched = false;
        IsAltLatched = false;
        return modifiers;
    }

    private void Send(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        if (MirrorInput is { } mirror)
        {
            mirror(bytes);
            return;
        }

        Write(bytes);
    }

    private void Write(byte[] bytes)
    {
        // The emulator's own answers belong to a shell of this device's, not to the host's.
        if (MirrorInput is null)
        {
            _input.Writer.TryWrite(bytes);
        }
    }

    /// <summary>Writes keys and pastes to the shell in order, off the UI thread, since a busy shell may not read them at once.</summary>
    private async Task WriteInputAsync()
    {
        await foreach (var bytes in _input.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            try
            {
                if (_process is { } process)
                {
                    await process.Input.WriteAsync(bytes).ConfigureAwait(false);
                    await process.Input.FlushAsync().ConfigureAwait(false);
                }
            }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException)
            {
                // The shell ended; Enter starts a new one.
            }
        }
    }
}

