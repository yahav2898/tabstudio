using System.Text;

namespace TabStudio.Live.Text;

/// <summary>
/// A text that several devices edit at once and that always ends up the same on all of them: a replicated growable
/// array (RGA), the sequence CRDT, with runs of characters kept together.
/// </summary>
/// <remarks>
/// <para>
/// Every character has an identity (<see cref="CharId"/>). An insertion names the character it was typed after; a
/// deletion names the characters it removes and leaves them in place, invisible, so later operations can still find
/// them. Where several devices insert after the same character at once, the insertion with the larger identity comes
/// first, so every device orders them the same way without asking another. Applying the same operations in any order
/// that keeps each participant's own order gives the same text.
/// </para>
/// <para>
/// Operations that arrive before what they depend on (a character they name, or an earlier operation of the same
/// participant) wait until it arrives; operations that arrive twice are ignored. Characters typed one after the other
/// are stored as one run, so a typed file costs about as much as the file.
/// </para>
/// <para>Not thread-safe: one thread at a time, which in the app is the editor's.</para>
/// </remarks>
public sealed class SharedText
{
    /// <summary>How many operations may wait for what they depend on before the text gives up on the sender.</summary>
    public const int MaxPendingOperations = 100_000;

    private readonly Block _head = new(0, 0, string.Empty) { IsDeleted = true };
    private readonly Dictionary<int, List<Block>> _byReplica = [];
    private readonly List<TextOperation> _log = [];
    private readonly List<TextOperation> _pending = [];
    private readonly VersionVector _version = new();
    private long _clock;
    private int _length;

