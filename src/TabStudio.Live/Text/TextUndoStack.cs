namespace TabStudio.Live.Text;

/// <summary>
/// One participant's undo and redo of a <see cref="SharedText"/>: Undo takes back what this participant typed, never
/// what someone else did meanwhile, by making new operations that every device applies like any other edit.
/// </summary>
/// <remarks>
/// <para>
/// An undo of typed text deletes those characters, the ones still there; an undo of a deletion types the deleted text
/// again where it was, after whatever is before it now. Typing one character after another, or deleting one after
/// another, within a moment is one step, as in an editor; edits between <see cref="BeginGroup"/> and
/// <see cref="EndGroup"/> are one step too.
/// </para>
/// <para>Not thread-safe: use it on the text's thread.</para>
/// </remarks>
public sealed class TextUndoStack
{
    /// <summary>How long a pause in typing may be for the typing to stay one step.</summary>
    public static readonly TimeSpan TypingPause = TimeSpan.FromSeconds(1.5);

    /// <summary>How many steps are kept.</summary>
    public const int MaxSteps = 500;

    private readonly SharedText _text;
    private readonly TimeProvider _time;
    private readonly List<List<UndoEntry>> _undo = [];
    private readonly List<List<UndoEntry>> _redo = [];
    private List<UndoEntry>? _group;
    private int _groupDepth;
    private TypingRun? _typing;

    /// <summary>Creates an empty stack for the participant that edits a text on this device.</summary>
    /// <param name="text">The text.</param>
    /// <param name="time">The clock that decides when typing is one step; the system's when <see langword="null"/>.</param>
    public TextUndoStack(SharedText text, TimeProvider? time = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        _text = text;
        _time = time ?? TimeProvider.System;
    }

    /// <summary>Raised when <see cref="CanUndo"/> or <see cref="CanRedo"/> may have changed.</summary>
    public event EventHandler? Changed;

    /// <summary>Whether there is something of this participant's to undo.</summary>
    public bool CanUndo => _undo.Count > 0;

    /// <summary>Whether there is an undone step to do again.</summary>
    public bool CanRedo => _redo.Count > 0;

    /// <summary>Starts a step that every edit until <see cref="EndGroup"/> belongs to. Groups nest.</summary>
    public void BeginGroup()
    {
        if (_groupDepth++ == 0)
        {
            _group = null;
            _typing = null;
        }
    }

    /// <summary>Ends the step <see cref="BeginGroup"/> started.</summary>
    public void EndGroup()
    {
        if (_groupDepth == 0)
        {
            return;
        }

        if (--_groupDepth == 0)
        {
            _group = null;
            _typing = null;
        }
    }

