namespace TabStudio.Extensions.Snippets;

/// <summary>A replacement in the document: the text from <paramref name="Start"/> for <paramref name="Length"/> characters becomes <paramref name="Text"/>.</summary>
/// <param name="Start">Where the replaced text starts.</param>
/// <param name="Length">How long it is.</param>
/// <param name="Text">The new text.</param>
public readonly record struct TextReplacement(int Start, int Length, string Text);

/// <summary>A tab stop occurrence in the document while a snippet is being filled in.</summary>
/// <param name="Index">The tab stop's number.</param>
/// <param name="Start">Where it starts in the document.</param>
/// <param name="Length">Its length.</param>
/// <param name="IsPrimary">Whether it is the occurrence that is selected and typed into; the others follow it.</param>
public readonly record struct SnippetRange(int Index, int Start, int Length, bool IsPrimary);

/// <summary>
/// Filling in an inserted snippet, as in VS Code: Tab and Shift+Tab move between tab stops in their order (0 last),
/// typing in a tab stop changes its linked copies too (through their transforms), and the session ends at tab stop 0.
/// The editor reports every change to the document and applies the replacements the session asks for.
/// </summary>
public sealed class SnippetSession
{
    private readonly List<Occurrence> _occurrences;
    private readonly int[] _order;
    private readonly List<(TextReplacement Replacement, Occurrence Target)> _pending = [];
    private int _position = -1;

    /// <summary>Starts a session for a snippet inserted at an offset.</summary>
    /// <param name="expansion">The expanded snippet.</param>
    /// <param name="offset">Where its text was inserted.</param>
    public SnippetSession(SnippetExpansion expansion, int offset)
    {
        ArgumentNullException.ThrowIfNull(expansion);
        _occurrences = [.. expansion.Placeholders.Select(p => new Occurrence(p.Index, offset + p.Start, offset + p.Start + p.Length, p.Choices, p.Transform))];

        // The first occurrence of an index without a transform is the one typed into.
        foreach (var group in _occurrences.GroupBy(o => o.Index))
        {
            (group.FirstOrDefault(o => o.Transform is null) ?? group.First()).IsPrimary = true;
        }

        var indices = _occurrences.Select(o => o.Index).Distinct().Where(i => i > 0).Order().ToList();
        indices.Add(0);
        _order = [.. indices];
    }

    /// <summary>Whether the snippet is still being filled in.</summary>
    public bool IsActive { get; private set; } = true;

    /// <summary>The tab stop the caret is in, or -1 before the first move.</summary>
    public int CurrentIndex => _position >= 0 ? _order[_position] : -1;

    /// <summary>Whether the current tab stop is the last one before tab stop 0.</summary>
    public bool IsAtLastTabStop => _position >= 0 && _position >= _order.Length - 2;

    /// <summary>The choices of the current tab stop, or <see langword="null"/>.</summary>
    public IReadOnlyList<string>? CurrentChoices => Primary(CurrentIndex)?.Choices;

    /// <summary>Every tab stop occurrence that is still in the document, for drawing them.</summary>
    public IReadOnlyList<SnippetRange> Ranges =>
        [.. _occurrences.Where(o => !o.IsRemoved && o.Index != 0).Select(o => new SnippetRange(o.Index, o.Start, o.End - o.Start, o.IsPrimary))];

    /// <summary>Moves to the next tab stop and returns the range to select; at tab stop 0 the session ends.</summary>
    /// <returns>The selection, or <see langword="null"/> when there is nothing left to select and the session ended.</returns>
    public (int Start, int Length)? MoveNext()
    {
        while (IsActive && _position < _order.Length - 1)
        {
            _position++;
            if (Primary(_order[_position]) is { } primary)
            {
                if (_order[_position] == 0)
                {
                    IsActive = false;
                }

                return (primary.Start, primary.End - primary.Start);
            }
        }

        IsActive = false;
        return null;
    }

    /// <summary>Moves to the previous tab stop and returns the range to select.</summary>
    /// <returns>The selection, or <see langword="null"/> when already at the first tab stop.</returns>
    public (int Start, int Length)? MovePrevious()
    {
        while (IsActive && _position > 0)
        {
            _position--;
            if (Primary(_order[_position]) is { } primary)
            {
                return (primary.Start, primary.End - primary.Start);
            }
        }

        return null;
    }

    /// <summary>Ends the session, as Escape does.</summary>
    public void End() => IsActive = false;

    /// <summary>Whether an offset is inside or at the edge of the current tab stop, where typing belongs to it.</summary>
    /// <param name="offset">The offset, such as the caret's.</param>
    public bool IsInCurrentTabStop(int offset) =>
        Primary(CurrentIndex) is { } primary && offset >= primary.Start && offset <= primary.End;

