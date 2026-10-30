using TabStudio.Link;
using TabStudio.Link.Messaging;
using TabStudio.Link.Networking;
using TabStudio.Link.Security;
using TabStudio.Live.Files;
using TabStudio.Live.Protocol;
using TabStudio.Live.Text;

namespace TabStudio.Live.Sessions;

/// <summary>Connecting, copying the shared files, and reconnecting by itself with the edits made meanwhile.</summary>
public sealed partial class LiveGuest
{
    /// <summary>Connects, joins, copies what changed, and makes the connection the current one.</summary>
    private async Task ConnectAsync(LinkTarget target, bool rejoin, CancellationToken cancellationToken)
    {
        var handler = new GuestLink(this);
        var options = new LinkClientOptions
        {
            Identity = _options.Identity,
            DeviceName = LiveNames.Clean(_options.Name),
            DeviceKind = _options.Kind,
            Role = LinkRole.Live,
            PingInterval = _options.PingInterval,
            AppVersion = _options.AppVersion,
        };
        var session = await LinkClient.ConnectAsync(options, target, handler, cancellationToken).ConfigureAwait(false);
        try
        {
            var request = await Dispatcher.InvokeAsync(() => new LiveJoinRequest(
                options.DeviceName,
                options.DeviceKind,
                rejoin ? SelfId : null,
                rejoin ? [.. Documents.Select(d => new LiveDocumentVersion(d.Path, d.Text.Version.ToDictionary()))] : null)).ConfigureAwait(false);
            var answer = await session.Connection.RequestAsync(
                LiveMethods.Join,
                LiveJson.Body(request, LiveJsonContext.Default.LiveJoinRequest),
                cancellationToken: cancellationToken).ConfigureAwait(false);
            var joined = answer.Read(LiveJsonContext.Default.LiveJoinResponse);
            var terminal = answer.Attachments.Count > 0 ? answer.Attachments[0] : Array.Empty<byte>();

            var mirror = _mirror ?? new LiveMirror(MirrorRoot(session.Host.Fingerprint, joined.Session.Workspace));
            await CopyAsync(session.Connection, mirror, joined.Files, removeOthers: true, cancellationToken).ConfigureAwait(false);
            await Dispatcher.InvokeAsync(() => Adopt(session, handler, mirror, joined, terminal, target, rejoin)).ConfigureAwait(false);
        }
        catch
        {
            await session.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>Copies the files the copy lacks, in batches, and drops those the host no longer shares.</summary>
    private async Task CopyAsync(LinkConnection connection, LiveMirror mirror, IReadOnlyList<LiveFileEntry> files, bool removeOthers, CancellationToken cancellationToken)
    {
        await _mirrorGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var missing = mirror.Missing(files);
            var done = 0;
            ReportCopy(done, missing.Count);
            foreach (var batch in missing.Chunk(FilesPerRequest))
            {
                // An answer stops at 32 MB and leaves the rest of the batch empty: those are asked for again, until
                // an answer brings none of them (a file the host cannot read). Skipped, they were missing from the
                // copy until the next reconnection.
                var pending = batch.Select(f => f.Path).ToList();
                while (pending.Count > 0)
                {
                    var answer = await connection.RequestAsync(
                        LiveMethods.Files,
                        LiveJson.Body(new LiveFilesRequest(pending), LiveJsonContext.Default.LiveFilesRequest),
                        cancellationToken: cancellationToken).ConfigureAwait(false);
                    var response = answer.Read(LiveJsonContext.Default.LiveFilesResponse);
                    var left = new List<string>();
                    for (var i = 0; i < pending.Count; i++)
                    {
                        if (i < response.Files.Count && i < answer.Attachments.Count && response.Files[i] is { } entry)
                        {
                            mirror.Write(entry, answer.Attachments[i]);
                        }
                        else
                        {
                            left.Add(pending[i]);
                        }
                    }

                    pending = left.Count < pending.Count ? left : [];
                }

                done += batch.Length;
                ReportCopy(done, missing.Count);
            }

            if (removeOthers)
            {
                mirror.RemoveOthers(files);
            }

            mirror.SaveIndex();
        }
        finally
        {
            _mirrorGate.Release();
        }
    }

    private void ReportCopy(int done, int total) => Dispatcher.Post(() => CopyProgress?.Invoke(this, (done, total)));

    /// <summary>Makes a connection that joined the current one, with everything the host said. Runs on the session's thread.</summary>
    private void Adopt(LinkClientSession session, GuestLink handler, LiveMirror mirror, LiveJoinResponse joined, byte[] terminal, LinkTarget target, bool rejoin)
    {
        if (HasEnded || Volatile.Read(ref _disposed) == 1)
        {
            _ = session.DisposeAsync().AsTask();
            return;
        }

        var sender = new LiveSender(session.Connection);
        _link = session;
        _sender = sender;
        _handler = handler;
        _mirror = mirror;
        var now = DateTimeOffset.UtcNow;
        var addresses = new[] { session.Host.Address }
            .Concat(target.Addresses.Select(a => LinkEndpoint.TryParse(a, out var endpoint) ? endpoint.Host : a))
            .Concat(_host?.Addresses ?? [])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(6)
            .ToList();
        _host = new LinkPairedDevice(joined.Session.Host, session.Host.Kind, session.Host.Fingerprint, _host?.PairedAt ?? now, now, addresses, session.Port);

        SelfId = joined.You;
        WorkspaceName = joined.Session.Workspace;
        HostName = joined.Session.Host;
        WorkspaceRoot = mirror.Root;
        EntryPath = joined.Session.Entry is { } entry && mirror.PathOf(entry) is { } entryPath && File.Exists(entryPath) ? entryPath : mirror.Root;
        Files = joined.Files;
        SetParticipants(joined.Participants, joined.Session.Classroom, joined.Session.Presenter);

        // What the host runs, as a guest that just joined should see it.
        SetRunState(joined.Run.State);
        SetErrors(joined.Run.Errors);
        SetBreakpoints(joined.Run.Breakpoints);
        SetDebug(joined.Run.Debug);
        RaiseOutputCleared();
        foreach (var output in joined.Run.Output)
        {
            RaiseOutput(output);
        }

        if (!rejoin && terminal.Length > 0)
        {
            RaiseTerminal(terminal);
        }

        // After a reconnection: the edits this device missed, then the ones it made while disconnected.
        foreach (var document in joined.Documents)
        {
            if (FindDocument(document.Path) is not { } open)
            {
                continue;
            }

            ApplyEdits(open, document.Operations);
            var mine = open.Text.OperationsSince(new VersionVector(document.Version)).Where(o => o.Replica == SelfId).ToList();
            if (mine.Count > 0)
            {
                sender.Notify(LiveMethods.Edit, LiveJson.Body(new LiveEditNotice(open.Path, mine), LiveJsonContext.Default.LiveEditNotice));
            }
        }

        foreach (var stale in Documents.Where(d => !joined.Documents.Any(j => j.Path == d.Path)).ToList())
        {
            // The host does not have a document this device had open: it is loaded again.
            _ = ReloadAsync(stale.Path);
        }

        session.Connection.Closed += (_, e) => Dispatcher.Post(() => OnConnectionClosed(session, e.Error));
        if (!session.Connection.IsOpen)
        {
            Dispatcher.Post(() => OnConnectionClosed(session, session.Connection.CloseError));
        }

        SetState(LiveGuestState.Joined, null);
        handler.Activate();
    }

    /// <summary>The connection closed: the device reconnects, unless the session ended. Runs on the session's thread.</summary>
    private void OnConnectionClosed(LinkClientSession session, Exception? error)
    {
        if (!ReferenceEquals(_link, session))
        {
            return;
        }

        _link = null;
        _handler = null;
        _ = _sender?.CompleteAsync();
        _sender = null;
        _ = session.DisposeAsync().AsTask();
        if (HasEnded || _host is not { } host || Volatile.Read(ref _disposed) == 1)
        {
            return;
        }

        SetState(LiveGuestState.Reconnecting, error is null ? $"{HostName} closed the connection." : $"Lost {HostName}: {error.Message}");
        _reconnect?.Cancel();
        _reconnect?.Dispose();
        _reconnect = new CancellationTokenSource();
        var token = _reconnect.Token;
        _ = Task.Run(() => ReconnectAsync(host, token));
    }

    private async Task ReconnectAsync(LinkPairedDevice host, CancellationToken cancellationToken)
    {
        var delays = _options.ReconnectDelays;
        for (var attempt = 0; !cancellationToken.IsCancellationRequested; attempt++)
        {
            try
            {
                await Task.Delay(delays[Math.Min(attempt, delays.Count - 1)], cancellationToken).ConfigureAwait(false);
                var discovered = await LinkDiscovery.FindAsync(host.Fingerprint, TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
                await ConnectAsync(LinkTarget.Paired(host, discovered.Select(d => d.Endpoint.ToString())), rejoin: true, cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex) when (IsFinal(ex))
            {
                // The host removed this device, ended the session, or runs another version: trying again cannot help.
                var message = ex.Message;
                Dispatcher.Post(() => EndSession(message));
                return;
            }
            catch (Exception ex) when (IsConnectFailure(ex))
            {
                var message = Describe(ex);
                Dispatcher.Post(() =>
                {
                    if (_state == LiveGuestState.Reconnecting)
                    {
                        SetState(LiveGuestState.Reconnecting, message);
                    }
                });
            }
        }
    }

    private static bool IsFinal(Exception error) => error switch
    {
        LinkAuthenticationException { Refusal: LinkRefusal.NotPaired or LinkRefusal.VersionMismatch or LinkRefusal.WrongHost or LinkRefusal.WrongRole } => true,
        LinkRemoteException { Code: LinkErrorCodes.NotPaired or LinkErrorCodes.Busy } => true,
        _ => false,
    };

    /// <summary>Opens a document from the host's edits. Comes back to the session's thread.</summary>
    private async Task<LiveDocument?> OpenFromHostAsync(LinkClientSession link, LiveMirror mirror, string path, CancellationToken cancellationToken)
    {
        _early[path] = [];
        try
        {
            var answer = await link.Connection.RequestAsync(
                LiveMethods.Open,
                LiveJson.Body(new LivePathRequest(path), LiveJsonContext.Default.LivePathRequest),
                cancellationToken: cancellationToken).ConfigureAwait(false);
            var response = answer.Read(LiveJsonContext.Default.LiveDocumentResponse);
            return await Dispatcher.InvokeAsync(() =>
            {
                _opening.Remove(path);
                var early = _early.Remove(path, out var waiting) ? waiting : [];
                if (FindDocument(path) is { } open)
                {
                    return open;
                }

                if (mirror.PathOf(path) is not { } fullPath)
                {
                    return null;
                }

                var text = new SharedText(SelfId);
                foreach (var operation in response.Operations)
                {
                    text.Apply(operation);
                }

                var document = AddDocument(path, fullPath, text);
                ApplyEdits(document, early);
                return document;
            }).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is LinkRemoteException or LinkConnectionLostException or LinkProtocolException or InvalidDataException or OperationCanceledException)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                _opening.Remove(path);
                _early.Remove(path);
            }).ConfigureAwait(false);
            return null;
        }
    }

