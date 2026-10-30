namespace TabStudio.Live.Sessions;

/// <summary>
/// Classroom mode: one presenter, many followers. Followers watch and follow the presenter, raise a hand to ask, and
/// the host can hand control to one of them, who then presents and edits until the host takes it back.
/// </summary>
public sealed partial class LiveHost
{
    /// <summary>Turns classroom mode on or off. Call on the session's thread.</summary>
    /// <param name="isClassroom">Whether the session is a class: on, every guest watches and follows the host.</param>
    public void SetClassroom(bool isClassroom)
    {
        if (IsClassroom == isClassroom)
        {
            return;
        }

        // Guests away longer than the reconnect grace come back with the class's roles too.
        foreach (var guest in _guests.Values.Concat(_away.Values))
        {
            if (isClassroom)
            {
                guest.RoleBeforeClassroom = guest.Participant.Role;
                guest.Participant = guest.Participant with { Role = LiveRole.Viewer, IsPresenter = false, HandRaised = false };
            }
            else
            {
                guest.Participant = guest.Participant with { Role = guest.RoleBeforeClassroom, IsPresenter = false, HandRaised = false };
            }
        }

        IsClassroom = isClassroom;
        PresenterId = HostId;
        PublishParticipants();
    }

    /// <summary>
    /// Hands control to a guest in classroom mode: it presents, so everyone follows it, and it edits; the guest that
    /// presented before goes back to watching. Call on the session's thread.
    /// </summary>
    /// <param name="id">The guest.</param>
    /// <returns>Whether control was handed over: the session is a class and the guest is connected.</returns>
    public bool HandControl(int id)
    {
        if (!IsClassroom || FindGuest(id) is not { Connection: not null } guest)
        {
            return false;
        }

        ReturnPresenterToWatching();
        guest.Participant = guest.Participant with { Role = LiveRole.Editor, IsPresenter = true, HandRaised = false };
        PresenterId = id;
        PublishParticipants();
        return true;
    }

    /// <summary>Takes control back from the guest that presents: the host presents again. Call on the session's thread.</summary>
    public void TakeControl()
    {
        if (!IsClassroom || PresenterId == HostId)
        {
            return;
        }

        ReturnPresenterToWatching();
        PresenterId = HostId;
        PublishParticipants();
    }

    private void ReturnPresenterToWatching()
    {
        if (PresenterId != HostId && FindGuest(PresenterId) is { } presenter)
        {
            presenter.Participant = presenter.Participant with { Role = LiveRole.Viewer, IsPresenter = false };
        }
    }
}
