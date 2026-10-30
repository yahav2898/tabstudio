using TabStudio.Link;
using TabStudio.Link.Messaging;
using TabStudio.Link.Networking;
using TabStudio.Live.Protocol;
using TabStudio.Live.Text;

namespace TabStudio.Live.Sessions;

/// <summary>Guests joining, reconnecting and leaving, and what the host lets each of them do.</summary>
public sealed partial class LiveHost
{
    /// <summary>
    /// Changes what a guest may do: edit, or only watch. In classroom mode the presenter edits and everyone else
    /// watches; this changes what a guest goes back to when the class ends.
    /// </summary>
    /// <param name="id">The guest.</param>
    /// <param name="role">Editor or viewer.</param>
    public void SetRole(int id, LiveRole role)
    {
        if (role == LiveRole.Host || FindGuest(id) is not { } guest)
        {
            return;
        }

        if (IsClassroom && PresenterId != id)
        {
            guest.RoleBeforeClassroom = role;
            return;
        }

        Update(guest, guest.Participant with { Role = role });
    }

    /// <summary>Lets a guest continue and step the debugged program, or stops letting it.</summary>
    /// <param name="id">The guest.</param>
    /// <param name="canStep">Whether it may.</param>
    public void SetCanStep(int id, bool canStep)
    {
        if (FindGuest(id) is { } guest)
        {
            Update(guest, guest.Participant with { CanStep = canStep });
        }
    }

    /// <summary>Lets a guest type in the host's Terminal, or stops letting it.</summary>
    /// <param name="id">The guest.</param>
    /// <param name="canUseTerminal">Whether it may.</param>
    public void SetCanUseTerminal(int id, bool canUseTerminal)
    {
        if (FindGuest(id) is { } guest)
        {
            Update(guest, guest.Participant with { CanUseTerminal = canUseTerminal });
        }
    }

    /// <summary>
    /// Takes a guest out of the session at once: it is told, disconnected, and refused if it tries to come back, even
    /// with the code. Its copy of the workspace is deleted on its device.
    /// </summary>
    /// <param name="id">The guest.</param>
    public async Task RemoveAsync(int id)
    {
        if (FindGuest(id) is not { } guest)
        {
            return;
        }

        _removed.Add(guest.Fingerprint);
        _guests.Remove(guest.Fingerprint);
        _trust.Forget(guest.Fingerprint);
        ClearPresence(id);
        _latestPresence.Remove(id);
        var sender = guest.Sender;
        var connection = guest.Connection;
        guest.Sender = null;
        guest.Connection = null;
        PublishParticipants();

        if (sender is not null)
        {
            sender.Notify(LiveMethods.Removed, LiveJson.Body(new LiveEndNotice($"{HostName} removed you from the session."), LiveJsonContext.Default.LiveEndNotice));
            await Task.WhenAny(sender.FlushAsync(), Task.Delay(TimeSpan.FromSeconds(2))).ConfigureAwait(false);
        }

        connection?.Close();
        if (Volatile.Read(ref _link) is { } link)
        {
            link.DisconnectClient(guest.Fingerprint);
        }
    }

    /// <summary>Closes a guest's connection as a dropped Wi-Fi would, keeping it in the session, for tests.</summary>
    /// <param name="id">The guest.</param>
    internal void DropConnection(int id) => FindGuest(id)?.Connection?.Close();

