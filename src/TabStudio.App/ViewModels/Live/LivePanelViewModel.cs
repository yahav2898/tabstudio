using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.Link.Networking;
using TabStudio.Link.Qr;
using TabStudio.Live;
using TabStudio.Live.Sessions;

namespace TabStudio.App.ViewModels.Live;

/// <summary>
/// The Live Share window in the activity bar: who is in the session and what each may do, Follow, raised hands and
/// classroom mode, and the Share Session dialog with the session's QR code and code. With no session it offers to
/// share the workspace or to join a session.
/// </summary>
public sealed partial class LivePanelViewModel : ObservableObject
{
    private readonly Func<Task> _share;
    private readonly Action _join;
    private readonly Action<int?> _follow;
    private readonly Func<Task> _end;
    private LiveSession? _session;
    private string? _qrText;
    private int? _followed;

    /// <summary>Creates the window.</summary>
    /// <param name="share">Shares the workspace.</param>
    /// <param name="join">Opens the Join dialog.</param>
    /// <param name="follow">Follows a participant, or stops following with <see langword="null"/>.</param>
    /// <param name="end">Ends the session for everyone, or leaves it.</param>
    public LivePanelViewModel(Func<Task> share, Action join, Action<int?> follow, Func<Task> end)
    {
        ArgumentNullException.ThrowIfNull(share);
        ArgumentNullException.ThrowIfNull(join);
        ArgumentNullException.ThrowIfNull(follow);
        ArgumentNullException.ThrowIfNull(end);
        _share = share;
        _join = join;
        _follow = follow;
        _end = end;
    }

    /// <summary>Everyone in the session, the host first.</summary>
    public ObservableCollection<LiveParticipantRowViewModel> Participants { get; } = [];

    /// <summary>Whether this device is in a session.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOff))]
    public partial bool IsInSession { get; private set; }

    /// <summary>Whether this device is in no session: the window offers to share or join.</summary>
    public bool IsOff => !IsInSession;

    /// <summary>Whether this device hosts the session.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGuest))]
    public partial bool IsHost { get; private set; }

    /// <summary>Whether this device joined the session as a guest.</summary>
    public bool IsGuest => IsInSession && !IsHost;

    /// <summary>The window's subtitle, such as <c>Hello · 3 participants</c>.</summary>
    [ObservableProperty]
    public partial string Subtitle { get; private set; } = "Code together on the local network";

    /// <summary>The status bar's item: <c>Live · 3</c>, <c>Following Dana</c> or <c>Reconnecting…</c>; <see langword="null"/> with no session.</summary>
    [ObservableProperty]
    public partial string? StatusText { get; private set; }

    /// <summary>How many other participants are connected, for the activity bar's badge.</summary>
    [ObservableProperty]
    public partial int OthersCount { get; private set; }

    /// <summary>Whether guests that join can edit; the host chooses. Off, they only watch.</summary>
    [ObservableProperty]
    public partial bool GuestsCanEdit { get; set; } = true;

    /// <summary>Whether the session is in classroom mode: one presenter, everyone else follows.</summary>
    [ObservableProperty]
    public partial bool IsClassroom { get; set; }

    /// <summary>Whether the session takes no new guests.</summary>
    [ObservableProperty]
    public partial bool IsLocked { get; set; }

    /// <summary>Whether this guest's hand is raised.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HandText))]
    public partial bool HandRaised { get; set; }

    /// <summary>The Raise hand button's label.</summary>
    public string HandText => HandRaised ? "Lower hand" : "Raise hand";

    /// <summary>Whether this guest may only watch.</summary>
    [ObservableProperty]
    public partial bool IsViewer { get; private set; }

    /// <summary>Whether this guest follows the presenter, in classroom mode.</summary>
    [ObservableProperty]
    public partial bool FollowsPresenter { get; private set; }

    /// <summary>Whether this guest is in a class and does not present.</summary>
    [ObservableProperty]
    public partial bool IsInClass { get; private set; }

    /// <summary>Whether the Share Session dialog is shown.</summary>
    [ObservableProperty]
    public partial bool IsShareOpen { get; private set; }

    /// <summary>The QR code guests scan.</summary>
    [ObservableProperty]
    public partial QrCode? Code { get; private set; }

    /// <summary>The address guests type, such as <c>192.168.1.23:47814</c>.</summary>
    [ObservableProperty]
    public partial string? AddressText { get; private set; }

