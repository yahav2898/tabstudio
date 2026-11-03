using TabStudio.Core.Workspaces;
using TabStudio.Debugging.Building;
using TabStudio.Debugging.Inspection;
using TabStudio.Debugging.Recording;

namespace TabStudio.Debugging;

public sealed partial class DebugSession
{
    private readonly Lock _historyGate = new();
    private long _selectedSequence;
    private int _selectedHistoryFrame;
    private DebugPausedEventArgs? _historicalPause;

    /// <summary>Creates an explicitly recording session. Ordinary sessions allocate no ring buffer.</summary>
    /// <param name="build">A successful instrumented build.</param>
    /// <param name="recording">The binary ring capacity.</param>
    public DebugSession(DebugBuild build, RecordingOptions recording) : this(build, DefaultEvaluationTimeout, recording) { }

    /// <summary>The recording, retained after exit and disposal, or null when recording was not requested.</summary>
    public DebugRecording? Recording => _engine.Recording;

    /// <summary>What <see cref="RecordingLimitation"/> says for a program with Visual Basic code.</summary>
    public const string VisualBasicRecordingLimitation =
        "Recording is available for C# programs. In Visual Basic code it records each step and the values of the locals, " +
        "but not each write to a variable, so Jump to Last Write and the cause of an exception are not found there.";

    /// <summary>
    /// What recording cannot do for this program, for the IDE to show, or <see langword="null"/> when it records
    /// everything. Recording a program with Visual Basic code works, with <see cref="VisualBasicRecordingLimitation"/>.
    /// </summary>
    public string? RecordingLimitation =>
        Language == ProjectLanguage.VisualBasic || DebugInfo.Statements.Any(s => SourceLanguage.IsVisualBasic(s.Location.FilePath))
            ? VisualBasicRecordingLimitation
            : null;

    /// <summary>The selected historical snapshot. It does not replace or resume the engine's live pause.</summary>
    public DebugPausedEventArgs? HistoricalPause { get { lock (_historyGate) return _historicalPause; } }

    /// <summary>
    /// Raised on the thread that changed the selected historical step. The selection belongs to the IDE's UI thread:
    /// the <c>Find…</c> methods search the recording without changing it and may run on any thread; the methods that
    /// select a step are called by the IDE on its UI thread with what they found.
    /// </summary>
    public event EventHandler? HistorySelectionChanged;

    /// <summary>Selected timeline index; the latest step when viewing the live pause.</summary>
    public int HistoryIndex
    {
        get
        {
            var (sequence, _) = Selection;
            return sequence == 0 ? (Recording?.Count ?? 0) - 1 : Recording?.IndexOf(sequence) ?? -1;
        }
    }

    private (long Sequence, int Frame) Selection { get { lock (_historyGate) return (_selectedSequence, _selectedHistoryFrame); } }

    /// <summary>Selects any retained step while paused or after exit. Execution never runs backwards.</summary>
    /// <param name="index">Zero-based index in the retained timeline.</param>
    public DebugPausedEventArgs? SeekHistory(int index)
    {
        if (State == DebugSessionState.Running || Recording?.GetStep(index) is not { } step) return null;
        var pause = step.ToPause();
        lock (_historyGate)
        {
            _selectedSequence = step.Sequence;
            _selectedHistoryFrame = 0;
            _historicalPause = pause;
        }
        HistorySelectionChanged?.Invoke(this, EventArgs.Empty);
        return pause;
    }

    /// <summary>Steps to the preceding recorded event, including called functions.</summary>
    public DebugPausedEventArgs? StepBackInto() => SeekHistory(HistoryIndex - 1);

    /// <summary>Steps backwards over called functions, following the current logical invocation.</summary>
    public DebugPausedEventArgs? StepBack() => SeekHistory(FindStepBack());

    /// <summary>Finds where <see cref="StepBack"/> goes, without selecting it; -1 when there is no such step.</summary>
    public int FindStepBack()
    {
        var index = HistoryIndex;
        var frame = Selection.Frame;
        if (Recording?.GetStep(index) is not { Frames.Count: > 0 } current) return -1;
        frame = Math.Min(frame, current.Frames.Count - 1);
        var invocation = current.Frames[frame].Id;
        var ancestors = current.Frames.Skip(frame).Select(f => f.Id).ToHashSet();
        for (var i = index - 1; i >= 0; i--)
        {
            if (Recording.GetStep(i) is not { Frames.Count: > 0 } previous) continue;
            // An await continuation can move to another managed thread; invocation identity remains stable.
            if (previous.Frames[0].Id == invocation || ancestors.Contains(previous.Frames[0].Id)) return i;
        }
        return -1;
    }

    /// <summary>Finds the previous actual breakpoint hit, or an exception's derivable faulty write.</summary>
    public DebugPausedEventArgs? ReverseContinue() => SeekHistory(FindReverseContinue());

    /// <summary>Finds where <see cref="ReverseContinue"/> goes, without selecting it; -1 when there is no such step.</summary>
    public int FindReverseContinue()
    {
        for (var i = HistoryIndex - 1; i >= 0; i--)
            if (Recording?.GetStep(i) is { } step && (step.IsBreakpoint || step.Sequence == Recording.ExceptionCause)) return i;
        return -1;
    }

    /// <summary>Jumps to the last recorded write in the selected invocation.</summary>
    /// <param name="path">A variable or shallow field path, as shown by Locals or Watch.</param>
    public DebugPausedEventArgs? JumpToLastWrite(string path) => SeekHistory(FindLastWrite(path));

    /// <summary>Finds where <see cref="JumpToLastWrite"/> goes, without selecting it; -1 when there is no such write.</summary>
    /// <param name="path">A variable or shallow field path, as shown by Locals or Watch.</param>
    public int FindLastWrite(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var index = HistoryIndex;
        var historical = HistoricalPause is not null;
        var frame = Selection.Frame;
        if (Recording?.GetStep(index) is not { Frames.Count: > 0 } step) return -1;
        return Recording.FindLastWrite(path, step.Frames[Math.Min(frame, step.Frames.Count - 1)].Id, historical ? index : index + 1);
    }

    /// <summary>Reads the selected historical frame's captured locals; callers are immutable snapshots too.</summary>
    /// <param name="frameIndex">Zero for the innermost frame.</param>
    public DebugPausedEventArgs? GetHistoricalFrame(int frameIndex)
    {
        if (Recording?.GetStep(HistoryIndex) is not { } step || (uint)frameIndex >= (uint)step.Frames.Count) return null;
        lock (_historyGate) _selectedHistoryFrame = frameIndex;
        return step.ToPause(frameIndex);
    }

    /// <summary>Returns the debugger view to its live pause, without continuing the program.</summary>
    public void ReturnToLive()
    {
        lock (_historyGate)
        {
            _selectedSequence = 0;
            _selectedHistoryFrame = 0;
            _historicalPause = null;
        }
        HistorySelectionChanged?.Invoke(this, EventArgs.Empty);
    }
}
