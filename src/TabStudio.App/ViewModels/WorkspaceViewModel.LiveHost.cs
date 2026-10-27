using System.Collections.Specialized;
using System.ComponentModel;
using System.Text;
using TabStudio.Debugging;
using TabStudio.Debugging.Symbols;
using TabStudio.Link;
using TabStudio.Live;
using TabStudio.Live.Sessions;
using TabStudio.Swarm;

namespace TabStudio.App.ViewModels;

/// <summary>
/// Hosting a TabStudio Live session: what the IDE runs is every guest's too. The Output, the Terminal, the Error List,
/// the run's state, the breakpoints and, while the program is paused, where it is, its call stack and its locals go to
/// every guest; guests may continue and step when the host allows it, and expand values and evaluate watches on the
/// host's paused program. The host's IDE does what guests ask through <see cref="LiveHostBridge"/>.
/// </summary>
public sealed partial class WorkspaceViewModel
{
    private readonly Dictionary<string, (DebugValue? Value, IDebugVariable? Variable)> _liveValues = new(StringComparer.Ordinal);
    private long _livePauseId;
    private bool _liveDebugShown;
    private bool _liveDebugScheduled;
    private bool _liveSavingForGuest;

    /// <summary>The workspace is shared: what it runs goes to the guests from now on.</summary>
    private void AttachLiveHost(LiveHost host)
    {
        Output.Written += OnOutputWrittenForLive;
        Output.Cleared += OnOutputClearedForLive;
        Terminal.ShellOutput += OnTerminalOutputForLive;
        ErrorList.Refreshed += OnErrorListRefreshedForLive;
        Breakpoints.Changed += OnBreakpointsChangedForLive;
        Locals.Tree.Rows.CollectionChanged += OnLocalsChangedForLive;
        PropertyChanged += OnPropertyChangedForLiveHost;
        PublishLiveRun(host);
        PublishLiveBreakpoints(host);
        PublishLiveErrors(host);
        if (IsPaused)
        {
            _livePauseId++;
            ScheduleLiveDebug();
        }
    }

    private void DetachLiveHost(LiveHost host)
    {
        _ = host;
        Output.Written -= OnOutputWrittenForLive;
        Output.Cleared -= OnOutputClearedForLive;
        Terminal.ShellOutput -= OnTerminalOutputForLive;
        ErrorList.Refreshed -= OnErrorListRefreshedForLive;
        Breakpoints.Changed -= OnBreakpointsChangedForLive;
        Locals.Tree.Rows.CollectionChanged -= OnLocalsChangedForLive;
        PropertyChanged -= OnPropertyChangedForLiveHost;
        _liveValues.Clear();
        _liveDebugShown = false;
    }

    /// <summary>The host saved a document: guests' copies of it change, and their editors show it saved.</summary>
    private void OnDocumentSavedForLive(DocumentViewModel document)
    {
        if (LiveSession is LiveHost host && !_liveSavingForGuest)
        {
            host.NotifySaved(document.FilePath);
        }
    }

    /// <summary>
    /// The session ended: documents guests changed that the host never opened are opened with the guests' text, unsaved,
    /// so no edit is lost.
    /// </summary>
    private void KeepGuestEdits(LiveHost host)
    {
        var changed = new List<(string Path, string Text)>();
        foreach (var shared in host.Documents)
        {
            if (Documents.Any(d => string.Equals(d.FilePath, shared.FullPath, StringComparison.Ordinal)))
            {
                continue;
            }

            var text = shared.Text.ToString();
            string? onDisk;
            try
            {
                onDisk = File.Exists(shared.FullPath) ? File.ReadAllText(shared.FullPath) : null;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                onDisk = null;
            }

            if (!string.Equals(text, onDisk, StringComparison.Ordinal))
            {
                changed.Add((shared.FullPath, text));
            }
        }

        if (changed.Count > 0)
        {
            _ = OpenGuestEditsAsync(changed);
        }
    }

    private async Task OpenGuestEditsAsync(List<(string Path, string Text)> changed)
    {
        foreach (var (path, text) in changed)
        {
            if (await OpenDocumentAsync(path) is { } document)
            {
                document.RestoreUnsavedText(text);
            }
        }
    }

