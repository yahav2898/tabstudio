using System.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.App.Services.Live;
using TabStudio.App.ViewModels.Live;
using TabStudio.App.ViewModels.Shell;
using TabStudio.Live;
using TabStudio.Live.Sessions;

namespace TabStudio.App.ViewModels;

/// <summary>
/// TabStudio Live in the IDE: File &gt; Share Session shares the workspace with devices on the same Wi-Fi, which join
/// by a scan; every open document is shared as it is typed, with everyone's carets and selections in their colors,
/// and Follow shows what another participant shows. The host's run and debugging are shared too
/// (<c>WorkspaceViewModel.LiveHost.cs</c> on the host, <c>WorkspaceViewModel.LiveGuest.cs</c> on a guest).
/// </summary>
/// <remarks>Everything here runs on the UI thread, which is also the session's thread.</remarks>
public sealed partial class WorkspaceViewModel
{
    private LiveService? _live;
    private LiveJoinViewModel? _liveJoin;
    private LiveSession? _liveSession;
    private int? _liveFollowed;
    private bool _liveSwitching;
    private bool _liveClassroom;
    private int _livePresenter;
    private LivePresence? _pendingFollow;

    /// <summary>The Live Share window, or <see langword="null"/> where the app has no TabStudio Live, as in some tests.</summary>
    public LivePanelViewModel? LivePanel { get; private set; }

    /// <summary>Whether the app has TabStudio Live, for the menus and the activity bar.</summary>
    public bool IsLiveAvailable => LivePanel is not null;

    /// <summary>Whether the side bar shows the Live Share window.</summary>
    public bool IsLiveSelected => SelectedSideBarPanel == SideBarPanel.Live;

    /// <summary>The session this workspace is in, as host or guest.</summary>
    public LiveSession? LiveSession => _liveSession is { HasEnded: false } session ? session : null;

    /// <summary>Whether this workspace is another device's, joined in a Live session: it edits, the host builds and runs.</summary>
    public bool IsLiveGuest => _liveSession is LiveGuest { HasEnded: false };

    /// <summary>Whether this workspace is shared in a Live session it hosts.</summary>
    public bool IsLiveHost => _liveSession is LiveHost { HasEnded: false };

    /// <summary>The participant this device follows, if it follows one.</summary>
    public int? LiveFollowed => _liveFollowed;

    /// <summary>Starts TabStudio Live. Called once, by the constructor.</summary>
    private void InitializeLive(LiveService? live, LiveJoinViewModel? liveJoin)
    {
        if (live is null)
        {
            return;
        }

        _live = live;
        _liveJoin = liveJoin;
        LivePanel = new LivePanelViewModel(ShareLiveSessionAsync, JoinLiveSession, FollowLive, EndLiveSessionAsync);
        LivePanel.PropertyChanged += OnLivePanelPropertyChanged;
        PropertyChanged += OnPropertyChangedForLive;
    }

    /// <summary>Leaves or ends the session with the workspace. Called by <see cref="Dispose"/>.</summary>
    private void DisposeLive()
    {
        if (_live is null)
        {
            return;
        }

        PropertyChanged -= OnPropertyChangedForLive;
        if (LivePanel is { } panel)
        {
            panel.PropertyChanged -= OnLivePanelPropertyChanged;
        }

        var session = _liveSession;
        DetachLiveSession();
        if (session is { HasEnded: false })
        {
            _ = _live.EndAsync();
        }
    }

    /// <summary>File &gt; Share Session: shares this workspace, then shows the QR code guests scan.</summary>
    [RelayCommand]
    private Task ShareSessionAsync() => ShareLiveSessionAsync();

    /// <summary>File &gt; Join Live Session: scan or type another device's session code.</summary>
    [RelayCommand]
    private void JoinLiveSession()
    {
        if (_liveJoin is null)
        {
            StatusText = "TabStudio Live is not available here";
            return;
        }

        _liveJoin.Open();
    }

    /// <summary>Ends the session for everyone as its host, or leaves it as a guest.</summary>
    [RelayCommand]
    private Task EndLiveSessionAsync() => _live?.EndAsync() ?? Task.CompletedTask;

    /// <summary>Shares this workspace: starts the session, if it is not started, and shows its code.</summary>
    public async Task ShareLiveSessionAsync()
    {
        if (_live is null || LivePanel is not { } panel)
        {
            StatusText = "TabStudio Live is not available here";
            return;
        }

        SelectSideBarPanel(SideBarPanel.Live);
        if (IsLiveHost)
        {
            panel.OpenShare();
            return;
        }

        if (IsLiveGuest)
        {
            panel.Error = $"This workspace is {_liveSession!.HostName}'s. Leave the session before sharing one.";
            return;
        }

        if (Workspace is not { } workspace || IsRemoteWorkspace)
        {
            panel.Error = IsRemoteWorkspace
                ? "A remote folder cannot be shared. Open a folder on this device to share it."
                : "Open a solution or a folder to share it.";
            return;
        }

        panel.Error = null;
        var root = workspace.RootDirectory;
        var entry = workspace.Solution?.FilePath is { } solution ? Path.GetRelativePath(root, solution).Replace('\\', '/') : null;
        try
        {
            var host = await _live.StartHostingAsync(root, workspace.Name, entry, new LiveHostBridge(this));
            host.DefaultRole = panel.GuestsCanEdit ? LiveRole.Editor : LiveRole.Viewer;
            host.SetClassroom(panel.IsClassroom);
            AttachLiveSession(host);
            panel.OpenShare();
            StatusText = "Sharing the workspace: scan the code to join";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or System.Net.Sockets.SocketException)
        {
            panel.Error = $"The session could not start: {ex.Message}";
        }
    }

