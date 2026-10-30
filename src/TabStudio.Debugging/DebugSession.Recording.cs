using TabStudio.Core.Workspaces;
using TabStudio.Debugging.Building;
using TabStudio.Debugging.Inspection;
using TabStudio.Debugging.Recording;

namespace TabStudio.Debugging;

public sealed partial class DebugSession
{
    private long _selectedSequence;
    private int _selectedHistoryFrame;

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
    public DebugPausedEventArgs? HistoricalPause { get; private set; }

    /// <summary>Selected timeline index; the latest step when viewing the live pause.</summary>
    public int HistoryIndex => _selectedSequence == 0 ? (Recording?.Count ?? 0) - 1 : Recording?.IndexOf(_selectedSequence) ?? -1;

    /// <summary>Selects any retained step while paused or after exit. Execution never runs backwards.</summary>
    /// <param name="index">Zero-based index in the retained timeline.</param>
    public DebugPausedEventArgs? SeekHistory(int index)
    {
        if (State == DebugSessionState.Running || Recording?.GetStep(index) is not { } step) return null;
        _selectedSequence = step.Sequence;
        _selectedHistoryFrame = 0;
        return HistoricalPause = step.ToPause();
    }

    /// <summary>Steps to the preceding recorded event, including called functions.</summary>
    public DebugPausedEventArgs? StepBackInto() => SeekHistory(HistoryIndex - 1);

    /// <summary>Steps backwards over called functions, following the current logical invocation.</summary>
    public DebugPausedEventArgs? StepBack()
    {
        var index = HistoryIndex;
        if (Recording?.GetStep(index) is not { Frames.Count: > 0 } current) return null;
        var invocation = current.Frames[_selectedHistoryFrame].Id;
        var ancestors = current.Frames.Skip(_selectedHistoryFrame).Select(f => f.Id).ToHashSet();
        for (var i = index - 1; i >= 0; i--)
        {
            if (Recording.GetStep(i) is not { Frames.Count: > 0 } previous) continue;
            // An await continuation can move to another managed thread; invocation identity remains stable.
            if (previous.Frames[0].Id == invocation || ancestors.Contains(previous.Frames[0].Id)) return SeekHistory(i);
        }
        return null;
    }

    /// <summary>Finds the previous actual breakpoint hit, or an exception's derivable faulty write.</summary>
    public DebugPausedEventArgs? ReverseContinue()
    {
        for (var i = HistoryIndex - 1; i >= 0; i--)
            if (Recording?.GetStep(i) is { } step && (step.IsBreakpoint || step.Sequence == Recording.ExceptionCause)) return SeekHistory(i);
        return null;
    }

    /// <summary>Jumps to the last recorded write in the selected invocation.</summary>
    /// <param name="path">A variable or shallow field path, as shown by Locals or Watch.</param>
    public DebugPausedEventArgs? JumpToLastWrite(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var index = HistoryIndex;
        if (Recording?.GetStep(index) is not { Frames.Count: > 0 } step) return null;
        return SeekHistory(Recording.FindLastWrite(path, step.Frames[_selectedHistoryFrame].Id, HistoricalPause is null ? index + 1 : index));
    }

    /// <summary>Reads the selected historical frame's captured locals; callers are immutable snapshots too.</summary>
    /// <param name="frameIndex">Zero for the innermost frame.</param>
    public DebugPausedEventArgs? GetHistoricalFrame(int frameIndex)
    {
        if (Recording?.GetStep(HistoryIndex) is not { } step || (uint)frameIndex >= (uint)step.Frames.Count) return null;
        _selectedHistoryFrame = frameIndex;
        return step.ToPause(frameIndex);
    }

    /// <summary>Returns the debugger view to its live pause, without continuing the program.</summary>
    public void ReturnToLive()
    {
        _selectedSequence = 0;
        _selectedHistoryFrame = 0;
        HistoricalPause = null;
    }
}