    /// <summary>Loads a document again from the host's edits, after the host refused this device's.</summary>
    private async Task ReloadAsync(string path)
    {
        if (_link is not { } link)
        {
            return;
        }

        try
        {
            var answer = await link.Connection.RequestAsync(
                LiveMethods.Reload,
                LiveJson.Body(new LivePathRequest(path), LiveJsonContext.Default.LivePathRequest)).ConfigureAwait(false);
            var response = answer.Read(LiveJsonContext.Default.LiveDocumentResponse);
            await Dispatcher.InvokeAsync(() =>
            {
                if (FindDocument(path) is { } document)
                {
                    document.Reload(response.Operations, SelfId);
                    DocumentReloaded?.Invoke(this, document);
                }
            }).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is LinkRemoteException or LinkConnectionLostException or LinkProtocolException or InvalidDataException)
        {
            // The document stays as it is; the next reconnection loads it again.
        }
    }

    /// <summary>Applies the host's edits to a document; one this device cannot apply loads the document again.</summary>
    private void ApplyEdits(LiveDocument document, IReadOnlyList<TextOperation> operations)
    {
        if (operations.Count == 0)
        {
            return;
        }

        try
        {
            document.ApplyRemote(operations);
        }
        catch (InvalidDataException)
        {
            _ = ReloadAsync(document.Path);
        }
    }

