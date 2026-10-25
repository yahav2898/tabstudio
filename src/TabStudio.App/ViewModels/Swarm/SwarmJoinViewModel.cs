using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.App.Platform;
using TabStudio.App.Swarm;
using TabStudio.Link.Networking;
using TabStudio.Link.Security;
using TabStudio.Swarm.Device;

namespace TabStudio.App.ViewModels.Swarm;

/// <summary>
/// The "Join a swarm" half of the TabStudio Link dialog, on a phone or tablet that runs the app a tablet builds: scan
/// the code the tablet's Swarm window shows, or type its address and code, or rejoin a swarm this device paired with.
/// Once it joins, the device shows the app full screen (<see cref="SwarmDevicePageViewModel"/>).
/// </summary>
public sealed partial class SwarmJoinViewModel : ObservableObject, IDisposable
{
    private readonly SwarmMemberService _member;
    private readonly IQrScanner _scanner;
    private CancellationTokenSource? _joining;

    /// <summary>Creates the half of the dialog.</summary>
    /// <param name="member">This device's side of a swarm.</param>
    /// <param name="scanner">Scans QR codes with the camera, where there is one.</param>
    public SwarmJoinViewModel(SwarmMemberService member, IQrScanner scanner)
    {
        ArgumentNullException.ThrowIfNull(member);
        ArgumentNullException.ThrowIfNull(scanner);
        _member = member;
        _scanner = scanner;
        _member.Changed += OnMemberChanged;
        Refresh();
    }

    /// <summary>Raised when the device joined a swarm, so the dialog closes for the app.</summary>
    public event EventHandler? Joined;

    /// <summary>This device's side of a swarm.</summary>
    public SwarmMemberService Member => _member;

    /// <summary>Whether the device is in a swarm now, or rejoining one.</summary>
    [ObservableProperty]
    public partial bool IsInSwarm { get; private set; }

    /// <summary>What the device does with a swarm, such as "In Yahav's tablet's swarm".</summary>
    [ObservableProperty]
    public partial string StatusText { get; private set; } = string.Empty;

    /// <summary>Why the device could not join, or left.</summary>
    [ObservableProperty]
    public partial string? Error { get; private set; }

    /// <summary>The swarms this device paired with.</summary>
    public ObservableCollection<PairedDeviceViewModel> PairedSwarms { get; } = [];

    /// <summary>Whether the device paired with a swarm before.</summary>
    [ObservableProperty]
    public partial bool HasPairedSwarms { get; private set; }

    /// <summary>The tablet's address, as its Swarm window shows it.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(JoinCommand))]
    public partial string AddressText { get; set; } = string.Empty;

    /// <summary>The pairing code the tablet's Swarm window shows.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(JoinCommand))]
    public partial string CodeText { get; set; } = string.Empty;

    /// <summary>Whether the device is joining.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(JoinCommand), nameof(ScanCommand))]
    public partial bool IsBusy { get; private set; }

    /// <summary>Whether this device has a camera to scan the tablet's QR code with.</summary>
    public bool CanScan => _scanner.IsAvailable;

    /// <summary>Joins the swarm a scanned or pasted invitation names, as the Link dialog's own scan does for a swarm's code.</summary>
    /// <param name="invitation">The swarm's invitation.</param>
    public Task<bool> JoinInvitationAsync(LinkInvitation invitation)
    {
        ArgumentNullException.ThrowIfNull(invitation);
        return JoinWithAsync(LinkTarget.FromInvitation(invitation));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _member.Changed -= OnMemberChanged;
        _joining?.Cancel();
        _joining?.Dispose();
    }

    /// <summary>Scans the tablet's QR code with the camera, then joins its swarm.</summary>
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

        if (!LinkInvitation.TryParse(scanned, out var invitation) || invitation.Role != LinkRole.Swarm)
        {
            Error = "That is not a swarm's code. On the tablet, open the Swarm window and tap Add a device, then scan the code it shows.";
            return;
        }

        await JoinWithAsync(LinkTarget.FromInvitation(invitation));
    }

    /// <summary>Joins the swarm at the typed address with the typed code.</summary>
    [RelayCommand(CanExecute = nameof(CanJoin))]
    private async Task JoinAsync()
    {
        Error = null;
        if (!LinkEndpoint.TryParse(AddressText, out var endpoint))
        {
            Error = "Type the address the tablet's Swarm window shows, such as 192.168.1.23.";
            return;
        }

        if (!LinkPairingCode.TryParse(CodeText, out var code))
        {
            Error = "Type the eight-character code the tablet shows, such as 7K3Q-M9XA.";
            return;
        }

        if (await JoinWithAsync(LinkTarget.FromCode(endpoint, code)))
        {
            CodeText = string.Empty;
        }
    }

    /// <summary>Leaves the swarm: the device is the IDE again.</summary>
    [RelayCommand]
    private Task LeaveAsync() => _member.LeaveAsync();

    private bool CanStart() => !IsBusy;

    private bool CanJoin() => !IsBusy && !string.IsNullOrWhiteSpace(AddressText) && !string.IsNullOrWhiteSpace(CodeText);

    private async Task<bool> JoinWithAsync(LinkTarget target)
    {
        IsBusy = true;
        Error = null;
        _joining?.Dispose();
        _joining = new CancellationTokenSource();
        try
        {
            var joined = await _member.JoinAsync(target, _joining.Token);
            Error = joined ? null : _member.Member.Error;
            if (joined)
            {
                Joined?.Invoke(this, EventArgs.Empty);
            }

            return joined;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RejoinAsync(LinkPairedDevice coordinator)
    {
        IsBusy = true;
        Error = null;
        try
        {
            if (await _member.Member.RejoinAsync(coordinator))
            {
                Joined?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                Error = _member.Member.Error;
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void OnMemberChanged(object? sender, EventArgs e) => Refresh();

    private void Refresh()
    {
        var member = _member.Member;
        IsInSwarm = _member.IsInSwarm;
        StatusText = member.Membership switch
        {
            SwarmMembership.Joined => $"In {member.CoordinatorName}'s swarm",
            SwarmMembership.Joining => "Joining the swarm…",
            SwarmMembership.Reconnecting => $"Reconnecting to {member.CoordinatorName}…",
            _ => "Not in a swarm",
        };
        if (member.Membership == SwarmMembership.Off && member.Error is { } error)
        {
            Error = error;
        }

        var current = member.Coordinator?.Fingerprint;
        PairedSwarms.Clear();
        foreach (var swarm in _member.Coordinators.Devices.OrderByDescending(d => d.LastSeen))
        {
            var isCurrent = IsInSwarm && current == swarm.Fingerprint;
            PairedSwarms.Add(new PairedDeviceViewModel(
                swarm,
                isCurrent,
                new AsyncRelayCommand(() => RejoinAsync(swarm), () => !IsBusy && !IsInSwarm),
                new RelayCommand(() => _ = _member.Member.ForgetAsync(swarm))));
        }

        HasPairedSwarms = PairedSwarms.Count > 0;
    }
}
