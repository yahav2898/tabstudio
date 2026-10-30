using TabStudio.Link;
using TabStudio.Link.Messaging;
using TabStudio.Live.Files;
using TabStudio.Live.Protocol;
using TabStudio.Live.Text;

namespace TabStudio.Live.Sessions;

/// <summary>Guests' requests: files, saving, the debugger, breakpoints, the Terminal and raised hands, each checked first.</summary>
public sealed partial class LiveHost
{
    /// <summary>The most files one request may ask for.</summary>
    private const int MaxFilesPerRequest = 512;

    /// <summary>The most bytes one answer carries; a guest asks again for the rest.</summary>
    private const long MaxBytesPerAnswer = 32L * 1024 * 1024;

    /// <summary>How long a save waits for the guest's edits that were sent just before it.</summary>
    private static readonly TimeSpan SaveWait = TimeSpan.FromSeconds(5);

    /// <summary>Shared files a guest fetches for its copy of the workspace: only files the host lists, from the shared folder.</summary>
    private async Task<LinkReply> ReadFilesAsync(Guest guest, LiveFilesRequest request)
    {
        if (request.Paths.Count > MaxFilesPerRequest)
        {
            throw new LinkRemoteException(LinkErrorCodes.InvalidRequest, $"Ask for at most {MaxFilesPerRequest} files at once.");
        }

        var allowed = await Dispatcher.InvokeAsync(() =>
            request.Paths.Select(p => LivePermissions.CanRead(guest.Participant, p, _shared.ContainsKey) ? p : null).ToList()).ConfigureAwait(false);

        var entries = new List<LiveFileEntry?>(allowed.Count);
        var contents = new List<ReadOnlyMemory<byte>>(allowed.Count);
        long total = 0;
        foreach (var path in allowed)
        {
            var bytes = path is null || total > MaxBytesPerAnswer ? null : _files.Read(path);
            if (path is null || bytes is null)
            {
                entries.Add(null);
                contents.Add(ReadOnlyMemory<byte>.Empty);
                continue;
            }

            total += bytes.Length;
            entries.Add(new LiveFileEntry(path, bytes.Length, LiveWorkspaceFiles.HashOf(bytes)));
            contents.Add(bytes);
        }

        return LiveJson.Reply(new LiveFilesResponse(entries), LiveJsonContext.Default.LiveFilesResponse, contents);
    }