    /// <summary>Follows a participant: this device shows the document it shows, where it is, until the user types or taps.</summary>
    /// <param name="participant">The participant, or <see langword="null"/> to stop following.</param>
    public void FollowLive(int? participant)
    {
        var session = LiveSession;
        if (session is null || participant == session.SelfId || (participant is { } id && session.Find(id) is null))
        {
            participant = null;
        }

        _liveFollowed = participant;
        LivePanel?.ShowFollowed(participant);
        UpdateLiveActivity();
        if (participant is { } followed && session?.PresenceOf(followed) is { } presence)
        {
            _ = ShowFollowedAsync(presence);
        }
    }

    /// <summary>A session started or this workspace joined one: documents are shared, the panels show it.</summary>
    private void AttachLiveSession(LiveSession session)
    {
        if (ReferenceEquals(session, _liveSession))
        {
            return;
        }

        DetachLiveSession();
        _liveSession = session;
        _liveClassroom = session.IsClassroom;
        _livePresenter = session.PresenterId;
        session.ParticipantsChanged += OnLiveParticipantsChanged;
        session.PresenceChanged += OnLivePresenceChanged;
        session.Ended += OnLiveSessionEnded;
        LivePanel?.Attach(session);
        if (session is LiveHost host)
        {
            AttachLiveHost(host);
        }
        else if (session is LiveGuest guest)
        {
            AttachLiveGuest(guest);
        }

        foreach (var document in Documents.ToList())
        {
            BindLiveDocument(document);
        }

        ReportLivePlace();
        OnLiveParticipantsChanged(session, EventArgs.Empty);
        NotifyLiveState();
    }

    /// <summary>The session ended or the workspace closes: documents are this device's own again.</summary>
    private void DetachLiveSession()
    {
        if (_liveSession is not { } session)
        {
            return;
        }

        session.ParticipantsChanged -= OnLiveParticipantsChanged;
        session.PresenceChanged -= OnLivePresenceChanged;
        session.Ended -= OnLiveSessionEnded;
        if (session is LiveHost host)
        {
            DetachLiveHost(host);
        }
        else if (session is LiveGuest guest)
        {
            DetachLiveGuest(guest);
        }

        foreach (var document in Documents)
        {
            document.Live?.Dispose();
        }

        _liveSession = null;
        _liveFollowed = null;
        LivePanel?.Attach(null);
        NotifyLiveState();
    }

    private void OnLiveSessionEnded(object? sender, string reason)
    {
        if (!ReferenceEquals(sender, _liveSession))
        {
            return;
        }

        var wasGuest = IsGuestSession(sender);
        if (sender is LiveHost host)
        {
            KeepGuestEdits(host);
        }

        DetachLiveSession();
        StatusText = reason;
        if (LivePanel is { } panel)
        {
            panel.Error = wasGuest ? null : reason;
        }
    }

    private static bool IsGuestSession(object? session) => session is LiveGuest;

    /// <summary>Shares a document that opened, or opens it from the host's session on a guest.</summary>
    private void BindLiveDocument(DocumentViewModel document)
    {
        if (LiveSession is not { } session || document.Live is not null || !CanShareLive(document)
            || session.RelativePathOf(document.FilePath) is not { } path)
        {
            return;
        }

        if (session is LiveHost host)
        {
            if (host.Share(document.FilePath, document.Document.Text) is { } shared)
            {
                _ = new LiveDocumentBinder(shared, document);
                OnLiveDocumentBound(document);
            }
        }
        else if (session is LiveGuest guest)
        {
            _ = BindGuestDocumentAsync(guest, document, path);
        }
    }

    private static bool CanShareLive(DocumentViewModel document) =>
        !document.IsComparison && !document.IsCiLog && !document.IsExtensionsPage && !document.IsNotebook
        && !document.IsDeviceMatrix && !document.IsPullRequest && !document.IsReview;

    /// <summary>Stops sharing a document that closes.</summary>
    private void UnbindLiveDocument(DocumentViewModel document)
    {
        if (document.Live is not { } binder)
        {
            return;
        }

        var shared = binder.Live;
        binder.Dispose();
        if (_liveSession is { HasEnded: false } session && !Documents.Any(d => ReferenceEquals(d.Live?.Live, shared)))
        {
            session.CloseDocument(shared);
        }

        ReportLivePlace();
    }