    /// <summary>The code guests type, such as <c>7K3Q-M9XA</c>.</summary>
    [ObservableProperty]
    public partial string? CodeText { get; private set; }

    /// <summary>Whether the host has an address on a network guests can reach.</summary>
    [ObservableProperty]
    public partial bool HasNetwork { get; private set; } = true;

    /// <summary>Why the session could not start, or why it ended.</summary>
    [ObservableProperty]
    public partial string? Error { get; set; }

    /// <summary>The participant this device follows.</summary>
    public int? Followed => _followed;

    /// <summary>The session shown, if any.</summary>
    public LiveSession? Session => _session;

    /// <summary>Shows a session, or none. Call on the UI thread.</summary>
    /// <param name="session">The session, or <see langword="null"/> when it ended.</param>
    public void Attach(LiveSession? session)
    {
        if (ReferenceEquals(session, _session))
        {
            return;
        }

        if (_session is { } old)
        {
            old.ParticipantsChanged -= OnSessionChanged;
            if (old is LiveHost oldHost)
            {
                oldHost.PairingCodeChanged -= OnCodeChanged;
            }

            if (old is LiveGuest oldGuest)
            {
                oldGuest.StateChanged -= OnSessionChanged;
            }
        }

        _session = session;
        _followed = null;
        if (session is not null)
        {
            session.ParticipantsChanged += OnSessionChanged;
            if (session is LiveHost host)
            {
                host.PairingCodeChanged += OnCodeChanged;
                GuestsCanEdit = host.DefaultRole == LiveRole.Editor;
                IsLocked = host.IsLocked;
            }

            if (session is LiveGuest guest)
            {
                guest.StateChanged += OnSessionChanged;
            }
        }
        else
        {
            IsShareOpen = false;
        }

        Refresh();
    }

    /// <summary>Notes who this device follows, as the workspace decided it.</summary>
    /// <param name="participant">The participant followed, or <see langword="null"/>.</param>
    public void ShowFollowed(int? participant)
    {
        if (_followed != participant)
        {
            _followed = participant;
            Refresh();
        }
    }

    /// <summary>Opens the Share Session dialog with the session's QR code.</summary>
    public void OpenShare()
    {
        if (_session is LiveHost host)
        {
            host.RefreshAddresses();
            RefreshCode();
            IsShareOpen = true;
        }
    }

    /// <summary>Reads everything again from the session. Call on the UI thread.</summary>
    public void Refresh()
    {
        var session = _session is { HasEnded: false } live ? live : null;
        IsInSession = session is not null;
        IsHost = session is LiveHost;
        IsClassroom = session?.IsClassroom ?? false;
        var self = session?.Self;
        IsViewer = self is { Role: LiveRole.Viewer };
        IsInClass = session is { IsClassroom: true } && self is { IsPresenter: false };
        FollowsPresenter = IsInClass && session is not null && _followed == session.PresenterId;
        HandRaised = self?.HandRaised ?? false;

        Participants.Clear();
        if (session is null)
        {
            Subtitle = "Code together on the local network";
            StatusText = null;
            OthersCount = 0;
            return;
        }

        foreach (var participant in session.Participants)
        {
            Participants.Add(new LiveParticipantRowViewModel(participant, this, participant.Id == session.SelfId, participant.Id == _followed, IsHost, session.IsClassroom));
        }

        OthersCount = session.Participants.Count(p => p.Id != session.SelfId && p.IsConnected);
        var people = session.Participants.Count == 1 ? "1 participant" : $"{session.Participants.Count.ToString(CultureInfo.CurrentCulture)} participants";
        Subtitle = session.IsClassroom ? $"{session.WorkspaceName} · class of {people}" : $"{session.WorkspaceName} · {people}";
        StatusText = session switch
        {
            LiveGuest { State: LiveGuestState.Reconnecting } guest => $"Reconnecting to {guest.HostName}…",
            _ when _followed is { } id && session.Find(id) is { } followed => $"Following {followed.Name}",
            LiveHost => OthersCount == 1 ? "Live · 1 guest" : $"Live · {OthersCount.ToString(CultureInfo.CurrentCulture)} guests",
            _ => $"Live with {session.HostName}",
        };
        if (IsHost)
        {
            RefreshCode();
        }
    }

    /// <summary>Follows a participant, or stops when it is the one followed.</summary>
    /// <param name="participant">The participant.</param>
    public void ToggleFollow(int participant) => _follow(_followed == participant ? null : participant);