    /// <summary>A guest saves a document, once the host has every edit the guest had made when it saved.</summary>
    private async Task<LinkReply> SaveAsync(Guest guest, LiveSaveRequest request, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + SaveWait;
        while (true)
        {
            var document = await Dispatcher.InvokeAsync(() =>
            {
                if (!LivePermissions.CanSave(guest.Participant))
                {
                    throw new LinkRemoteException(LinkErrorCodes.Failed, $"{HostName} lets you watch this session, not save.");
                }

                var shared = FindDocument(request.Path) ?? throw new LinkRemoteException(LinkErrorCodes.Failed, $"{request.Path} is not open in the session.");
                var version = shared.Text.Version;
                return request.Version.All(v => version[v.Key] >= v.Value) ? shared : null;
            }).ConfigureAwait(false);

            if (document is not null)
            {
                await Dispatcher.InvokeAsync(() => _workspace.SaveAsync(document)).ConfigureAwait(false);
                await Dispatcher.InvokeAsync(() => NotifySaved(document.FullPath)).ConfigureAwait(false);
                return LinkReply.Empty;
            }

            if (DateTime.UtcNow > deadline)
            {
                throw new LinkRemoteException(LinkErrorCodes.Failed, "Your last edits did not reach the host. Try saving again.");
            }

            await Task.Delay(50, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>A guest continues, steps or pauses the debugged program, when the host allows it to.</summary>
    private async Task<LinkReply> ControlDebuggerAsync(Guest guest, LiveDebugCommandRequest request)
    {
        var done = await Dispatcher.InvokeAsync(() =>
        {
            if (!LivePermissions.CanControlDebugger(guest.Participant))
            {
                throw new LinkRemoteException(LinkErrorCodes.Failed, $"{HostName} decides who steps the program, and has not allowed you to.");
            }

            return _workspace.ControlDebuggerAsync(request.Command, guest.Participant);
        }).ConfigureAwait(false);

        return done ? LinkReply.Empty : throw new LinkRemoteException(LinkErrorCodes.Failed, "The debugger cannot do that now.");
    }

    /// <summary>A guest expands a value of the paused program; everyone may look.</summary>
    private async Task<LinkReply> GetVariablesAsync(Guest guest, LiveVariablesRequest request, CancellationToken cancellationToken)
    {
        _ = guest;
        var variables = await Dispatcher.InvokeAsync(() => _workspace.GetVariablesAsync(request.Pause, request.Path, cancellationToken)).ConfigureAwait(false);
        return LiveJson.Reply(new LiveVariablesResponse(variables), LiveJsonContext.Default.LiveVariablesResponse);
    }

    /// <summary>A guest's Watch or DataTip expression: a variable for everyone, anything for whoever may step.</summary>
    private async Task<LinkReply> EvaluateAsync(Guest guest, LiveEvaluateRequest request, CancellationToken cancellationToken)
    {
        if (request.Expression.Length > 1024)
        {
            throw new LinkRemoteException(LinkErrorCodes.InvalidRequest, "The expression is too long.");
        }

        if (!LivePermissions.CanEvaluate(guest.Participant, request.Expression))
        {
            throw new LinkRemoteException(LinkErrorCodes.Failed, $"{HostName} lets you read variables, such as order.Total or items[0], not run code in the program.");
        }

        var value = await Dispatcher.InvokeAsync(() => _workspace.EvaluateAsync(request.Pause, request.Expression, cancellationToken)).ConfigureAwait(false);
        return LiveJson.Reply(value, LiveJsonContext.Default.LiveEvaluation);
    }

    /// <summary>A guest sets or removes a breakpoint in a shared file, when it may edit.</summary>
    private LinkReply SetBreakpoint(Guest guest, LiveBreakpointRequest request)
    {
        if (!LivePermissions.CanSetBreakpoints(guest.Participant))
        {
            throw new LinkRemoteException(LinkErrorCodes.Failed, $"{HostName} lets you watch this session, not set breakpoints.");
        }

        if (request.Line < 1 || !LivePermissions.CanRead(guest.Participant, request.Path, IsShared)
            || LiveWorkspacePaths.ToFullPath(WorkspaceRoot!, request.Path) is not { } full)
        {
            throw new LinkRemoteException(LinkErrorCodes.InvalidRequest, $"{request.Path} is not a shared file.");
        }

        _workspace.SetBreakpoint(full, request.Line, request.Condition, request.Remove, guest.Participant);
        return LinkReply.Empty;
    }

    /// <summary>A guest raises or lowers its hand.</summary>
    private void OnHand(Guest guest, bool raised)
    {
        if (guest.Connection is not null)
        {
            Update(guest, guest.Participant with { HandRaised = raised });
        }
    }

    /// <summary>A guest types in the Terminal, when the host allows it to.</summary>
    private void OnTerminalInput(Guest guest, LinkConnection connection, byte[] input)
    {
        if (ReferenceEquals(guest.Connection, connection) && LivePermissions.CanUseTerminal(guest.Participant) && input.Length is > 0 and <= 64 * 1024)
        {
            _workspace.WriteTerminal(input, guest.Participant);
        }
    }

    /// <summary>Lowers a guest's hand, as the host does once it answered.</summary>
    /// <param name="id">The guest.</param>
    public void LowerHand(int id)
    {
        if (FindGuest(id) is { } guest)
        {
            Update(guest, guest.Participant with { HandRaised = false });
        }
    }
}