    private void OnOutputWrittenForLive(object? sender, OutputWrittenEventArgs e) =>
        (LiveSession as LiveHost)?.WriteOutput(e.Text, (LiveOutputKind)(int)e.Kind);

    private void OnOutputClearedForLive(object? sender, EventArgs e) => (LiveSession as LiveHost)?.ClearOutput();

    private void OnTerminalOutputForLive(object? sender, ReadOnlyMemory<byte> data) => (LiveSession as LiveHost)?.WriteTerminal(data.Span);

    private void OnErrorListRefreshedForLive(object? sender, EventArgs e)
    {
        if (LiveSession is LiveHost host)
        {
            PublishLiveErrors(host);
        }
    }

    private void OnBreakpointsChangedForLive(object? sender, EventArgs e)
    {
        if (LiveSession is LiveHost host)
        {
            PublishLiveBreakpoints(host);
        }
    }

    private void OnLocalsChangedForLive(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (IsPaused)
        {
            ScheduleLiveDebug();
        }
    }

    private void OnPropertyChangedForLiveHost(object? sender, PropertyChangedEventArgs e)
    {
        if (LiveSession is not LiveHost host)
        {
            return;
        }

        switch (e.PropertyName)
        {
            case nameof(State):
                if (State == IdeState.Paused)
                {
                    _livePauseId++;
                    _liveValues.Clear();
                }

                PublishLiveRun(host);
                ScheduleLiveDebug();
                break;
            case nameof(StatusText):
                PublishLiveRun(host);
                break;
        }
    }

    private void PublishLiveRun(LiveHost host)
    {
        var phase = State switch
        {
            IdeState.Busy => LiveRunPhase.Building,
            IdeState.Running => LiveRunPhase.Running,
            IdeState.Debugging => LiveRunPhase.Debugging,
            IdeState.Paused => LiveRunPhase.Paused,
            _ => LiveRunPhase.Idle,
        };
        host.PublishRun(new LiveRunState(phase, StatusText));
    }

    private void PublishLiveBreakpoints(LiveHost host) =>
        host.PublishBreakpoints([.. Breakpoints.All
            .Select(b => host.RelativePathOf(b.FilePath) is { } path ? new SwarmBreakpoint(path, b.Line, b.Condition) : null)
            .OfType<SwarmBreakpoint>()]);

    private void PublishLiveErrors(LiveHost host) =>
        host.PublishErrors([.. ErrorList.OwnDiagnostics.Select(d =>
        {
            var path = d.Location is { } location ? host.RelativePathOf(location.FilePath) : null;
            return path is null && d.Location is not null
                ? null
                : new LiveDiagnostic(d.Id, (int)d.Severity, d.Message, path, d.Location?.Start ?? 0, d.Location?.Length ?? 0, d.Location?.Line ?? 0, d.Location?.Column ?? 0, d.ProjectName);
        }).OfType<LiveDiagnostic>()]);

    /// <summary>Sends the pause once the panels show it, coalescing the changes of one pause into one message.</summary>
    private void ScheduleLiveDebug()
    {
        if (_liveDebugScheduled)
        {
            return;
        }

        _liveDebugScheduled = true;
        _ui.Post(() =>
        {
            _liveDebugScheduled = false;
            if (LiveSession is LiveHost host)
            {
                PublishLiveDebug(host);
            }
        });
    }

    /// <summary>Where the program is paused, its call stack and its locals, as the host's panels show them; or that it runs.</summary>
    private void PublishLiveDebug(LiveHost host)
    {
        if (State != IdeState.Paused)
        {
            if (_liveDebugShown)
            {
                _liveDebugShown = false;
                host.PublishDebug(null);
            }

            return;
        }

        var frames = CallStack.Frames.Select(f => new SwarmFrame(f.MethodName, ToRelative(host, f.Location))).ToList();
        var locals = new List<SwarmVariable>();
        foreach (var row in Locals.Tree.Rows.Where(r => r.Depth == 0))
        {
            if (row.IsError)
            {
                continue;
            }

            _liveValues[row.Path] = (row.Source, row.Variable);
            locals.Add(new SwarmVariable(row.Name, row.Value, row.Type, row.IsExpandable ? 1 : 0, row.Path));
        }

        var exception = _exceptionHelper is { } helper ? $"{helper.ExceptionType}: {helper.Message}" : null;
        _liveDebugShown = true;
        host.PublishDebug(new LiveDebugState(_livePauseId, StatusText, frames, locals, exception));
    }

