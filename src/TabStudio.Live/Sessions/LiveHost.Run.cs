using System.Text;
using TabStudio.Live.Protocol;
using TabStudio.Swarm;

namespace TabStudio.Live.Sessions;

/// <summary>
/// What the host runs, as every guest sees it: the Output, the Terminal, the Error List, the run's state, the pause
/// and the breakpoints. The host's IDE reports them here; output is sent in batches, so a program that prints in a loop
/// does not send a message per character.
/// </summary>
public sealed partial class LiveHost
{
    /// <summary>How long output gathers before it is sent.</summary>
    private static readonly TimeSpan OutputInterval = TimeSpan.FromMilliseconds(40);

    /// <summary>How long the Error List settles before it is sent, since a build changes it many times.</summary>
    private static readonly TimeSpan ErrorsInterval = TimeSpan.FromMilliseconds(250);

    private readonly List<LiveOutput> _outputTail = [];
    private readonly List<LiveOutput> _outputPending = [];
    private readonly List<byte> _terminalTail = [];
    private readonly List<byte> _terminalPending = [];
    private int _outputTailLength;
    private bool _outputScheduled;
    private bool _errorsScheduled;

    /// <summary>Says what the host runs now. Call on the session's thread.</summary>
    /// <param name="state">What its program does, and its status bar's message.</param>
    public void PublishRun(LiveRunState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state == RunState)
        {
            return;
        }

        SetRunState(state);
        Broadcast(LiveMethods.Run, LiveJson.Body(state, LiveJsonContext.Default.LiveRunState));
    }

    /// <summary>Says where the debugged program is paused, or that it runs again. Call on the session's thread.</summary>
    /// <param name="state">The pause, on the workspace's relative paths, or <see langword="null"/>.</param>
    public void PublishDebug(LiveDebugState? state)
    {
        SetDebug(state);
        Broadcast(LiveMethods.Debug, LiveJson.Body(new LiveDebugNotice(state), LiveJsonContext.Default.LiveDebugNotice));
    }

    /// <summary>Says which breakpoints there are. Call on the session's thread.</summary>
    /// <param name="breakpoints">Every breakpoint in a shared file, on the workspace's relative paths.</param>
    public void PublishBreakpoints(IReadOnlyList<SwarmBreakpoint> breakpoints)
    {
        ArgumentNullException.ThrowIfNull(breakpoints);
        if (breakpoints.SequenceEqual(Breakpoints))
        {
            return;
        }

        SetBreakpoints(breakpoints);
        Broadcast(LiveMethods.Breakpoints, LiveJson.Body(new LiveBreakpointsNotice(breakpoints), LiveJsonContext.Default.LiveBreakpointsNotice));
    }

    /// <summary>Says what the Error List shows. Sent once it settles. Call on the session's thread.</summary>
    /// <param name="errors">Its rows in shared files and of whole projects, on the workspace's relative paths.</param>
    public void PublishErrors(IReadOnlyList<LiveDiagnostic> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (errors.SequenceEqual(Errors))
        {
            return;
        }

        SetErrors(errors);
        if (!_errorsScheduled)
        {
            _errorsScheduled = true;
            _ = SendErrorsLaterAsync();
        }
    }

    /// <summary>Adds to the Output every guest sees. Call on the session's thread.</summary>
    /// <param name="text">The text.</param>
    /// <param name="kind">What it is.</param>
    public void WriteOutput(string text, LiveOutputKind kind)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        Append(_outputPending, text, kind);
        Append(_outputTail, text, kind);
        _outputTailLength += text.Length;
        while (_outputTailLength > LiveProtocol.OutputTailLength && _outputTail.Count > 1)
        {
            _outputTailLength -= _outputTail[0].Text.Length;
            _outputTail.RemoveAt(0);
        }

        ScheduleOutput();
    }

    /// <summary>Clears the Output on every guest. Call on the session's thread.</summary>
    public void ClearOutput()
    {
        _outputPending.Clear();
        _outputTail.Clear();
        _outputTailLength = 0;
        Broadcast(LiveMethods.OutputCleared, null);
    }

    /// <summary>Adds output of the host's Terminal, which every guest's Terminal shows. Call on the session's thread.</summary>
    /// <param name="data">What the shell wrote.</param>
    public void WriteTerminal(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty)
        {
            return;
        }

        foreach (var b in data)
        {
            _terminalPending.Add(b);
            _terminalTail.Add(b);
        }

        if (_terminalTail.Count > LiveProtocol.TerminalTailLength)
        {
            _terminalTail.RemoveRange(0, _terminalTail.Count - LiveProtocol.TerminalTailLength);
        }

        ScheduleOutput();
    }

    /// <summary>What a guest that joins sees of the host's run.</summary>
    private LiveRunSnapshot Snapshot() => new(RunState, [.. _outputTail], Errors, Debug, Breakpoints);

    /// <summary>The last of the Terminal's output, for a guest that joins.</summary>
    private ReadOnlyMemory<byte> TerminalTail() => _terminalTail.ToArray();

    private static void Append(List<LiveOutput> outputs, string text, LiveOutputKind kind)
    {
        if (outputs.Count > 0 && outputs[^1].Kind == kind && outputs[^1].Text.Length < 4096)
        {
            outputs[^1] = outputs[^1] with { Text = outputs[^1].Text + text };
        }
        else
        {
            outputs.Add(new LiveOutput(text, kind));
        }
    }

    private void ScheduleOutput()
    {
        if (!_outputScheduled)
        {
            _outputScheduled = true;
            _ = SendOutputLaterAsync();
        }
    }

    private async Task SendOutputLaterAsync()
    {
        await Task.Delay(OutputInterval).ConfigureAwait(false);
        Dispatcher.Post(() =>
        {
            _outputScheduled = false;
            foreach (var output in Merge(_outputPending))
            {
                Broadcast(LiveMethods.Output, LiveJson.Body(output, LiveJsonContext.Default.LiveOutput));
            }

            _outputPending.Clear();
            if (_terminalPending.Count > 0)
            {
                Broadcast(LiveMethods.Terminal, null, attachment: new ReadOnlyMemory<byte>(_terminalPending.ToArray()));
                _terminalPending.Clear();
            }
        });
    }

    private async Task SendErrorsLaterAsync()
    {
        await Task.Delay(ErrorsInterval).ConfigureAwait(false);
        Dispatcher.Post(() =>
        {
            _errorsScheduled = false;
            Broadcast(LiveMethods.Errors, LiveJson.Body(new LiveErrorsNotice(Errors), LiveJsonContext.Default.LiveErrorsNotice));
        });
    }

    /// <summary>Joins pieces of the same kind, so a batch is as few messages as it can be.</summary>
    private static List<LiveOutput> Merge(List<LiveOutput> outputs)
    {
        var merged = new List<LiveOutput>();
        var text = new StringBuilder();
        LiveOutputKind? kind = null;
        foreach (var output in outputs)
        {
            if (kind is { } current && current != output.Kind)
            {
                merged.Add(new LiveOutput(text.ToString(), current));
                text.Clear();
            }

            kind = output.Kind;
            text.Append(output.Text);
        }

        if (kind is { } last && text.Length > 0)
        {
            merged.Add(new LiveOutput(text.ToString(), last));
        }

        return merged;
    }
}