    /// <summary>Records an edit this participant made, so it can be undone. Clears what could be redone.</summary>
    /// <param name="operation">The operation the edit made.</param>
    /// <param name="removedText">For a deletion, the text it removed, as the editor showed it.</param>
    /// <param name="offset">Where the edit was, in visible characters.</param>
    public void Record(TextOperation operation, string? removedText, int offset)
    {
        ArgumentNullException.ThrowIfNull(operation);
        UndoEntry entry;
        int length;
        if (operation.Text is { } inserted)
        {
            entry = new UndoEntry([new CharRange(operation.Replica, operation.Clock, inserted.Length)], null, null);
            length = inserted.Length;
        }
        else
        {
            var deleted = operation.Deleted!;
            entry = new UndoEntry([], removedText ?? string.Empty, deleted[0].First);
            length = removedText?.Length ?? 0;
        }

        _redo.Clear();
        var now = _time.GetTimestamp();
        var target = _groupDepth > 0 ? _group : ContinuesTyping(operation.IsInsert, offset, length, operation.Text, now) ? _undo[^1] : null;
        if (target is null)
        {
            target = [];
            _undo.Add(target);
            if (_undo.Count > MaxSteps)
            {
                _undo.RemoveAt(0);
            }

            if (_groupDepth > 0)
            {
                _group = target;
            }
        }

        if (target.Count > 0 && Merge(target[^1], entry, offset, length) is { } merged)
        {
            target[^1] = merged;
        }
        else
        {
            target.Add(entry);
        }

        _typing = _groupDepth > 0 ? null : new TypingRun(operation.IsInsert, operation.IsInsert ? offset + length : offset, now);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Takes back this participant's last step.</summary>
    /// <returns>The operations for the other devices and the changes for the editor, or <see langword="null"/> when there is nothing to undo.</returns>
    public TextEditResult? Undo() => Revert(_undo, _redo);

    /// <summary>Does the last undone step again.</summary>
    /// <returns>The operations for the other devices and the changes for the editor, or <see langword="null"/> when there is nothing to redo.</returns>
    public TextEditResult? Redo() => Revert(_redo, _undo);

    /// <summary>Forgets every step, as when the text is loaded again.</summary>
    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
        _group = null;
        _typing = null;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// One entry for two edits that follow each other: characters typed right after the last ones, or a deletion just
    /// before or at the last one. Otherwise <see langword="null"/>, and the step keeps both.
    /// </summary>
    private UndoEntry? Merge(UndoEntry last, UndoEntry next, int offset, int length)
    {
        if (_typing is not { } typing)
        {
            return null;
        }

        if (last.Remove.Count == 1 && next.Remove.Count == 1 && typing.IsInsert && offset == typing.Offset)
        {
            var before = last.Remove[0];
            var added = next.Remove[0];
            return before.Replica == added.Replica && before.LastClock + 1 == added.Clock
                ? last with { Remove = [before with { Length = before.Length + added.Length }] }
                : null;
        }

        if (last.Restore is { } restored && next.Restore is { } removed && !typing.IsInsert)
        {
            // Backspace: the new text goes first, and is where it starts now; Delete: it goes after.
            return offset + length == typing.Offset ? new UndoEntry([], removed + restored, next.At)
                : offset == typing.Offset ? new UndoEntry([], restored + removed, last.At)
                : null;
        }

        return null;
    }

    private bool ContinuesTyping(bool isInsert, int offset, int length, string? text, long now)
    {
        if (_typing is not { } typing || _undo.Count == 0 || typing.IsInsert != isInsert || _time.GetElapsedTime(typing.At, now) > TypingPause)
        {
            return false;
        }

        if (isInsert)
        {
            // Typing goes on at the end of what was typed; a new line starts a new step.
            return offset == typing.Offset && text is not null && !text.Contains('\n', StringComparison.Ordinal);
        }

        // Backspace deletes just before the last deletion; Delete deletes where it was.
        return offset + length == typing.Offset || offset == typing.Offset;
    }

    private TextEditResult? Revert(List<List<UndoEntry>> from, List<List<UndoEntry>> to)
    {
        if (from.Count == 0)
        {
            return null;
        }

        var step = from[^1];
        from.RemoveAt(from.Count - 1);
        _typing = null;
        var operations = new List<TextOperation>();
        var changes = new List<TextChange>();
        var inverse = new List<UndoEntry>();
        for (var i = step.Count - 1; i >= 0; i--)
        {
            if (Revert(step[i], operations, changes) is { } undone)
            {
                inverse.Add(undone);
            }
        }

        if (inverse.Count > 0)
        {
            to.Add(inverse);
        }

        Changed?.Invoke(this, EventArgs.Empty);
        return new TextEditResult(operations, changes);
    }

    /// <summary>Reverts one entry and returns the entry that reverts the revert.</summary>
    private UndoEntry? Revert(UndoEntry entry, List<TextOperation> operations, List<TextChange> changes)
    {
        UndoEntry? inverse = null;
        if (entry.Remove.Count > 0)
        {
            var visible = _text.VisibleParts(entry.Remove);
            if (visible.Count > 0)
            {
                var removed = _text.TextOf(visible);
                if (_text.Delete(visible, changes) is { } deletion)
                {
                    operations.Add(deletion);
                    inverse = new UndoEntry([], removed, visible[0].First);
                }
            }
        }

        if (entry.Restore is { Length: > 0 } restore && entry.At is { } at)
        {
            var offset = _text.OffsetOf(at) ?? _text.Length;
            if (_text.Insert(offset, restore, changes) is { } insertion)
            {
                operations.Add(insertion);
                inverse = new UndoEntry([new CharRange(insertion.Replica, insertion.Clock, restore.Length)], null, null);
            }
        }

        return inverse;
    }

    /// <summary>One edit of a step: characters to delete, or text to type again where a character was.</summary>
    /// <param name="Remove">The characters an undo deletes.</param>
    /// <param name="Restore">The text an undo types again.</param>
    /// <param name="At">The first character it was deleted from, which says where to type it again.</param>
    private sealed record UndoEntry(IReadOnlyList<CharRange> Remove, string? Restore, CharId? At);

    /// <summary>Where the last typed or deleted character was, and when.</summary>
    private readonly record struct TypingRun(bool IsInsert, int Offset, long At);
}
