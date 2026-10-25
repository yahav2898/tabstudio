using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.App.Services;
using TabStudio.App.Services.Link;
using TabStudio.App.Services.WindowsBridge;
using TabStudio.Link;
using TabStudio.WindowsBridge.Client;
using TabStudio.WindowsBridge.Frames;
using TabStudio.WindowsBridge.Input;
using TabStudio.WindowsBridge.Protocol;

namespace TabStudio.App.ViewModels.WindowsBridge;

/// <summary>Takes the input of the Bridge pane's picture of a window, for the program on the PC.</summary>
public interface IBridgeInputSink
{
    /// <summary>Sends input to the window shown, in order.</summary>
    /// <param name="events">The input, positions in the window's pixels.</param>
    void Send(IReadOnlyList<BridgeInputEvent> events);
}

/// <summary>
/// The Windows Bridge pane in the side bar: the programs running on the PC, with a picker for their windows and
/// dialogs, the chosen window streamed live, and what goes back to it: touch, keys, the pane's size, the clipboard.
/// </summary>
public sealed partial class BridgePaneViewModel : ObservableObject, IBridgeInputSink, IDisposable
{
    private readonly WindowsBridgeService _bridge;
    private readonly IUiDispatcher _ui;
    private int _streamVersion;
    private string? _lastExit;
    private bool _disposed;

    /// <summary>Creates the pane.</summary>
    /// <param name="bridge">The app's Windows Bridge.</param>
    /// <param name="ui">Gets the bridge's events onto the UI thread.</param>
    public BridgePaneViewModel(WindowsBridgeService bridge, IUiDispatcher ui)
    {
        ArgumentNullException.ThrowIfNull(bridge);
        ArgumentNullException.ThrowIfNull(ui);
        _bridge = bridge;
        _ui = ui;
        _bridge.Changed += OnBridgeChanged;
        _bridge.ConnectionChanged += OnConnectionChanged;
        _bridge.Client.AppStarted += OnAppStarted;
        _bridge.Client.AppWindowsChanged += OnAppWindowsChanged;
        _bridge.Client.AppExited += OnAppExited;
        Refresh();
    }

    /// <summary>Raised on the UI thread when a new picture of the shown window is ready to draw.</summary>
    public event EventHandler? FrameReady;

    /// <summary>Raised when the pane asks to rebuild and restart a program, as after Restart.</summary>
    public event EventHandler<BridgeAppViewModel>? RestartRequested;

    /// <summary>Raised when the user wants to type: the view focuses the picture, which brings the on-screen keyboard.</summary>
    public event EventHandler? KeyboardRequested;

    /// <summary>The programs running on the PC for this tablet.</summary>
    public ObservableCollection<BridgeAppViewModel> Apps { get; } = [];

    /// <summary>The program shown.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title), nameof(HasSelectedApp))]
    [NotifyCanExecuteChangedFor(nameof(StopAppCommand), nameof(RestartAppCommand))]
    public partial BridgeAppViewModel? SelectedApp { get; set; }

    /// <summary>The window shown.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(FitWindowCommand), nameof(ShowKeyboardCommand))]
    public partial BridgeWindowOption? SelectedWindow { get; set; }

    /// <summary>The stream of the window shown, which the view draws.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsStreaming))]
    public partial BridgeStreamReceiver? Stream { get; private set; }

    /// <summary>Whether a window is streaming.</summary>
    public bool IsStreaming => Stream is not null;

    /// <summary>Whether a program is chosen.</summary>
    public bool HasSelectedApp => SelectedApp is not null;

    /// <summary>Whether a program has run on the PC from this workspace, or a PC is paired, which shows the pane's tab.</summary>
    [ObservableProperty]
    public partial bool IsAvailable { get; private set; }

    /// <summary>The pane's header: the program's name, or the pane's.</summary>
    public string Title => SelectedApp?.Name ?? "Windows Bridge";

    /// <summary>Where it runs and how it streams, such as <c>On DESKTOP-7Q2M · 12 ms · Sharp</c>.</summary>
    [ObservableProperty]
    public partial string StatusText { get; private set; } = string.Empty;

    /// <summary>What the pane says over the picture when it cannot show the program: the PC is away, or nothing runs.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMessage))]
    public partial string? Message { get; private set; }