    /// <summary>Admits a guest that sent its join, or refuses it. Runs on the session's thread.</summary>
    private LinkReply Accept(GuestHandler handler, LiveJoinRequest request)
    {
        var fingerprint = handler.Peer.Fingerprint;
        if (HasEnded)
        {
            throw new LinkRemoteException(LinkErrorCodes.Failed, $"{HostName} ended the session.");
        }

        if (_removed.Contains(fingerprint))
        {
            throw new LinkRemoteException(LinkErrorCodes.NotPaired, $"{HostName} removed this device from the session, so it cannot join it again.");
        }

        if (!_guests.ContainsKey(fingerprint) && _away.Remove(fingerprint, out var returning))
        {
            _guests[fingerprint] = returning;
        }

        var isNew = !_guests.TryGetValue(fingerprint, out var known);
        Guest guest;
        if (known is null)
        {
            if (IsLocked)
            {
                throw new LinkRemoteException(LinkErrorCodes.Busy, $"{HostName} takes no new guests in this session now.");
            }

            var role = IsClassroom ? LiveRole.Viewer : DefaultRole;
            guest = new Guest(fingerprint, new LiveParticipant(_nextId++, LiveNames.Clean(request.Name), KindOf(request.Kind), NextColor(), role))
            {
                RoleBeforeClassroom = DefaultRole,
            };
            _guests[fingerprint] = guest;
        }
        else
        {
            guest = known;
            guest.Participant = guest.Participant with { Name = LiveNames.Clean(request.Name), IsConnected = true };
        }

        // A guest that reconnects replaces its old connection.
        var old = guest.Sender;
        var sender = new LiveSender(handler.Connection);
        guest.Connection = handler.Connection;
        guest.Sender = sender;
        _ = old?.CompleteAsync();
        handler.Connection.Closed += (_, _) => Dispatcher.Post(() => OnGuestClosed(guest, handler.Connection));
        handler.Activate(guest);

        // A guest that reconnects gets the edits it missed of the documents it had open, and sends those it made.
        var documents = new List<LiveDocumentResponse>();
        guest.Documents.Clear();
        if (!isNew && request.Previous == guest.Id && request.Documents is { } open)
        {
            foreach (var version in open)
            {
                if (FindDocument(version.Path) is { } document)
                {
                    guest.Documents.Add(document.Path);
                    documents.Add(new LiveDocumentResponse(document.Path, document.Text.OperationsSince(new VersionVector(version.Version)), document.Text.Version.ToDictionary()));
                }
            }
        }

        PublishParticipants();
        foreach (var presence in _latestPresence.Values)
        {
            if (presence.Participant != guest.Id && ShouldRelay(presence.Participant))
            {
                sender.Notify(LiveMethods.Presence, LiveJson.Body(presence, LiveJsonContext.Default.LivePresence));
            }
        }

        if (isNew)
        {
            GuestJoined?.Invoke(this, guest.Participant);
        }

        var response = new LiveJoinResponse(
            new LiveSessionInfo(WorkspaceName, HostName, _options.EntryPath, IsClassroom, PresenterId),
            guest.Id,
            Participants,
            [.. _shared.Values.OrderBy(f => f.Path, StringComparer.Ordinal)],
            Snapshot(),
            documents);
        return LiveJson.Reply(response, LiveJsonContext.Default.LiveJoinResponse, [TerminalTail()]);
    }

    /// <summary>A guest's connection closed: it stays in the list as reconnecting, for a while.</summary>
    private void OnGuestClosed(Guest guest, LinkConnection connection)
    {
        if (!ReferenceEquals(guest.Connection, connection))
        {
            return;
        }

        _ = guest.Sender?.CompleteAsync();
        guest.Sender = null;
        guest.Connection = null;
        guest.Participant = guest.Participant with { IsConnected = false, HandRaised = false };
        ClearPresence(guest.Id);
        _latestPresence.Remove(guest.Id);
        if (_guests.ContainsKey(guest.Fingerprint))
        {
            PublishParticipants();
            _ = ForgetLaterAsync(guest);
        }
    }

    /// <summary>
    /// Takes a guest that did not reconnect in time off the list. Its device stays known: forgotten, a tablet that
    /// slept for six minutes was refused when it woke, and the guest's IDE closed the copy with its unsent edits.
    /// When it reconnects it comes back as itself.
    /// </summary>
    private async Task ForgetLaterAsync(Guest guest)
    {
        try
        {
            await Task.Delay(_options.ReconnectGrace, _stopping.Token).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException)
        {
            return;
        }

        Dispatcher.Post(() =>
        {
            if (guest.Connection is null && _guests.TryGetValue(guest.Fingerprint, out var current) && ReferenceEquals(current, guest))
            {
                _guests.Remove(guest.Fingerprint);
                _away[guest.Fingerprint] = guest;
                PublishParticipants();
            }
        });
    }

    /// <summary>A guest left on purpose: it is forgotten, and can join again with the code.</summary>
    private void OnGuestLeft(Guest guest)
    {
        if (!_guests.Remove(guest.Fingerprint))
        {
            return;
        }

        _trust.Forget(guest.Fingerprint);
        ClearPresence(guest.Id);
        _latestPresence.Remove(guest.Id);
        _ = guest.Sender?.CompleteAsync();
        guest.Sender = null;
        guest.Connection?.Close();
        guest.Connection = null;
        PublishParticipants();
    }

    private void Update(Guest guest, LiveParticipant participant)
    {
        if (guest.Participant == participant)
        {
            return;
        }

        guest.Participant = participant;
        PublishParticipants();
    }

