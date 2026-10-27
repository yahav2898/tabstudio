using TabStudio.Live.Text;

namespace TabStudio.Live.Tests;

/// <summary>Undo is per participant: it takes back what this participant did, never what someone else did meanwhile.</summary>
public sealed class UndoTests
{
    [Fact]
    public void Undo_takes_back_only_this_participants_typing()
    {
        var time = new ManualTime();
        var a = new SharedText(1);
        var b = new SharedText(2);
        var undo = new TextUndoStack(a, time);

        Type(a, b, undo, 0, "hello", time);
        time.Advance(TimeSpan.FromSeconds(5));
        foreach (var c in " world")
        {
            a.Apply(b.Insert(b.Length, new string(c, 1))!);
        }

        Assert.Equal("hello world", a.ToString());
        var undone = undo.Undo()!;
        foreach (var operation in undone.Operations)
        {
            b.Apply(operation);
        }

        Assert.Equal(" world", a.ToString());
        Assert.Equal(" world", b.ToString());
        Assert.Equal([new TextChange(0, 5, string.Empty)], undone.Changes);
        Assert.False(undo.CanUndo);
        Assert.True(undo.CanRedo);

        var redone = undo.Redo()!;
        foreach (var operation in redone.Operations)
        {
            b.Apply(operation);
        }

        Assert.Equal("hello world", a.ToString());
        Assert.Equal("hello world", b.ToString());
    }

    [Fact]
    public void Typing_without_a_pause_is_one_step_and_a_pause_starts_another()
    {
        var time = new ManualTime();
        var a = new SharedText(1);
        var undo = new TextUndoStack(a, time);

        Type(a, null, undo, 0, "abc", time);
        time.Advance(TimeSpan.FromSeconds(3));
        Type(a, null, undo, 3, "def", time);

        undo.Undo();
        Assert.Equal("abc", a.ToString());
        undo.Undo();
        Assert.Equal(string.Empty, a.ToString());
        Assert.False(undo.CanUndo);
    }

    [Fact]
    public void Undo_of_a_deletion_puts_the_text_back_where_it_was_after_others_edited()
    {
        var a = new SharedText(1);
        var b = new SharedText(2);
        var undo = new TextUndoStack(a);
        b.Apply(a.Insert(0, "one two three")!);
        undo.Clear();

        var removed = a.Substring(4, 4);
        var deletion = a.Delete(4, 4)!;
        undo.Record(deletion, removed, 4);
        b.Apply(deletion);
        a.Apply(b.Insert(0, ">> ")!);
        Assert.Equal(">> one three", a.ToString());

        foreach (var operation in undo.Undo()!.Operations)
        {
            b.Apply(operation);
        }

        Assert.Equal(">> one two three", a.ToString());
        Assert.Equal(a.ToString(), b.ToString());
    }

    [Fact]
    public void Undo_skips_text_someone_else_already_deleted()
    {
        var a = new SharedText(1);
        var b = new SharedText(2);
        var undo = new TextUndoStack(a);
        var typed = a.Insert(0, "abcdef")!;
        undo.Record(typed, null, 0);
        b.Apply(typed);
        a.Apply(b.Delete(0, 3)!);

        var undone = undo.Undo()!;
        Assert.Equal(string.Empty, a.ToString());
        Assert.Equal([new TextChange(0, 3, string.Empty)], undone.Changes);
        foreach (var operation in undone.Operations)
        {
            b.Apply(operation);
        }

        Assert.Equal(string.Empty, b.ToString());
    }

    [Fact]
    public void A_group_is_one_step()
    {
        var a = new SharedText(1);
        var undo = new TextUndoStack(a);
        a.Insert(0, "x = 1;");
        undo.BeginGroup();
        var removed = a.Substring(4, 1);
        undo.Record(a.Delete(4, 1)!, removed, 4);
        undo.Record(a.Insert(4, "42")!, null, 4);
        undo.EndGroup();
        Assert.Equal("x = 42;", a.ToString());

        undo.Undo();
        Assert.Equal("x = 1;", a.ToString());
        undo.Redo();
        Assert.Equal("x = 42;", a.ToString());
    }

    private static void Type(SharedText text, SharedText? other, TextUndoStack undo, int offset, string typed, ManualTime time)
    {
        foreach (var c in typed)
        {
            var operation = text.Insert(offset, new string(c, 1))!;
            undo.Record(operation, null, offset);
            other?.Apply(operation);
            offset++;
            time.Advance(TimeSpan.FromMilliseconds(150));
        }
    }

    /// <summary>A clock the test moves.</summary>
    private sealed class ManualTime : TimeProvider
    {
        private long _ticks = 1_000_000;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => _ticks;

        public void Advance(TimeSpan by) => _ticks += by.Ticks;
    }
}