    /// <summary>The message's headline.</summary>
    [ObservableProperty]
    public partial string MessageTitle { get; private set; } = string.Empty;

    /// <summary>Whether the pane shows a message.</summary>
    public bool HasMessage => Message is not null;

    /// <summary>Whether the PC is asleep or away, so the last picture is dimmed under the message.</summary>
    [ObservableProperty]
    public partial bool IsPcAway { get; private set; }

    /// <summary>The PC's pixels per device-independent pixel of the shown window, so it is drawn at its own size.</summary>
    [ObservableProperty]
    public partial double WindowScale { get; private set; } = 1;

    /// <summary>The pane's size in device-independent pixels, which streams and "Fit window to pane" use.</summary>
    public (double Width, double Height) PaneSize { get; set; } = (800, 600);

    /// <summary>The tablet's pixels per device-independent pixel, so streams send no more pixels than the pane shows.</summary>
    public double RenderScale { get; set; } = 1;

    /// <summary>What the last clipboard action did, for the pane's footer.</summary>
    [ObservableProperty]
    public partial string? ClipboardNote { get; private set; }

    /// <summary>Shows a program that just started on the PC, as Run and Start Debugging do.</summary>
    /// <param name="started">The program.</param>
    public void Show(AppStarted started)
    {
        ArgumentNullException.ThrowIfNull(started);
        var app = Apps.FirstOrDefault(a => a.AppId == started.AppId);
        if (app is null)
        {
            app = new BridgeAppViewModel(started);
            Apps.Add(app);
        }

        SelectedApp = app;
        IsAvailable = true;
        Refresh();
    }

    /// <inheritdoc />
    public void Send(IReadOnlyList<BridgeInputEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        if (SelectedApp is not { } app || SelectedWindow is not { } window || events.Count == 0 || !_bridge.IsConnected)
        {
            return;
        }

        try
        {
            _bridge.Client.SendInput(app.AppId, window.Id, events);
        }
        catch (LinkConnectionLostException)
        {
            // The PC left; the pane says so.
        }
    }

    /// <summary>The PC's clipboard text, for Copy from PC.</summary>
    public async Task<string?> ReadPcClipboardAsync()
    {
        if (!_bridge.IsConnected)
        {
            return null;
        }

        var text = await _bridge.Client.GetClipboardAsync();
        ClipboardNote = text is null ? "The PC's clipboard has no text." : "Copied the PC's clipboard to this tablet.";
        return text;
    }

    /// <summary>Puts text on the PC's clipboard, for Paste to PC.</summary>
    /// <param name="text">The tablet's clipboard text.</param>
    public async Task WritePcClipboardAsync(string? text)
    {
        if (!_bridge.IsConnected)
        {
            return;
        }

        if (string.IsNullOrEmpty(text))
        {
            ClipboardNote = "This tablet's clipboard has no text.";
            return;
        }

        await _bridge.Client.SetClipboardAsync(text);
        ClipboardNote = "Put this tablet's clipboard on the PC. Press Ctrl+V in the program to paste it.";
    }

    /// <summary>Called by the view after it drew a picture: the bridge may send the next one.</summary>
    public void OnFrameDrawn() => Stream?.Shown();