    /// <summary>Lets a guest edit or only watch.</summary>
    /// <param name="participant">The guest.</param>
    /// <param name="role">Editor or viewer.</param>
    public void SetRole(int participant, LiveRole role) => (_session as LiveHost)?.SetRole(participant, role);

    /// <summary>Lets a guest step the debugger, or stops letting it.</summary>
    /// <param name="participant">The guest.</param>
    /// <param name="canStep">Whether it may.</param>
    public void SetCanStep(int participant, bool canStep) => (_session as LiveHost)?.SetCanStep(participant, canStep);

    /// <summary>Lets a guest type in the Terminal, or stops letting it.</summary>
    /// <param name="participant">The guest.</param>
    /// <param name="canUseTerminal">Whether it may.</param>
    public void SetCanUseTerminal(int participant, bool canUseTerminal) => (_session as LiveHost)?.SetCanUseTerminal(participant, canUseTerminal);

    /// <summary>Hands control to a guest in classroom mode.</summary>
    /// <param name="participant">The guest.</param>
    public void HandControl(int participant) => (_session as LiveHost)?.HandControl(participant);

    /// <summary>Lowers a guest's hand.</summary>
    /// <param name="participant">The guest.</param>
    public void LowerHand(int participant) => (_session as LiveHost)?.LowerHand(participant);

    /// <summary>Takes a guest out of the session.</summary>
    /// <param name="participant">The guest.</param>
    public Task RemoveAsync(int participant) => _session is LiveHost host ? host.RemoveAsync(participant) : Task.CompletedTask;

    partial void OnGuestsCanEditChanged(bool value)
    {
        if (_session is LiveHost host)
        {
            host.DefaultRole = value ? LiveRole.Editor : LiveRole.Viewer;
        }
    }

    partial void OnIsClassroomChanged(bool value)
    {
        if (_session is LiveHost { IsClassroom: var current } host && current != value)
        {
            host.SetClassroom(value);
        }
    }

    partial void OnIsLockedChanged(bool value)
    {
        if (_session is LiveHost host)
        {
            host.IsLocked = value;
        }
    }

    partial void OnHandRaisedChanged(bool value)
    {
        if (_session is LiveGuest guest && guest.Self is { } self && self.HandRaised != value)
        {
            guest.RaiseHand(value);
        }
    }

    /// <summary>Shares the workspace, or shows the session's code when it is shared already.</summary>
    [RelayCommand]
    private async Task ShareAsync()
    {
        if (_session is LiveHost)
        {
            OpenShare();
            return;
        }

        await _share();
    }

    /// <summary>Opens the Join dialog.</summary>
    [RelayCommand]
    private void Join() => _join();

    /// <summary>Ends the session for everyone, as the host, or leaves it, as a guest.</summary>
    [RelayCommand]
    private Task EndAsync() => _end();

    /// <summary>Follows the presenter again, in classroom mode.</summary>
    [RelayCommand]
    private void FollowPresenter()
    {
        if (_session is { IsClassroom: true } session)
        {
            _follow(session.PresenterId);
        }
    }

    /// <summary>Takes control back from the guest that presents.</summary>
    [RelayCommand]
    private void TakeControl() => (_session as LiveHost)?.TakeControl();

    /// <summary>Raises this guest's hand, or lowers it.</summary>
    [RelayCommand]
    private void ToggleHand() => HandRaised = !HandRaised;

    /// <summary>Closes the Share Session dialog.</summary>
    [RelayCommand]
    private void CloseShare() => IsShareOpen = false;

    /// <summary>Draws a new code; the shown one stops working for devices that have not joined.</summary>
    [RelayCommand]
    private void NewCode()
    {
        (_session as LiveHost)?.RenewPairingCode();
        RefreshCode();
    }

    private void OnSessionChanged(object? sender, EventArgs e) => Refresh();

    private void OnCodeChanged(object? sender, EventArgs e) => RefreshCode();

    private void RefreshCode()
    {
        if (_session is not LiveHost host)
        {
            return;
        }

        var addresses = host.Addresses;
        HasNetwork = addresses.Count > 0;
        var uri = host.Invitation?.ToUri();
        if (!string.Equals(uri, _qrText, StringComparison.Ordinal))
        {
            _qrText = uri;
            Code = uri is null ? null : QrCode.Encode(uri);
        }

        AddressText = addresses.Count > 0 ? new LinkEndpoint(addresses[0], host.Port).ToString() : null;
        CodeText = host.PairingCode?.DisplayText;
    }
}