    /// <summary>Creates an empty text for a participant.</summary>
    /// <param name="replica">The participant this device edits as; never 0.</param>
    public SharedText(int replica)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(replica);
        Replica = replica;
    }

    /// <summary>The participant this device edits as.</summary>
    public int Replica { get; }

    /// <summary>The number of visible characters.</summary>
    public int Length => _length;

    /// <summary>Every operation applied, in the order this device applied them, which respects what each depends on.</summary>
    public IReadOnlyList<TextOperation> Operations => _log;

    /// <summary>How many operations wait for something they depend on.</summary>
    public int PendingCount => _pending.Count;

    /// <summary>A copy of which operations are applied.</summary>
    public VersionVector Version => _version.Clone();

    /// <summary>The largest clock seen, which the next local edit goes beyond.</summary>
    public long Clock => _clock;

    /// <summary>The visible text.</summary>
    public override string ToString()
    {
        var text = new StringBuilder(_length);
        for (var block = _head.Next; block is not null; block = block.Next)
        {
            if (!block.IsDeleted)
            {
                text.Append(block.Text);
            }
        }

        return text.ToString();
    }

    /// <summary>Part of the visible text.</summary>
    /// <param name="index">The first character.</param>
    /// <param name="length">How many characters.</param>
    public string Substring(int index, int length)
    {
        CheckRange(index, length);
        var text = new StringBuilder(length);
        var position = 0;
        for (var block = _head.Next; block is not null && text.Length < length; block = block.Next)
        {
            if (block.IsDeleted)
            {
                continue;
            }

            var start = Math.Max(index - position, 0);
            if (start < block.Length)
            {
                var take = Math.Min(block.Length - start, length - text.Length);
                text.Append(block.Text, start, take);
            }

            position += block.Length;
        }

        return text.ToString();
    }

    /// <summary>Inserts text typed on this device and returns the operation for the other devices.</summary>
    /// <param name="index">Where, in visible characters.</param>
    /// <param name="text">What; nothing is done for an empty text.</param>
    /// <returns>The operation, or <see langword="null"/> when there is nothing to insert.</returns>
    public TextOperation? Insert(int index, string text) => Insert(index, text, null);

    /// <summary>Inserts text on this device, recording what it did to the visible text, as an undo needs.</summary>
    /// <param name="index">Where, in visible characters.</param>
    /// <param name="text">What; nothing is done for an empty text.</param>
    /// <param name="changes">Receives the change of the visible text; <see langword="null"/> when not needed.</param>
    /// <returns>The operation, or <see langword="null"/> when there is nothing to insert.</returns>
    public TextOperation? Insert(int index, string text, ICollection<TextChange>? changes)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(index, _length);
        if (text.Length == 0)
        {
            return null;
        }

        var origin = index == 0 ? (CharId?)null : CharAt(index - 1);
        var operation = TextOperation.Insert(Replica, _version[Replica] + 1, _clock + 1, origin, text);
        IntegrateLocal(operation, changes);
        return operation;
    }

    /// <summary>Deletes text on this device and returns the operation for the other devices.</summary>
    /// <param name="index">Where the deleted text starts, in visible characters.</param>
    /// <param name="length">How many characters; nothing is done for none.</param>
    /// <returns>The operation, or <see langword="null"/> when there is nothing to delete.</returns>
    public TextOperation? Delete(int index, int length)
    {
        CheckRange(index, length);
        if (length == 0)
        {
            return null;
        }

        var ranges = VisibleRanges(index, length);
        var operation = TextOperation.Delete(Replica, _version[Replica] + 1, _clock + 1, ranges);
        IntegrateLocal(operation, null);
        return operation;
    }

    /// <summary>Replaces text on this device, as an editor reports a change, and returns the operations.</summary>
    /// <param name="index">Where.</param>
    /// <param name="length">How many characters are replaced.</param>
    /// <param name="text">What replaces them.</param>
    /// <returns>The deletion and the insertion, those that did something, in that order.</returns>
    public IReadOnlyList<TextOperation> Replace(int index, int length, string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var operations = new List<TextOperation>(2);
        if (Delete(index, length) is { } deleted)
        {
            operations.Add(deleted);
        }

        if (Insert(index, text) is { } inserted)
        {
            operations.Add(inserted);
        }

        return operations;
    }

    /// <summary>
    /// Applies an operation from another device. One that arrives early waits for what it depends on; one that arrived
    /// before is ignored.
    /// </summary>
    /// <param name="operation">The operation.</param>
    /// <returns>What it, and any waiting operations it let through, did to the visible text, in order.</returns>
    /// <exception cref="InvalidDataException">The operation is malformed, or too many operations wait.</exception>
    public IReadOnlyList<TextChange> Apply(TextOperation operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (!operation.IsValid())
        {
            throw new InvalidDataException("The edit is malformed.");
        }

        var changes = new List<TextChange>();
        if (_version.Contains(operation))
        {
            return changes;
        }

        if (!IsReady(operation))
        {
            if (_pending.Count >= MaxPendingOperations)
            {
                throw new InvalidDataException("Too many edits wait for edits that never arrived.");
            }

            _pending.Add(operation);
            return changes;
        }

        Integrate(operation, changes);
        ApplyPending(changes);
        return changes;
    }

    /// <summary>The operations another device lacks, in an order it can apply them.</summary>
    /// <param name="version">Which operations the other device has.</param>
    public IReadOnlyList<TextOperation> OperationsSince(VersionVector version)
    {
        ArgumentNullException.ThrowIfNull(version);
        return [.. _log.Where(operation => !version.Contains(operation))];
    }

    /// <summary>A place that stays put while others edit, for a caret or a selection's end.</summary>
    /// <param name="index">The place, in visible characters; clamped to the text.</param>
    public TextPosition PositionAt(int index)
    {
        if (index <= 0 || _length == 0)
        {
            return TextPosition.Start;
        }

        return new TextPosition(CharAt(Math.Min(index, _length) - 1));
    }

    /// <summary>Where a place is now, in visible characters.</summary>
    /// <param name="position">The place.</param>
    /// <returns>The offset, or <see langword="null"/> when it names a character this device does not have yet.</returns>
    public int? IndexOf(TextPosition position)
    {
        if (position.After is not { } after)
        {
            return 0;
        }

        if (Find(after) is not { } block)
        {
            return null;
        }

        var before = VisibleOffsetOf(block);
        return block.IsDeleted ? before : before + (int)(after.Clock - block.Clock) + 1;
    }

    /// <summary>How many visible characters come before a character, whether it is visible or deleted.</summary>
    /// <param name="id">The character.</param>
    /// <returns>The offset, or <see langword="null"/> when this device does not have it.</returns>
    public int? OffsetOf(CharId id)
    {
        if (Find(id) is not { } block)
        {
            return null;
        }

        var before = VisibleOffsetOf(block);
        return block.IsDeleted ? before : before + (int)(id.Clock - block.Clock);
    }

    /// <summary>The characters of some runs that are still visible, as an undo of their insertion deletes them.</summary>
    /// <param name="ranges">The runs.</param>
    public IReadOnlyList<CharRange> VisibleParts(IReadOnlyList<CharRange> ranges)
    {
        ArgumentNullException.ThrowIfNull(ranges);
        var visible = new List<CharRange>();
        foreach (var range in ranges)
        {
            var clock = range.Clock;
            while (clock <= range.LastClock)
            {
                if (Find(new CharId(range.Replica, clock)) is not { } block)
                {
                    break;
                }

                var end = Math.Min(block.LastClock, range.LastClock);
                if (!block.IsDeleted)
                {
                    AddRange(visible, new CharRange(range.Replica, clock, (int)(end - clock + 1)));
                }

                clock = end + 1;
            }
        }

        return visible;
    }

    /// <summary>Deletes characters by identity on this device, as an undo of their insertion does.</summary>
    /// <param name="ranges">The characters; those already deleted are skipped.</param>
    /// <param name="changes">Receives the changes of the visible text; <see langword="null"/> when not needed.</param>
    /// <returns>The operation, or <see langword="null"/> when none of them is visible.</returns>
    public TextOperation? Delete(IReadOnlyList<CharRange> ranges, ICollection<TextChange>? changes)
    {
        var visible = VisibleParts(ranges);
        if (visible.Count == 0)
        {
            return null;
        }

        var operation = TextOperation.Delete(Replica, _version[Replica] + 1, _clock + 1, visible);
        IntegrateLocal(operation, changes);
        return operation;
    }

    /// <summary>The visible text of runs of characters, in the order the runs are given.</summary>
    /// <param name="ranges">The runs; deleted characters in them are left out.</param>
    public string TextOf(IReadOnlyList<CharRange> ranges)
    {
        ArgumentNullException.ThrowIfNull(ranges);
        var text = new StringBuilder();
        foreach (var range in VisibleParts(ranges))
        {
            var clock = range.Clock;
            while (clock <= range.LastClock && Find(new CharId(range.Replica, clock)) is { } block)
            {
                var start = (int)(clock - block.Clock);
                var take = (int)Math.Min(block.Length - start, range.LastClock - clock + 1);
                text.Append(block.Text, start, take);
                clock += take;
            }
        }

        return text.ToString();
    }

    private static void AddRange(List<CharRange> ranges, CharRange range)
    {
        if (ranges.Count > 0 && ranges[^1] is var last && last.Replica == range.Replica && last.LastClock + 1 == range.Clock)
        {
            ranges[^1] = last with { Length = last.Length + range.Length };
        }
        else
        {
            ranges.Add(range);
        }
    }

    private void CheckRange(int index, int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        if (index + length > _length)
        {
            throw new ArgumentOutOfRangeException(nameof(length), "The range goes past the end of the text.");
        }
    }

    /// <summary>Whether an operation's dependencies are applied: the participant's previous operation and every character it names.</summary>
    private bool IsReady(TextOperation operation)
    {
        if (operation.Sequence != _version[operation.Replica] + 1)
        {
            return false;
        }

        if (operation.IsInsert)
        {
            return operation.Origin is not { } origin || Find(origin) is not null;
        }

        foreach (var range in operation.Deleted!)
        {
            var clock = range.Clock;
            while (clock <= range.LastClock)
            {
                if (Find(new CharId(range.Replica, clock)) is not { } block)
                {
                    return false;
                }

                clock = block.LastClock + 1;
            }
        }

        return true;
    }

    private void ApplyPending(List<TextChange> changes)
    {
        bool progressed;
        do
        {
            progressed = false;
            for (var i = 0; i < _pending.Count; i++)
            {
                var operation = _pending[i];
                if (_version.Contains(operation))
                {
                    _pending.RemoveAt(i--);
                    continue;
                }

                if (IsReady(operation))
                {
                    _pending.RemoveAt(i--);
                    Integrate(operation, changes);
                    progressed = true;
                }
            }
        }
        while (progressed);
    }

    /// <summary>Applies an operation made on this device, recording what it did to the visible text when asked.</summary>
    private void IntegrateLocal(TextOperation operation, ICollection<TextChange>? changes)
    {
        if (changes is null)
        {
            Integrate(operation, null);
            return;
        }

        var made = new List<TextChange>();
        Integrate(operation, made);
        foreach (var change in made)
        {
            changes.Add(change);
        }
    }

    /// <summary>Applies a ready operation, recording what it did to the visible text when asked.</summary>
    private void Integrate(TextOperation operation, List<TextChange>? changes)
    {
        if (operation.IsInsert)
        {
            IntegrateInsert(operation, changes);
        }
        else
        {
            IntegrateDelete(operation, changes);
        }

        _version.Advance(operation.Replica, operation.Sequence);
        _clock = Math.Max(_clock, operation.LastClock);
        _log.Add(operation);
    }

    private void IntegrateInsert(TextOperation operation, List<TextChange>? changes)
    {
        var text = operation.Text!;
        var id = new CharId(operation.Replica, operation.Clock);
        var left = _head;
        var placed = false;
        if (operation.Origin is { } origin)
        {
            left = Find(origin) ?? throw new InvalidDataException($"The edit follows character {origin}, which is not in the text.");
            var offset = (int)(origin.Clock - left.Clock);
            if (offset < left.Length - 1)
            {
                // The origin is inside a run. The character after it is the origin's own successor; when the new run is
                // larger, it goes between them, otherwise after the whole rest of the run, which is larger still.
                if (CharId.Compare(left.First.Plus(offset + 1), id) < 0)
                {
                    Split(left, offset + 1);
                    placed = true;
                }
            }
        }

        if (!placed)
        {
            // Skip what other devices inserted at the same place with larger identities, and what follows from them.
            while (left.Next is { } next && CharId.Compare(next.First, id) > 0)
            {
                left = next;
            }
        }

        // Typing goes on at the end of the same participant's run: the run grows instead of a new one starting.
        if (!left.IsDeleted && left.Replica == operation.Replica && left.LastClock + 1 == operation.Clock
            && operation.Origin == left.First.Plus(left.Length - 1) && !ReferenceEquals(left, _head))
        {
            var at = changes is null ? 0 : VisibleOffsetOf(left) + left.Length;
            left.Text += text;
            _length += text.Length;
            changes?.Add(new TextChange(at, 0, text));
            return;
        }

        var block = new Block(operation.Replica, operation.Clock, text);
        Link(left, block);
        AddToReplica(block);
        _length += text.Length;
        if (changes is not null)
        {
            changes.Add(new TextChange(VisibleOffsetOf(block), 0, text));
        }
    }

    private void IntegrateDelete(TextOperation operation, List<TextChange>? changes)
    {
        foreach (var range in operation.Deleted!)
        {
            var clock = range.Clock;
            while (clock <= range.LastClock)
            {
                var block = Find(new CharId(range.Replica, clock)) ?? throw new InvalidDataException($"The edit deletes character {range.Replica}:{clock}, which is not in the text.");
                var start = (int)(clock - block.Clock);
                if (start > 0)
                {
                    Split(block, start);
                    block = block.Next!;
                }

                var take = (int)Math.Min(block.Length, range.LastClock - clock + 1);
                if (take < block.Length)
                {
                    Split(block, take);
                }

                if (!block.IsDeleted)
                {
                    if (changes is not null)
                    {
                        changes.Add(new TextChange(VisibleOffsetOf(block), block.Length, string.Empty));
                    }

                    block.IsDeleted = true;
                    _length -= block.Length;
                }

                clock += take;
            }
        }
    }

    /// <summary>The identity of the visible character at an offset.</summary>
    private CharId CharAt(int index)
    {
        var position = 0;
        for (var block = _head.Next; block is not null; block = block.Next)
        {
            if (block.IsDeleted)
            {
                continue;
            }

            if (index < position + block.Length)
            {
                return block.First.Plus(index - position);
            }

            position += block.Length;
        }

        throw new ArgumentOutOfRangeException(nameof(index), "The offset is past the end of the text.");
    }

    /// <summary>The visible characters of a range, as runs by identity.</summary>
    private List<CharRange> VisibleRanges(int index, int length)
    {
        var ranges = new List<CharRange>();
        var position = 0;
        var end = index + length;
        for (var block = _head.Next; block is not null && position < end; block = block.Next)
        {
            if (block.IsDeleted)
            {
                continue;
            }

            var from = Math.Max(index, position);
            var to = Math.Min(end, position + block.Length);
            if (from < to)
            {
                AddRange(ranges, new CharRange(block.Replica, block.Clock + (from - position), to - from));
            }

            position += block.Length;
        }

        return ranges;
    }

    /// <summary>How many visible characters come before a run.</summary>
    private int VisibleOffsetOf(Block target)
    {
        var position = 0;
        for (var block = _head.Next; block is not null; block = block.Next)
        {
            if (ReferenceEquals(block, target))
            {
                return position;
            }

            if (!block.IsDeleted)
            {
                position += block.Length;
            }
        }

        throw new InvalidOperationException("The run is not in the text.");
    }

    /// <summary>The run that holds a character, or <see langword="null"/> when this device does not have it.</summary>
    private Block? Find(CharId id)
    {
        if (!_byReplica.TryGetValue(id.Replica, out var blocks))
        {
            return null;
        }

        var low = 0;
        var high = blocks.Count - 1;
        while (low <= high)
        {
            var middle = low + ((high - low) / 2);
            var block = blocks[middle];
            if (id.Clock < block.Clock)
            {
                high = middle - 1;
            }
            else if (id.Clock > block.LastClock)
            {
                low = middle + 1;
            }
            else
            {
                return block;
            }
        }

        return null;
    }

    /// <summary>Splits a run in two at an offset; the second part follows the first.</summary>
    private void Split(Block block, int offset)
    {
        var right = new Block(block.Replica, block.Clock + offset, block.Text[offset..]) { IsDeleted = block.IsDeleted };
        block.Text = block.Text[..offset];
        Link(block, right);
        var blocks = _byReplica[block.Replica];
        blocks.Insert(IndexInReplica(blocks, block) + 1, right);
    }

    private static int IndexInReplica(List<Block> blocks, Block block)
    {
        var low = 0;
        var high = blocks.Count - 1;
        while (low <= high)
        {
            var middle = low + ((high - low) / 2);
            var clock = blocks[middle].Clock;
            if (clock == block.Clock)
            {
                return middle;
            }

            if (clock < block.Clock)
            {
                low = middle + 1;
            }
            else
            {
                high = middle - 1;
            }
        }

        throw new InvalidOperationException("The run is not indexed.");
    }

    private void AddToReplica(Block block)
    {
        if (!_byReplica.TryGetValue(block.Replica, out var blocks))
        {
            blocks = [];
            _byReplica[block.Replica] = blocks;
        }

        // A participant's runs are almost always added in clock order; insertion keeps the list sorted otherwise.
        var index = blocks.Count;
        while (index > 0 && blocks[index - 1].Clock > block.Clock)
        {
            index--;
        }

        blocks.Insert(index, block);
    }

    private static void Link(Block left, Block block)
    {
        block.Previous = left;
        block.Next = left.Next;
        if (left.Next is { } next)
        {
            next.Previous = block;
        }

        left.Next = block;
    }

    /// <summary>A run of characters one participant typed one after the other, visible or deleted together.</summary>
    private sealed class Block(int replica, long clock, string text)
    {
        public int Replica { get; } = replica;

        public long Clock { get; } = clock;

        public string Text { get; set; } = text;

        public bool IsDeleted { get; set; }

        public Block? Previous { get; set; }

        public Block? Next { get; set; }

        public int Length => Text.Length;

        public CharId First => new(Replica, Clock);

        public long LastClock => Clock + Text.Length - 1;
    }
}
