using CommunityToolkit.Mvvm.Input;
using TabStudio.Live;

namespace TabStudio.App.ViewModels.Live;

/// <summary>
/// One participant in the Live Share window: its avatar, name and role, whether it raised a hand or is reconnecting,
/// Follow, and for the host what the participant may do.
/// </summary>
public sealed class LiveParticipantRowViewModel
{
    /// <summary>Creates the row.</summary>
    /// <param name="participant">The participant.</param>
    /// <param name="panel">The window, which carries out the row's commands.</param>
    /// <param name="isSelf">Whether it is this device.</param>
    /// <param name="isFollowed">Whether this device follows it.</param>
    /// <param name="manages">Whether this device is the host, which decides what guests may do.</param>
    /// <param name="isClassroom">Whether the session is in classroom mode.</param>
    public LiveParticipantRowViewModel(LiveParticipant participant, LivePanelViewModel panel, bool isSelf, bool isFollowed, bool manages, bool isClassroom)
    {
        ArgumentNullException.ThrowIfNull(participant);
        ArgumentNullException.ThrowIfNull(panel);
        Participant = participant;
        Avatar = LiveAvatarViewModel.Of(participant);
        IsSelf = isSelf;
        IsFollowed = isFollowed;
        CanManage = manages && participant.Role != LiveRole.Host;
        CanHandControl = CanManage && isClassroom && !participant.IsPresenter && participant.IsConnected;
        var id = participant.Id;
        FollowCommand = new RelayCommand(() => panel.ToggleFollow(id), () => !isSelf && participant.IsConnected);
        MakeEditorCommand = new RelayCommand(() => panel.SetRole(id, LiveRole.Editor), () => CanManage && participant.Role == LiveRole.Viewer);
        MakeViewerCommand = new RelayCommand(() => panel.SetRole(id, LiveRole.Viewer), () => CanManage && participant.Role == LiveRole.Editor);
        ToggleSteppingCommand = new RelayCommand(() => panel.SetCanStep(id, !participant.CanStep), () => CanManage);
        ToggleTerminalCommand = new RelayCommand(() => panel.SetCanUseTerminal(id, !participant.CanUseTerminal), () => CanManage);
        HandControlCommand = new RelayCommand(() => panel.HandControl(id), () => CanHandControl);
        LowerHandCommand = new RelayCommand(() => panel.LowerHand(id), () => CanManage && participant.HandRaised);
        RemoveCommand = new AsyncRelayCommand(() => panel.RemoveAsync(id), () => CanManage);
    }

    /// <summary>The participant.</summary>
    public LiveParticipant Participant { get; }

    /// <summary>Its avatar.</summary>
    public LiveAvatarViewModel Avatar { get; }

    /// <summary>Its name, with "(you)" for this device.</summary>
    public string Name => IsSelf ? $"{Participant.Name} (you)" : Participant.Name;

    /// <summary>What it may do, for its pill: Host, Presenter, Editor or Viewer.</summary>
    public string RoleText => Participant.Role == LiveRole.Host && !Participant.IsPresenter ? "Host"
        : Participant.IsPresenter ? "Presenter"
        : Participant.Role == LiveRole.Editor ? "Editor"
        : "Viewer";

    /// <summary>What it does now, such as <c>Hand raised</c> or <c>Reconnecting…</c>; empty when there is nothing to say.</summary>
    public string Detail
    {
        get
        {
            var parts = new List<string>();
            if (!Participant.IsConnected)
            {
                parts.Add("Reconnecting…");
            }

            if (Participant.HandRaised)
            {
                parts.Add("Hand raised");
            }

            if (Participant.Role != LiveRole.Host && Participant.CanStep)
            {
                parts.Add("Steps the debugger");
            }

            if (Participant.Role != LiveRole.Host && Participant.CanUseTerminal)
            {
                parts.Add("Types in the Terminal");
            }

            if (IsFollowed)
            {
                parts.Add("You follow");
            }

            return string.Join(" · ", parts);
        }
    }

    /// <summary>Whether <see cref="Detail"/> has something to say.</summary>
    public bool HasDetail => Detail.Length > 0;

    /// <summary>Whether it raised its hand.</summary>
    public bool HandRaised => Participant.HandRaised;

    /// <summary>Whether it is this device.</summary>
    public bool IsSelf { get; }

    /// <summary>Whether this device follows it.</summary>
    public bool IsFollowed { get; }

    /// <summary>The Follow button's label.</summary>
    public string FollowText => IsFollowed ? "Following" : "Follow";

    /// <summary>Whether the Follow button shows: for everyone but this device.</summary>
    public bool CanFollow => !IsSelf;

    /// <summary>Whether this device decides what the participant may do: the host, for a guest.</summary>
    public bool CanManage { get; }

    /// <summary>Whether the host can hand control to it: classroom mode, and it does not present already.</summary>
    public bool CanHandControl { get; }

    /// <summary>The Allow Stepping item's label.</summary>
    public string SteppingText => Participant.CanStep ? "Stop letting them step" : "Let them step the debugger";

    /// <summary>The Allow Terminal item's label.</summary>
    public string TerminalText => Participant.CanUseTerminal ? "Stop letting them type in the Terminal" : "Let them type in the Terminal";

    /// <summary>Follows the participant, or stops following it.</summary>
    public IRelayCommand FollowCommand { get; }

    /// <summary>Lets the guest edit.</summary>
    public IRelayCommand MakeEditorCommand { get; }

    /// <summary>Lets the guest only watch.</summary>
    public IRelayCommand MakeViewerCommand { get; }

    /// <summary>Lets the guest continue and step the debugged program, or stops letting it.</summary>
    public IRelayCommand ToggleSteppingCommand { get; }

    /// <summary>Lets the guest type in the Terminal, or stops letting it.</summary>
    public IRelayCommand ToggleTerminalCommand { get; }

    /// <summary>Hands control to the guest, in classroom mode.</summary>
    public IRelayCommand HandControlCommand { get; }

    /// <summary>Lowers the guest's hand.</summary>
    public IRelayCommand LowerHandCommand { get; }

    /// <summary>Takes the guest out of the session.</summary>
    public IAsyncRelayCommand RemoveCommand { get; }
}