    /// <summary>Forgets the programs and stops streaming, when the workspace closes.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _bridge.Changed -= OnBridgeChanged;
        _bridge.ConnectionChanged -= OnConnectionChanged;
        _bridge.Client.AppStarted -= OnAppStarted;
        _bridge.Client.AppWindowsChanged -= OnAppWindowsChanged;
        _bridge.Client.AppExited -= OnAppExited;
        StopStream();
    }

    /// <summary>Stops the program shown.</summary>
    [RelayCommand(CanExecute = nameof(HasSelectedApp))]
    private async Task StopAppAsync()
    {
        if (SelectedApp is { } app && _bridge.IsConnected)
        {
            await _bridge.Client.StopAsync(app.AppId);
        }
    }

    /// <summary>Builds the program again and starts it over.</summary>
    [RelayCommand(CanExecute = nameof(HasSelectedApp))]
    private void RestartApp()
    {
        if (SelectedApp is { } app)
        {
            RestartRequested?.Invoke(this, app);
        }
    }

    /// <summary>Resizes the window on the PC to fill the pane.</summary>
    [RelayCommand(CanExecute = nameof(HasSelectedWindow))]
    private async Task FitWindowAsync()
    {
        if (SelectedApp is not { } app || SelectedWindow is not { } window || !_bridge.IsConnected)
        {
            return;
        }

        var scale = Math.Max(window.Window.Scale, 0.5);
        var width = (int)Math.Round(PaneSize.Width * scale);
        var height = (int)Math.Round(PaneSize.Height * scale);
        await _bridge.Client.ResizeAsync(app.AppId, window.Id, width, height);
    }

    /// <summary>Brings up the keyboard for typing into the program.</summary>
    [RelayCommand(CanExecute = nameof(HasSelectedWindow))]
    private void ShowKeyboard() => KeyboardRequested?.Invoke(this, EventArgs.Empty);

    private static T? Newest<T>(ObservableCollection<T> items)
        where T : class => items.Count > 0 ? items[^1] : null;

    private bool HasSelectedWindow() => SelectedWindow is not null;

    partial void OnSelectedAppChanged(BridgeAppViewModel? value)
    {
        var newest = value is null ? null : Newest(value.Windows);
        if (SelectedWindow?.Id != newest?.Id || value is null)
        {
            SelectedWindow = newest;
        }
    }

    partial void OnSelectedWindowChanged(BridgeWindowOption? oldValue, BridgeWindowOption? newValue)
    {
        WindowScale = newValue is { Window.Scale: > 0 } option ? option.Window.Scale : 1;
        if (oldValue?.Id != newValue?.Id || Stream is null)
        {
            _ = RestartStreamAsync();
        }
    }

    private async Task RestartStreamAsync()
    {
        var version = ++_streamVersion;
        StopStream();
        if (SelectedApp is not { } app || SelectedWindow is not { } window || !_bridge.IsConnected)
        {
            Refresh();
            return;
        }

        var maxWidth = (int)Math.Max(640, Math.Round(PaneSize.Width * RenderScale));
        var maxHeight = (int)Math.Max(480, Math.Round(PaneSize.Height * RenderScale));
        BridgeStreamReceiver receiver;
        try
        {
            receiver = await BridgeStreamReceiver.StartAsync(_bridge.Client, app.AppId, window.Id, maxWidth, maxHeight);
        }
        catch (Exception ex) when (ex is LinkRemoteException or LinkConnectionLostException)
        {
            Refresh();
            return;
        }

        if (version != _streamVersion || _disposed)
        {
            receiver.Dispose();
            return;
        }

        receiver.FrameReady += (_, _) => _ui.Post(() => OnReceiverFrame(receiver));
        receiver.Ended += (_, reason) => _ui.Post(() => OnStreamEnded(receiver, reason));
        Stream = receiver;
        Refresh();
        if (receiver.FrameCount > 0)
        {
            // The first frame came with the answer, before anyone listened for it.
            OnReceiverFrame(receiver);
        }
    }

    private void OnReceiverFrame(BridgeStreamReceiver receiver)
    {
        if (!ReferenceEquals(receiver, Stream))
        {
            return;
        }

        if (receiver.Error is not null)
        {
            // The picture got out of step: start the stream over, with a whole picture.
            _ = RestartStreamAsync();
            return;
        }

        FrameReady?.Invoke(this, EventArgs.Empty);
        RefreshStatus();
    }

    private void OnStreamEnded(BridgeStreamReceiver receiver, string? reason)
    {
        if (!ReferenceEquals(receiver, Stream))
        {
            return;
        }

        StopStream();
        if (reason is not null && SelectedWindow is not null)
        {
            // The capture failed, not the window: try once more after a moment, which picks the PC's other way to capture.
            _ = RetryStreamAsync(_streamVersion);
        }

        Refresh();
    }

    private async Task RetryStreamAsync(int version)
    {
        await Task.Delay(TimeSpan.FromSeconds(1));
        if (version == _streamVersion && Stream is null && !_disposed)
        {
            await RestartStreamAsync();
        }
    }

    private void StopStream()
    {
        var stream = Stream;
        Stream = null;
        stream?.Dispose();
    }

    private void OnAppStarted(object? sender, AppStarted started) => _ui.Post(() =>
    {
        if (_disposed)
        {
            return;
        }

        if (Apps.All(a => a.AppId != started.AppId))
        {
            Apps.Add(new BridgeAppViewModel(started));
            IsAvailable = true;
            SelectedApp ??= Apps[^1];
        }
    });

    private void OnAppWindowsChanged(object? sender, AppWindows windows) => _ui.Post(() =>
    {
        if (_disposed || Apps.FirstOrDefault(a => a.AppId == windows.AppId) is not { } app)
        {
            return;
        }

        var added = app.SetWindows(windows.Windows);
        if (!ReferenceEquals(app, SelectedApp))
        {
            return;
        }

        // A new dialog comes to the front, as it does on the PC; otherwise the window shown stays, with its new size.
        var shown = added.LastOrDefault(w => w.Window.IsDialog)
            ?? app.Windows.FirstOrDefault(w => w.Id == SelectedWindow?.Id)
            ?? Newest(app.Windows);
        SelectedWindow = shown;
        Refresh();
    });

    private void OnAppExited(object? sender, AppExited exited) => _ui.Post(() =>
    {
        if (_disposed || Apps.FirstOrDefault(a => a.AppId == exited.AppId) is not { } app)
        {
            return;
        }

        Apps.Remove(app);
        if (ReferenceEquals(app, SelectedApp))
        {
            SelectedApp = Newest(Apps);
            _lastExit = exited.ExitCode is { } code ? $"{app.Name} exited with code {code}." : $"{app.Name} stopped.";
        }

        Refresh();
    });

    private void OnConnectionChanged(object? sender, EventArgs e) => _ui.Post(() =>
    {
        if (_disposed)
        {
            return;
        }

        if (_bridge.IsConnected)
        {
            // The PC is back: programs that ended while it was away are gone, and the window shown streams again.
            var running = _bridge.Status?.Apps.Select(a => a.AppId).ToHashSet();
            if (running is not null)
            {
                foreach (var gone in Apps.Where(a => !running.Contains(a.AppId)).ToList())
                {
                    Apps.Remove(gone);
                }

                if (SelectedApp is not null && !Apps.Contains(SelectedApp))
                {
                    SelectedApp = Newest(Apps);
                }
            }

            _ = RestartStreamAsync();
        }
        else
        {
            StopStream();
        }

        Refresh();
    });

    private void OnBridgeChanged(object? sender, EventArgs e) => Refresh();

    private void Refresh()
    {
        IsAvailable = IsAvailable || _bridge.HasPairedPc || Apps.Count > 0;
        var pc = _bridge.PcName ?? "the PC";
        IsPcAway = _bridge.HasPairedPc && !_bridge.IsConnected && Apps.Count > 0;
        if (IsPcAway)
        {
            MessageTitle = _bridge.IsPcAsleep ? $"{pc} is asleep or away" : $"Reconnecting to {pc}";
            Message = "Keep working here. The program keeps running on the PC, and it shows here again as soon as the PC is back.";
        }
        else if (!_bridge.HasPairedPc)
        {
            MessageTitle = "No Windows PC";
            Message = "Pair a Windows PC you own in File > TabStudio Link > Windows PC. Windows-only projects then build and run on it, and show here.";
        }
        else if (SelectedApp is null)
        {
            MessageTitle = _lastExit ?? $"Nothing runs on {pc}";
            Message = "Press F5 on a WPF, Windows Forms, WinUI, .NET Framework or C++ project, or pick \"Windows Bridge\" in the Run button's menu for any project.";
        }
        else if (!SelectedApp.HasWindows)
        {
            MessageTitle = $"{SelectedApp.Name} is starting";
            Message = "Its window shows here as soon as it opens on the PC. Its console output is in the Output panel.";
        }
        else
        {
            Message = null;
            _lastExit = null;
        }

        RefreshStatus();
    }

    private void RefreshStatus()
    {
        var pc = _bridge.PcName ?? "the PC";
        var parts = new List<string> { $"On {pc}" };
        if (LinkDisplay.Latency(_bridge.Latency) is { } latency)
        {
            parts.Add(latency);
        }

        if (Stream is { FrameCount: > 0 } stream)
        {
            parts.Add(FrameQuality.Describe(stream.Level));
        }

        StatusText = string.Join(" · ", parts);
    }
}