    private static DebugSourceSpan? ToRelative(LiveHost host, DebugSourceSpan? span) =>
        span is not null && host.RelativePathOf(span.FilePath) is { } path ? span with { FilePath = path } : null;

    /// <summary>The members or items of a value of the paused program, for a guest that expands it.</summary>
    private async Task<IReadOnlyList<SwarmVariable>> GetLiveVariablesAsync(long pauseId, string path, CancellationToken cancellationToken)
    {
        if (pauseId != _livePauseId || !IsPaused)
        {
            throw new LinkRemoteException(LinkErrorCodes.Failed, "The program is not paused there any more.");
        }

        if (!_liveValues.TryGetValue(path, out var value))
        {
            throw new LinkRemoteException(LinkErrorCodes.Failed, $"There is no value {path} in the paused program.");
        }

        // Reading members may run the program's code, such as property getters: off the UI thread.
        var children = await Task.Run(
            () => value.Value is { } debugValue
                ? debugValue.Expand().Select(c => (c.Name, (DebugValue?)c, (IDebugVariable?)null)).ToList()
                : value.Variable?.ReadChildren().Select(c => (c.Name, (DebugValue?)null, (IDebugVariable?)c.Value)).ToList() ?? [],
            cancellationToken);
        if (pauseId != _livePauseId)
        {
            throw new LinkRemoteException(LinkErrorCodes.Failed, "The program is not paused there any more.");
        }

        var result = new List<SwarmVariable>(children.Count);
        foreach (var (name, debugValue, variable) in children)
        {
            var childPath = variable?.EvaluateName ?? VariableViewModel.ChildPath(path, name);
            _liveValues[childPath] = (debugValue, variable);
            result.Add(await Task.Run(() => Describe(name, childPath, debugValue, variable), cancellationToken));
        }

        return result;
    }

    /// <summary>A Watch or DataTip expression of a guest, evaluated as the host's own DataTips are.</summary>
    private async Task<LiveEvaluation> EvaluateLiveAsync(long pauseId, string expression)
    {
        if (pauseId != _livePauseId || !IsPaused)
        {
            return new LiveEvaluation(null, "The program is not paused there any more.");
        }

        var tree = await ((IEditorDebugger)this).EvaluateDataTipAsync(expression);
        if (tree?.Rows.FirstOrDefault() is not { } row || row.IsError)
        {
            return new LiveEvaluation(null, tree?.Rows.FirstOrDefault()?.Value is { Length: > 0 } error ? error : "There is no such value.");
        }

        _liveValues[row.Path] = (row.Source, row.Variable);
        return new LiveEvaluation(new SwarmVariable(row.Name, row.Value, row.Type, row.IsExpandable ? 1 : 0, row.Path), null);
    }

    private static SwarmVariable Describe(string name, string path, DebugValue? value, IDebugVariable? variable)
    {
        try
        {
            return value is not null
                ? new SwarmVariable(name, value.Value, value.Type, value.HasChildren ? 1 : 0, path)
                : new SwarmVariable(name, variable?.Value ?? string.Empty, variable?.Type ?? string.Empty, variable?.HasChildren == true ? 1 : 0, path);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return new SwarmVariable(name, $"Could not read the value: {ex.Message}", string.Empty, 0, path);
        }
    }