    /// <summary>Files that changed on the host: the copy fetches them, except open documents, whose edits it has.</summary>
    private async Task RefreshFilesAsync(LinkConnection connection, LiveMirror mirror, IReadOnlyList<LiveFileEntry> changed, IReadOnlyList<string> removed)
    {
        try
        {
            await CopyAsync(connection, mirror, changed, removeOthers: false, CancellationToken.None).ConfigureAwait(false);
            await _mirrorGate.WaitAsync().ConfigureAwait(false);
            try
            {
                foreach (var path in removed)
                {
                    mirror.Remove(path);
                }

                mirror.SaveIndex();
            }
            finally
            {
                _mirrorGate.Release();
            }

            IReadOnlyList<string> paths = [.. changed.Select(c => c.Path), .. removed];
            Dispatcher.Post(() => FilesChanged?.Invoke(this, paths));
        }
        catch (Exception ex) when (ex is LinkRemoteException or LinkConnectionLostException or LinkProtocolException or IOException or UnauthorizedAccessException or ObjectDisposedException)
        {
            // The next reconnection compares the whole copy again.
        }
    }

    /// <summary>
    /// The guest's end of one connection: notifications wait until the join's answer is handled, then go to the
    /// session's thread in the order they arrived.
    /// </summary>
    private sealed class GuestLink(LiveGuest guest) : ILinkMessageHandler
    {
        private readonly Lock _gate = new();
        private readonly List<(string Method, LinkPayload Payload)> _early = [];
        private bool _active;

        /// <summary>The join's answer is handled: the notifications that waited go first, in order.</summary>
        public void Activate()
        {
            lock (_gate)
            {
                _active = true;
                foreach (var (method, payload) in _early)
                {
                    guest.Dispatcher.Post(() => guest.OnNotification(this, method, payload));
                }

                _early.Clear();
            }
        }

        public Task<LinkReply> HandleRequestAsync(string method, LinkPayload payload, CancellationToken cancellationToken) =>
            throw new LinkRemoteException(LinkErrorCodes.UnknownMethod, $"A guest answers no '{method}'.");

        public void HandleNotification(string method, LinkPayload payload)
        {
            lock (_gate)
            {
                if (_active)
                {
                    guest.Dispatcher.Post(() => guest.OnNotification(this, method, payload));
                }
                else
                {
                    _early.Add((method, payload));
                }
            }
        }
    }
}
