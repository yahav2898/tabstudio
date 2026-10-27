using TabStudio.Link.Messaging;
using TabStudio.Live.Files;
using TabStudio.Live.Protocol;

namespace TabStudio.Live.Sessions;

/// <summary>What the host tells a guest: edits, presence, who is in the session, what it runs, and the end.</summary>
public sealed partial class LiveGuest
{
    /// <summary>Handles one notification from the host. Runs on the session's thread.</summary>
    private void OnNotification(GuestLink from, string method, LinkPayload payload)
    {
        // Notifications of a connection that was replaced are out of date.
        if (!ReferenceEquals(from, _handler) || HasEnded)
        {
            return;
        }

        switch (method)
        {
            case LiveMethods.Edit:
                OnEdits(payload.Read(LiveJsonContext.Default.LiveEditNotice));
                break;
            case LiveMethods.Presence:
                SetPresence(payload.Read(LiveJsonContext.Default.LivePresence));
                break;
            case LiveMethods.Participants:
                var participants = payload.Read(LiveJsonContext.Default.LiveParticipantsNotice);
                SetParticipants(participants.Participants, participants.Classroom, participants.Presenter);
                break;
            case LiveMethods.Rejected:
                _ = ReloadAsync(payload.Read(LiveJsonContext.Default.LivePathRequest).Path);
                break;
            case LiveMethods.FilesChanged:
                OnFilesChanged(payload.Read(LiveJsonContext.Default.LiveFilesNotice), saved: false);
                break;
            case LiveMethods.Saved:
                OnFilesChanged(payload.Read(LiveJsonContext.Default.LiveFilesNotice), saved: true);
                break;
            case LiveMethods.Output:
                RaiseOutput(payload.Read(LiveJsonContext.Default.LiveOutput));
                break;
            case LiveMethods.OutputCleared:
                RaiseOutputCleared();
                break;
            case LiveMethods.Terminal when payload.Attachments.Count == 1:
                RaiseTerminal(payload.Attachments[0]);
                break;
            case LiveMethods.Errors:
                SetErrors(payload.Read(LiveJsonContext.Default.LiveErrorsNotice).Errors);
                break;
            case LiveMethods.Run:
                SetRunState(payload.Read(LiveJsonContext.Default.LiveRunState));
                break;
            case LiveMethods.Debug:
                SetDebug(payload.Read(LiveJsonContext.Default.LiveDebugNotice).State);
                break;
            case LiveMethods.Breakpoints:
                SetBreakpoints(payload.Read(LiveJsonContext.Default.LiveBreakpointsNotice).Breakpoints);
                break;
            case LiveMethods.Removed or LiveMethods.Ended:
                EndSession(payload.Read(LiveJsonContext.Default.LiveEndNotice).Message);
                break;
        }
    }

    private void OnEdits(LiveEditNotice notice)
    {
        if (FindDocument(notice.Path) is { } document)
        {
            ApplyEdits(document, notice.Operations);
        }
        else if (_early.TryGetValue(notice.Path, out var waiting))
        {
            // The document is being opened: its edits wait for the answer, and those it already has are ignored then.
            waiting.AddRange(notice.Operations);
        }
    }

    private void OnFilesChanged(LiveFilesNotice notice, bool saved)
    {
        var known = Files.ToDictionary(f => f.Path, StringComparer.Ordinal);
        foreach (var entry in notice.Changed)
        {
            known[entry.Path] = entry;
        }

        foreach (var path in notice.Removed)
        {
            known.Remove(path);
        }

        Files = [.. known.Values.OrderBy(f => f.Path, StringComparer.Ordinal)];
        if (_link is { } link && _mirror is { } mirror)
        {
            _ = RefreshFilesAsync(link.Connection, mirror, notice.Changed, notice.Removed);
        }

        if (saved)
        {
            foreach (var entry in notice.Changed)
            {
                RaiseSaved(entry.Path);
            }
        }
    }
}