    /// <summary>A guest continues, steps or pauses the program, once the session checked that the host allows it.</summary>
    private async Task<bool> ControlDebuggerForLiveAsync(LiveDebugCommand command, LiveParticipant participant)
    {
        switch (command)
        {
            case LiveDebugCommand.Continue when IsPaused:
                await StartDebuggingCommand.ExecuteAsync(null);
                break;
            case LiveDebugCommand.StepInto when IsPaused:
                await StepIntoCommand.ExecuteAsync(null);
                break;
            case LiveDebugCommand.StepOver when IsPaused:
                await StepOverCommand.ExecuteAsync(null);
                break;
            case LiveDebugCommand.StepOut when IsPaused:
                StepOutCommand.Execute(null);
                break;
            case LiveDebugCommand.BreakAll when IsDebuggeeRunning:
                BreakAllCommand.Execute(null);
                break;
            default:
                return false;
        }

        StatusText = $"{participant.Name}: {Describe(command)}";
        return true;
    }

    private static string Describe(LiveDebugCommand command) => command switch
    {
        LiveDebugCommand.Continue => "Continue",
        LiveDebugCommand.StepInto => "Step Into",
        LiveDebugCommand.StepOver => "Step Over",
        LiveDebugCommand.StepOut => "Step Out",
        _ => "Break All",
    };

    /// <summary>A guest sets or removes a breakpoint, once the session checked that it may.</summary>
    private void SetBreakpointForLive(string fullPath, int line, string? condition, bool removes)
    {
        var existing = Breakpoints.Find(fullPath, line);
        if (removes)
        {
            if (existing is not null)
            {
                Breakpoints.Remove(existing);
            }

            return;
        }

        if (existing is null)
        {
            Breakpoints.Add(fullPath, line, string.IsNullOrWhiteSpace(condition) ? null : condition);
        }
        else if (!string.Equals(existing.Condition, condition, StringComparison.Ordinal))
        {
            Breakpoints.SetCondition(existing, string.IsNullOrWhiteSpace(condition) ? null : condition);
        }
    }

    /// <summary>A guest saves: the host saves the document as its own Save does.</summary>
    private async Task SaveForLiveAsync(LiveDocument shared)
    {
        _liveSavingForGuest = true;
        try
        {
            if (Documents.FirstOrDefault(d => string.Equals(d.FilePath, shared.FullPath, StringComparison.Ordinal)) is { } open)
            {
                if (!await SaveDocumentAsync(open))
                {
                    throw new LinkRemoteException(LinkErrorCodes.Failed, StatusText);
                }

                return;
            }

            // A document only guests have open is saved from the session's text.
            try
            {
                await File.WriteAllTextAsync(shared.FullPath, shared.Text.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                GitChanges.ScheduleRefresh();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                throw new LinkRemoteException(LinkErrorCodes.Failed, $"{Path.GetFileName(shared.FullPath)} could not be saved: {ex.Message}");
            }
        }
        finally
        {
            _liveSavingForGuest = false;
        }
    }

    /// <summary>What the host's IDE does for its guests: the session calls it on the UI thread, once it checked the guest may.</summary>
    /// <param name="workspace">The IDE.</param>
    private sealed class LiveHostBridge(WorkspaceViewModel workspace) : ILiveHostWorkspace
    {
        public string? GetOpenText(string fullPath) =>
            workspace.Documents.FirstOrDefault(d => string.Equals(d.FilePath, fullPath, StringComparison.Ordinal))?.Document.Text;

        public Task SaveAsync(LiveDocument document) => workspace.SaveForLiveAsync(document);

        public Task<bool> ControlDebuggerAsync(LiveDebugCommand command, LiveParticipant participant) =>
            workspace.ControlDebuggerForLiveAsync(command, participant);

        public void SetBreakpoint(string fullPath, int line, string? condition, bool removes, LiveParticipant participant) =>
            workspace.SetBreakpointForLive(fullPath, line, condition, removes);

        public Task<IReadOnlyList<SwarmVariable>> GetVariablesAsync(long pauseId, string path, CancellationToken cancellationToken) =>
            workspace.GetLiveVariablesAsync(pauseId, path, cancellationToken);

        public Task<LiveEvaluation> EvaluateAsync(long pauseId, string expression, CancellationToken cancellationToken) =>
            workspace.EvaluateLiveAsync(pauseId, expression);

        public void WriteTerminal(ReadOnlyMemory<byte> input, LiveParticipant participant) => workspace.Terminal.TypeFromGuest(input);
    }
}
