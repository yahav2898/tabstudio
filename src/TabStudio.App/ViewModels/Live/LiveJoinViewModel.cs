using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.App.Platform;
using TabStudio.App.Services.Live;
using TabStudio.Link;
using TabStudio.Link.Networking;
using TabStudio.Link.Security;
using TabStudio.Live.Sessions;

namespace TabStudio.App.ViewModels.Live;

/// <summary>
/// File &gt; Join Live Session, and Join a Live Session on the start screen: scan the code another TabStudio's Share
/// Session window shows, or type its address and code. Once this device joined, the IDE opens the shared workspace.
/// </summary>
public sealed partial class LiveJoinViewModel : ObservableObject, IDisposable
{
    private readonly LiveService _live;
    private readonly IQrScanner _scanner;
    private CancellationTokenSource? _joining;

    /// <summary>Creates the dialog.</summary>
    /// <param name="live">TabStudio Live on this device.</param>
    /// <param name="scanner">Scans QR codes with the camera, where there is one.</param>
    public LiveJoinViewModel(LiveService live, IQrScanner scanner)
    {
        ArgumentNullException.ThrowIfNull(live);
        ArgumentNullException.ThrowIfNull(scanner);
        _live = live;
        _scanner = scanner;
        _live.Changed += OnLiveChanged;
        _live.GuestCopyProgress += OnCopyProgress;
    }

    /// <summary>TabStudio Live on this device.</summary>
    public LiveService Live => _live;

    /// <summary>Whether the dialog is shown.</summary>
    [ObservableProperty]
    public partial bool IsOpen { get; private set; }

    /// <summary>The host's address, as its Share Session window shows it.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(JoinCommand))]
    public partial string AddressText { get; set; } = string.Empty;

    /// <summary>The code the host's Share Session window shows.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(JoinCommand))]
    public partial string CodeText { get; set; } = string.Empty;

    /// <summary>Whether this device is joining.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(JoinCommand), nameof(ScanCommand))]
    public partial bool IsBusy { get; private set; }

    /// <summary>What joining does now, such as <c>Copying the workspace · 120 of 300 files</c>.</summary>
    [ObservableProperty]
    public partial string ProgressText { get; private set; } = string.Empty;

    /// <summary>Why this device could not join, or why the session ended.</summary>
    [ObservableProperty]
    public partial string? Error { get; set; }

    /// <summary>Whether this device has a camera to scan the host's QR code with.</summary>
    public bool CanScan => _scanner.IsAvailable;

    /// <summary>This device's name, as the others see it.</summary>
    public string DeviceText => $"You join as {_live.DeviceName}";

    /// <summary>Shows the dialog.</summary>
    /// <param name="error">Why it opens again, such as the end of a session, or <see langword="null"/>.</param>
    public void Open(string? error = null)
    {
        Error = error;
        IsOpen = true;
    }

    /// <summary>Joins the session a scanned or pasted invitation names, as the Link dialog's own scan does for a session's code.</summary>
    /// <param name="invitation">The session's invitation.</param>
    public Task<bool> JoinInvitationAsync(LinkInvitation invitation)
    {
        ArgumentNullException.ThrowIfNull(invitation);
        IsOpen = true;
        return JoinWithAsync(LinkTarget.FromInvitation(invitation));
    }

    /// <summary>Joins the session at an address with a code, as typing them does.</summary>
    /// <param name="address">The host's address.</param>
    /// <param name="code">The session's code.</param>
    public async Task<bool> JoinAtAsync(string address, string code)
    {
        AddressText = address;
        CodeText = code;
        await JoinCoreAsync();
        return _live.Guest is { State: LiveGuestState.Joined };
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _live.Changed -= OnLiveChanged;
        _live.GuestCopyProgress -= OnCopyProgress;
        _joining?.Cancel();
        _joining?.Dispose();
    }

    /// <summary>Scans the host's QR code with the camera, then joins its session.</summary>
    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task ScanAsync()
    {
        Error = null;
        string? scanned;
        try
        {
            scanned = await _scanner.ScanAsync(CancellationToken.None);
        }
        catch (UnauthorizedAccessException)
        {
            Error = "TabStudio may not use the camera. Allow it in Android's settings, or type the address and code instead.";
            return;
        }
        catch (InvalidOperationException ex)
        {
            Error = $"The camera did not start: {ex.Message} Type the address and code instead.";
            return;
        }

        if (scanned is null)
        {
            return;
        }

        if (!LinkInvitation.TryParse(scanned, out var invitation) || invitation.Role != LinkRole.Live)
        {
            Error = "That is not a Live session's code. On the other device, choose File > Share Session, then scan the code it shows.";
            return;
        }

        await JoinWithAsync(LinkTarget.FromInvitation(invitation));
    }

    /// <summary>Joins the session at the typed address with the typed code.</summary>
    [RelayCommand(CanExecute = nameof(CanJoin))]
    private Task JoinAsync() => JoinCoreAsync();

    /// <summary>Stops joining.</summary>
    [RelayCommand]
    private void Cancel()
    {
        _joining?.Cancel();
        if (!IsBusy)
        {
            IsOpen = false;
        }
    }

    private async Task JoinCoreAsync()
    {
        Error = null;
        if (!LinkEndpoint.TryParse(AddressText, LiveProtocolPort, out var endpoint))
        {
            Error = "Type the address the host's Share Session window shows, such as 192.168.1.23.";
            return;
        }

        if (!LinkPairingCode.TryParse(CodeText, out var code))
        {
            Error = "Type the eight-character code the host shows, such as 7K3Q-M9XA.";
            return;
        }

        if (await JoinWithAsync(LinkTarget.FromCode(endpoint, code)))
        {
            CodeText = string.Empty;
        }
    }

    private static int LiveProtocolPort => TabStudio.Live.LiveProtocol.DefaultPort;

    private bool CanStart() => !IsBusy;

    private bool CanJoin() => !IsBusy && !string.IsNullOrWhiteSpace(AddressText) && !string.IsNullOrWhiteSpace(CodeText);

    private async Task<bool> JoinWithAsync(LinkTarget target)
    {
        IsBusy = true;
        Error = null;
        ProgressText = "Connecting and checking that it is the right device…";
        _joining?.Dispose();
        _joining = new CancellationTokenSource();
        try
        {
            var joined = await _live.JoinAsync(target, _joining.Token);
            if (joined)
            {
                IsOpen = false;
            }
            else
            {
                Error = _joining.IsCancellationRequested ? null : _live.JoinError;
            }

            return joined;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        finally
        {
            IsBusy = false;
            ProgressText = string.Empty;
        }
    }

    private void OnLiveChanged(object? sender, EventArgs e)
    {
        if (_live.Guest is { State: LiveGuestState.Joining })
        {
            ProgressText = "Joining…";
        }
    }

    private void OnCopyProgress(object? sender, (int Done, int Total) progress) =>
        ProgressText = string.Create(CultureInfo.CurrentCulture, $"Copying the shared files · {progress.Done} of {progress.Total}");
}