    /// <summary>Says where this participant is: in the active document, at its caret, or nowhere in a shared one.</summary>
    private void ReportLivePlace()
    {
        if (LiveSession is not { } session)
        {
            return;
        }

        if (ActiveDocument?.Live is { } binder)
        {
            binder.ReportActive();
        }
        else
        {
            session.ReportPresence(null, 0, 0, 1);
        }
    }

    private void OnPropertyChangedForLive(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(ActiveDocument):
                if (!_liveSwitching && _liveFollowed is not null)
                {
                    // Another tab chosen by hand stops following, as typing does.
                    FollowLive(null);
                }

                ReportLivePlace();
                break;
            case nameof(Workspace):
                AttachGuestWorkspace();
                break;
        }
    }

    private void OnLivePanelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(LivePanelViewModel.OthersCount) or nameof(LivePanelViewModel.StatusText))
        {
            UpdateLiveActivity();
        }
    }

    /// <summary>Who is in the session changed: what this participant may do, who presents, who is followed.</summary>
    private void OnLiveParticipantsChanged(object? sender, EventArgs e)
    {
        if (LiveSession is not { } session)
        {
            return;
        }

        // In a class, everyone but the presenter follows the presenter, when the class starts and when another presents.
        var presenterChanged = session.IsClassroom && (!_liveClassroom || _livePresenter != session.PresenterId);
        _liveClassroom = session.IsClassroom;
        _livePresenter = session.PresenterId;
        if (presenterChanged && session.PresenterId != session.SelfId)
        {
            FollowLive(session.PresenterId);
        }
        else if (presenterChanged && _liveFollowed is not null)
        {
            FollowLive(null);
        }
        else if (_liveFollowed is { } followed && session.Find(followed) is not { IsConnected: true })
        {
            FollowLive(null);
        }

        OnLiveParticipantsChangedForGuest(session);
        UpdateLiveActivity();
        NotifyLiveState();
    }

    /// <summary>Someone moved: the participant this device follows is shown where it is.</summary>
    private void OnLivePresenceChanged(object? sender, LivePresence presence)
    {
        if (presence.Participant == _liveFollowed)
        {
            _ = ShowFollowedAsync(presence);
        }
    }

    /// <summary>Shows the followed participant's document, selection and first line.</summary>
    private async Task ShowFollowedAsync(LivePresence presence)
    {
        if (LiveSession is not { } session || presence.Path is not { } path || session.FullPathOf(path) is not { } fullPath)
        {
            return;
        }

        var document = Documents.FirstOrDefault(d => string.Equals(d.FilePath, fullPath, StringComparison.Ordinal));
        if (!ReferenceEquals(document, ActiveDocument))
        {
            _liveSwitching = true;
            try
            {
                document = await OpenDocumentAsync(fullPath);
            }
            finally
            {
                _liveSwitching = false;
            }
        }

        if (document is null || _liveFollowed != presence.Participant)
        {
            return;
        }

        if (document.Live is { } binder)
        {
            binder.ShowFollowed(presence);
            return;
        }

        // A guest's document is shared once the host sent it: the place is shown then.
        _pendingFollow = presence;
    }

    /// <summary>A document's binder started: a pending place of the followed participant is shown in it.</summary>
    private void OnLiveDocumentBound(DocumentViewModel document)
    {
        if (document.Live is not { } binder)
        {
            return;
        }

        binder.FollowStopped += (_, _) =>
        {
            if (_liveFollowed is not null)
            {
                FollowLive(null);
            }
        };
        if (_pendingFollow is { } pending && pending.Participant == _liveFollowed && binder.Live.Path == pending.Path)
        {
            _pendingFollow = null;
            binder.ShowFollowed(LiveSession?.PresenceOf(pending.Participant) ?? pending);
        }

        if (ReferenceEquals(document, ActiveDocument))
        {
            binder.ReportActive();
        }
    }

    /// <summary>The Live Share item in the activity bar: participants, and raised hands for the host.</summary>
    private void UpdateLiveActivity()
    {
        if (Activity.Find(nameof(SideBarPanel.Live)) is not { } item)
        {
            return;
        }

        item.IsAvailable = IsLiveAvailable;
        if (LiveSession is not { } session)
        {
            item.ClearBadge();
            return;
        }

        var hands = session.IsHost ? session.Participants.Count(p => p.HandRaised) : 0;
        if (hands > 0)
        {
            item.ShowCount(hands, ActivityTone.Warning, Count(hands, "hand raised", "hands raised"));
            return;
        }

        var others = session.Participants.Count(p => p.Id != session.SelfId && p.IsConnected);
        item.ShowCount(others, ActivityTone.Success, Count(others, "other participant", "other participants"));
    }

    private void NotifyLiveState()
    {
        OnPropertyChanged(nameof(LiveSession));
        OnPropertyChanged(nameof(IsLiveGuest));
        OnPropertyChanged(nameof(IsLiveHost));
        NotifyDebugCommands();
        BuildCommand.NotifyCanExecuteChanged();
        RunCommand.NotifyCanExecuteChanged();
    }
}