    /// <summary>
    /// Updates the tab stops after the document changed, and returns the replacements that make the current tab stop's
    /// linked copies follow it. The editor applies them, reporting each back with <paramref name="isSessionEdit"/> set.
    /// </summary>
    /// <param name="offset">Where the change starts.</param>
    /// <param name="removedLength">How many characters were removed.</param>
    /// <param name="insertedLength">How many characters were inserted.</param>
    /// <param name="getText">Reads the document's text after the change, from an offset for a length.</param>
    /// <param name="isSessionEdit">Whether the change is one of the replacements this session asked for.</param>
    public IReadOnlyList<TextReplacement> OnTextChanged(int offset, int removedLength, int insertedLength, Func<int, int, string> getText, bool isSessionEdit = false)
    {
        ArgumentNullException.ThrowIfNull(getText);
        if (!IsActive)
        {
            return [];
        }

        var current = Primary(CurrentIndex);
        var inCurrent = !isSessionEdit && current is not null && offset >= current.Start && offset + removedLength <= current.End;
        var delta = insertedLength - removedLength;
        var removedEnd = offset + removedLength;
        var target = isSessionEdit ? TakePendingTarget(offset, removedLength) : null;
        foreach (var occurrence in _occurrences.Where(o => !o.IsRemoved))
        {
            // Typing at the edge of the current tab stop goes into it; replacing a whole occurrence keeps it.
            var grows = !isSessionEdit && occurrence == current;
            if (occurrence == target || (target is null && occurrence.Start == offset && occurrence.End == removedEnd && (removedLength > 0 || grows)))
            {
                occurrence.Start = offset;
                occurrence.End = offset + insertedLength;
                continue;
            }

            var isEmptyAtEdge = occurrence.Start == occurrence.End && (occurrence.Start == offset || occurrence.Start == removedEnd);
            if (occurrence != current && occurrence.Start >= offset && occurrence.End <= removedEnd && removedLength > 0 && !isEmptyAtEdge)
            {
                // Text around a nested tab stop was replaced: the nested one is gone.
                occurrence.IsRemoved = true;
                continue;
            }

            occurrence.Start = Map(occurrence.Start, offset, removedEnd, delta, afterInsertion: !grows);
            occurrence.End = Math.Max(occurrence.Start, Map(occurrence.End, offset, removedEnd, delta, afterInsertion: grows));
        }

        if (!inCurrent || current is null)
        {
            return [];
        }

        var value = getText(current.Start, current.End - current.Start);
        _pending.Clear();
        foreach (var mirror in _occurrences.Where(o => o.Index == current.Index && o != current && !o.IsRemoved))
        {
            var text = mirror.Transform?.Apply(value) ?? value;
            if (!string.Equals(getText(mirror.Start, mirror.End - mirror.Start), text, StringComparison.Ordinal))
            {
                _pending.Add((new TextReplacement(mirror.Start, mirror.End - mirror.Start, text), mirror));
            }
        }

        // Applied from the end, so earlier offsets stay valid.
        _pending.Sort((a, b) => b.Replacement.Start.CompareTo(a.Replacement.Start));
        return [.. _pending.Select(p => p.Replacement)];
    }

    /// <summary>The occurrence a replacement this session asked for was meant for.</summary>
    private Occurrence? TakePendingTarget(int offset, int removedLength)
    {
        var index = _pending.FindIndex(p => p.Replacement.Start == offset && p.Replacement.Length == removedLength);
        if (index < 0)
        {
            return null;
        }

        var target = _pending[index].Target;
        _pending.RemoveAt(index);
        return target;
    }

    /// <summary>Moves an offset through a change; an offset inside removed text moves to the change's start or end.</summary>
    private static int Map(int position, int start, int removedEnd, int delta, bool afterInsertion)
    {
        if (position < start || (position == start && !afterInsertion))
        {
            return position;
        }

        if (position > removedEnd || (position == removedEnd && removedEnd > start))
        {
            return position + delta;
        }

        // Inside the removed text, or exactly at an insertion point.
        return afterInsertion ? removedEnd + delta : start;
    }

    private Occurrence? Primary(int index) => _occurrences.FirstOrDefault(o => o.Index == index && o.IsPrimary && !o.IsRemoved);

    /// <summary>A tab stop occurrence and where it is now.</summary>
    private sealed class Occurrence(int index, int start, int end, IReadOnlyList<string>? choices, SnippetTransform? transform)
    {
        public int Index { get; } = index;

        public int Start { get; set; } = start;

        public int End { get; set; } = end;

        public IReadOnlyList<string>? Choices { get; } = choices;

        public SnippetTransform? Transform { get; } = transform;

        public bool IsPrimary { get; set; }

        public bool IsRemoved { get; set; }
    }
}