    private static string KindOf(string? kind) => kind switch
    {
        LinkDeviceKinds.Phone => LinkDeviceKinds.Phone,
        LinkDeviceKinds.Computer => LinkDeviceKinds.Computer,
        _ => LinkDeviceKinds.Tablet,
    };

    /// <summary>
    /// Answers one guest's connection: its join first, then its requests and notifications, each checked against what
    /// the guest may do before the session's thread handles it.
    /// </summary>
    private sealed class GuestHandler(LiveHost host, LinkPeer peer, LinkConnection connection) : ILinkMessageHandler
    {
        private Guest? _guest;

        public LinkPeer Peer { get; } = peer;

        public LinkConnection Connection { get; } = connection;

        /// <summary>The guest joined: its messages are handled from now on.</summary>
        public void Activate(Guest guest) => Volatile.Write(ref _guest, guest);

        public async Task<LinkReply> HandleRequestAsync(string method, LinkPayload payload, CancellationToken cancellationToken)
        {
            if (method == LiveMethods.Join)
            {
                var request = payload.Read(LiveJsonContext.Default.LiveJoinRequest);
                return await host.Dispatcher.InvokeAsync(() => host.Accept(this, request)).ConfigureAwait(false);
            }

            var guest = Volatile.Read(ref _guest) ?? throw new LinkRemoteException(LinkErrorCodes.NotAuthenticated, "Join the session first.");
            return method switch
            {
                LiveMethods.Files => await host.ReadFilesAsync(guest, payload.Read(LiveJsonContext.Default.LiveFilesRequest)).ConfigureAwait(false),
                LiveMethods.Open or LiveMethods.Reload => await host.Dispatcher.InvokeAsync(() => host.Open(guest, payload.Read(LiveJsonContext.Default.LivePathRequest))).ConfigureAwait(false),
                LiveMethods.Save => await host.SaveAsync(guest, payload.Read(LiveJsonContext.Default.LiveSaveRequest), cancellationToken).ConfigureAwait(false),
                LiveMethods.DebugCommand => await host.ControlDebuggerAsync(guest, payload.Read(LiveJsonContext.Default.LiveDebugCommandRequest)).ConfigureAwait(false),
                LiveMethods.Variables => await host.GetVariablesAsync(guest, payload.Read(LiveJsonContext.Default.LiveVariablesRequest), cancellationToken).ConfigureAwait(false),
                LiveMethods.Evaluate => await host.EvaluateAsync(guest, payload.Read(LiveJsonContext.Default.LiveEvaluateRequest), cancellationToken).ConfigureAwait(false),
                LiveMethods.Breakpoint => await host.Dispatcher.InvokeAsync(() => host.SetBreakpoint(guest, payload.Read(LiveJsonContext.Default.LiveBreakpointRequest))).ConfigureAwait(false),
                _ => throw new LinkRemoteException(LinkErrorCodes.UnknownMethod, $"'{method}' is not part of TabStudio Live."),
            };
        }

        public void HandleNotification(string method, LinkPayload payload)
        {
            if (Volatile.Read(ref _guest) is not { } guest)
            {
                return;
            }

            switch (method)
            {
                case LiveMethods.Edit:
                    var edits = payload.Read(LiveJsonContext.Default.LiveEditNotice);
                    host.Dispatcher.Post(() => host.OnEdits(guest, Connection, edits));
                    break;
                case LiveMethods.Presence:
                    var presence = payload.Read(LiveJsonContext.Default.LivePresence);
                    host.Dispatcher.Post(() => host.OnPresence(guest, Connection, presence));
                    break;
                case LiveMethods.Close:
                    var closed = payload.Read(LiveJsonContext.Default.LivePathRequest);
                    host.Dispatcher.Post(() => guest.Documents.Remove(closed.Path));
                    break;
                case LiveMethods.Hand:
                    var hand = payload.Read(LiveJsonContext.Default.LiveHandNotice);
                    host.Dispatcher.Post(() => host.OnHand(guest, hand.Raised));
                    break;
                case LiveMethods.TerminalInput when payload.Attachments.Count == 1:
                    var input = payload.Attachments[0];
                    host.Dispatcher.Post(() => host.OnTerminalInput(guest, Connection, input));
                    break;
                case LiveMethods.Leave:
                    host.Dispatcher.Post(() => host.OnGuestLeft(guest));
                    break;
            }
        }
    }
}
