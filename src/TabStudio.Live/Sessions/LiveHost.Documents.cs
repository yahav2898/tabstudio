using TabStudio.Link;
using TabStudio.Link.Messaging;
using TabStudio.Live.Files;
using TabStudio.Live.Protocol;
using TabStudio.Live.Text;

namespace TabStudio.Live.Sessions;

/// <summary>Shared documents on the host: opening them for guests, merging everyone's edits, and where everyone is.</summary>
public sealed partial class LiveHost
{
    private readonly Dictionary<int, LivePresence> _latestPresence = [];
    private readonly HashSet<int> _presenceToRelay = [];
    private bool _relayScheduled;

    /// <inheritdoc />
    internal override void SendEdits(LiveDocument document, IReadOnlyList<TextOperation> operations) => Forward(document, operations, except: null);

    /// <inheritdoc />
    private protected override void SendPresence(LivePresence presence)
    {
        _latestPresence[HostId] = presence;
        Relay(HostId);
    }

    /// <summary>A document by path, shared from the editor's text or the file on first use.</summary>
    private LiveDocument? OpenShared(string path)
    {
        if (LiveWorkspacePaths.Normalize(path) is not { } normalized)
        {
            return null;
        }

        if (FindDocument(normalized) is { } open)
        {
            return open;
        }

        if (!_shared.ContainsKey(normalized) || LiveWorkspacePaths.ToFullPath(WorkspaceRoot!, normalized) is not { } full)
        {
            return null;
        }

        var text = _workspace.GetOpenText(full) ?? ReadText(full);
        return text is null ? null : CreateDocument(normalized, full, text);
    }

    private LiveDocument CreateDocument(string path, string fullPath, string text)
    {
        var shared = new SharedText(HostId);
        shared.Insert(0, text);
        return AddDocument(path, fullPath, shared);
    }

    private static string? ReadText(string fullPath)
    {
        try
        {
            var bytes = File.ReadAllBytes(fullPath);

            // A file with a NUL byte is not text, and is not opened as a shared document.
            if (bytes.AsSpan().Contains((byte)0))
            {
                return null;
            }

            using var reader = new StreamReader(new MemoryStream(bytes), detectEncodingFromByteOrderMarks: true);
            return reader.ReadToEnd();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>A guest opens a document: it gets every edit of it so far, and every edit from now on.</summary>
    private LinkReply Open(Guest guest, LivePathRequest request)
    {
        if (!LivePermissions.CanRead(guest.Participant, request.Path, IsShared) || OpenShared(request.Path) is not { } document)
        {
            throw new LinkRemoteException(LinkErrorCodes.Failed, $"{HostName} does not share {request.Path}.");
        }

        guest.Documents.Add(document.Path);
        var response = new LiveDocumentResponse(document.Path, document.Text.Operations, document.Text.Version.ToDictionary());
        return LiveJson.Reply(response, LiveJsonContext.Default.LiveDocumentResponse);
    }

    /// <summary>A guest's edits: applied when it may edit, passed on to everyone else who has the document open.</summary>
    private void OnEdits(Guest guest, LinkConnection connection, LiveEditNotice notice)
    {
        if (!ReferenceEquals(guest.Connection, connection) || FindDocument(notice.Path) is not { } document || !guest.Documents.Contains(document.Path))
        {
            return;
        }

        // A viewer's edits, or edits in another participant's name, are refused: the guest loads the document again.
        if (!LivePermissions.CanEdit(guest.Participant) || notice.Operations.Any(o => o.Replica != guest.Id || !o.IsValid()))
        {
            Reject(guest, document.Path);
            return;
        }

        var applied = new List<TextOperation>(notice.Operations.Count);
        foreach (var operation in notice.Operations)
        {
            try
            {
                document.ApplyRemote([operation]);
            }
            catch (InvalidDataException)
            {
                Reject(guest, document.Path);
                break;
            }

            applied.Add(operation);
        }

        if (applied.Count > 0)
        {
            Forward(document, applied, guest);
        }
    }

    private void Reject(Guest guest, string path) =>
        guest.Sender?.Notify(LiveMethods.Rejected, LiveJson.Body(new LivePathRequest(path), LiveJsonContext.Default.LivePathRequest));

    /// <summary>Passes edits on to every guest that has the document open, but the one that made them.</summary>
    private void Forward(LiveDocument document, IReadOnlyList<TextOperation> operations, Guest? except)
    {
        if (operations.Count == 0)
        {
            return;
        }

        var body = LiveJson.Body(new LiveEditNotice(document.Path, operations), LiveJsonContext.Default.LiveEditNotice);
        foreach (var guest in _guests.Values)
        {
            if (!ReferenceEquals(guest, except) && guest.Sender is { } sender && guest.Documents.Contains(document.Path))
            {
                sender.Notify(LiveMethods.Edit, body);
            }
        }
    }

    /// <summary>Where a guest is: kept, shown here, and passed on a moment later.</summary>
    private void OnPresence(Guest guest, LinkConnection connection, LivePresence presence)
    {
        if (!ReferenceEquals(guest.Connection, connection) || presence.Participant != guest.Id
            || (presence.Path is not null && LiveWorkspacePaths.Normalize(presence.Path) is null))
        {
            return;
        }

        _latestPresence[guest.Id] = presence;
        SetPresence(presence);
        Relay(guest.Id);
    }

    /// <summary>
    /// Whether a participant's presence goes to the other guests: always, except in classroom mode, where only the
    /// presenter's and the host's do, so thirty followers do not send each other their carets.
    /// </summary>
    private bool ShouldRelay(int participant) => !IsClassroom || participant == HostId || participant == PresenterId;

    private void Relay(int participant)
    {
        _presenceToRelay.Add(participant);
        if (!_relayScheduled)
        {
            _relayScheduled = true;
            _ = RelayLaterAsync();
        }
    }

    private async Task RelayLaterAsync()
    {
        await Task.Delay(PresenceInterval).ConfigureAwait(false);
        Dispatcher.Post(() =>
        {
            _relayScheduled = false;
            foreach (var participant in _presenceToRelay)
            {
                if (!ShouldRelay(participant) || !_latestPresence.TryGetValue(participant, out var presence))
                {
                    continue;
                }

                var body = LiveJson.Body(presence, LiveJsonContext.Default.LivePresence);
                foreach (var guest in _guests.Values)
                {
                    if (guest.Id != participant && guest.Sender is { } sender)
                    {
                        sender.Notify(LiveMethods.Presence, body);
                    }
                }
            }

            _presenceToRelay.Clear();
        });
    }
}
