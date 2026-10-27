using TabStudio.Live.Text;

namespace TabStudio.Live.Tests;

/// <summary>The text CRDT: local edits, concurrent edits that converge, out-of-order and repeated delivery, positions.</summary>
public sealed class SharedTextTests
{
    [Fact]
    public void Local_edits_change_the_text_where_they_are_made()
    {
        var text = new SharedText(1);
        text.Insert(0, "hello world");
        text.Insert(5, ",");
        text.Delete(0, 1);
        text.Insert(0, "H");
        text.Replace(7, 5, "there");

        Assert.Equal("Hello, there", text.ToString());
        Assert.Equal(12, text.Length);
        Assert.Equal("lo, t", text.Substring(3, 5));
    }

    [Fact]
    public void Another_device_applies_the_operations_and_reports_the_changes_for_its_editor()
    {
        var a = new SharedText(1);
        var b = new SharedText(2);
        var editor = string.Empty;

        foreach (var operation in new[] { a.Insert(0, "hello world")!, a.Insert(5, ",")!, a.Delete(0, 1)!, a.Insert(0, "H")! })
        {
            editor = Apply(editor, b.Apply(operation));
        }

        Assert.Equal("Hello, world", b.ToString());
        Assert.Equal(b.ToString(), editor);
    }

    [Fact]
    public void Concurrent_inserts_at_the_same_place_merge_in_the_same_order_on_both_devices()
    {
        var a = new SharedText(1);
        var b = new SharedText(2);
        var start = a.Insert(0, "ac")!;
        b.Apply(start);

        var fromA = a.Insert(1, "XX")!;
        var fromB = b.Insert(1, "yy")!;
        a.Apply(fromB);
        b.Apply(fromA);

        Assert.Equal(a.ToString(), b.ToString());
        Assert.True(a.ToString() is "aXXyyc" or "ayyXXc", a.ToString());
    }

    [Fact]
    public void An_insert_and_a_delete_of_the_same_place_both_take_effect()
    {
        var a = new SharedText(1);
        var b = new SharedText(2);
        b.Apply(a.Insert(0, "abcdef")!);

        var deleted = a.Delete(1, 4)!;
        var inserted = b.Insert(3, "XYZ")!;
        a.Apply(inserted);
        b.Apply(deleted);

        Assert.Equal("aXYZf", a.ToString());
        Assert.Equal(a.ToString(), b.ToString());
    }

    [Fact]
    public void Operations_that_arrive_early_wait_and_repeated_ones_are_ignored()
    {
        var a = new SharedText(1);
        var b = new SharedText(2);
        var first = a.Insert(0, "abc")!;
        var second = a.Insert(3, "def")!;
        var third = a.Delete(1, 2)!;

        Assert.Empty(b.Apply(third));
        Assert.Empty(b.Apply(second));
        Assert.Equal(2, b.PendingCount);
        var changes = b.Apply(first);
        Assert.Equal(3, changes.Count);
        Assert.Equal(0, b.PendingCount);
        Assert.Empty(b.Apply(second));
        Assert.Equal("adef", b.ToString());
        Assert.Equal(a.ToString(), b.ToString());
    }

    [Fact]
    public void Malformed_operations_are_refused()
    {
        var text = new SharedText(2);
        Assert.Throws<InvalidDataException>(() => text.Apply(new TextOperation(1, 1, 1, null, string.Empty, null)));
        Assert.Throws<InvalidDataException>(() => text.Apply(new TextOperation(0, 1, 1, null, "x", null)));
        Assert.Throws<InvalidDataException>(() => text.Apply(new TextOperation(1, 1, 1, null, null, [])));
    }

    [Theory]
    [InlineData(1, 2)]
    [InlineData(2, 3)]
    [InlineData(3, 5)]
    [InlineData(4, 5)]
    [InlineData(5, 8)]
    [InlineData(6, 4)]
    [InlineData(7, 6)]
    [InlineData(8, 3)]
    public void Random_concurrent_edits_converge_on_every_device_in_any_delivery_order(int seed, int devices)
    {
        var random = new Random(seed);
        var network = new Network(devices, random);
        network.Edit(0, text => text.Insert(0, "The quick brown fox jumps over the lazy dog.\n"));
        network.DeliverAll();

        for (var step = 0; step < 600; step++)
        {
            if (random.Next(3) == 0)
            {
                network.DeliverSome();
            }
            else
            {
                network.RandomEdit(random.Next(devices));
            }
        }

        network.DeliverAll();
        network.AssertConverged();
    }

    [Theory]
    [InlineData(11)]
    [InlineData(12)]
    [InlineData(13)]
    public void A_device_that_edits_while_offline_catches_up_both_ways_when_it_reconnects(int seed)
    {
        var random = new Random(seed);
        var network = new Network(3, random);
        network.Edit(0, text => text.Insert(0, "class Program\n{\n    static void Main() { }\n}\n"));
        network.DeliverAll();

        // Device 2 goes offline: it edits on its own while the others edit together.
        network.Disconnect(2);
        for (var step = 0; step < 200; step++)
        {
            network.RandomEdit(random.Next(3));
            if (random.Next(2) == 0)
            {
                network.DeliverSome();
            }
        }

        network.DeliverAll();
        Assert.NotEqual(network.Texts[0].ToString(), network.Texts[2].ToString());

        // It reconnects: each side sends what the other lacks, by version.
        network.Reconnect(2);
        network.AssertConverged();
    }

    [Fact]
    public void A_position_stays_by_its_character_while_others_edit_around_it()
    {
        var a = new SharedText(1);
        var b = new SharedText(2);
        b.Apply(a.Insert(0, "hello world")!);
        var caret = b.PositionAt(6);

        a.Apply(b.Insert(0, ">> ")!);
        Assert.Equal(9, b.IndexOf(caret));
        Assert.Equal(9, a.IndexOf(caret));

        b.Apply(a.Delete(3, 6)!);
        Assert.Equal(3, b.IndexOf(caret));
        Assert.Equal(0, b.IndexOf(TextPosition.Start));
        Assert.Null(new SharedText(3).IndexOf(caret));
    }

    [Fact]
    public void A_late_device_builds_the_same_text_from_the_operations_so_far()
    {
        var a = new SharedText(1);
        a.Insert(0, "abc");
        a.Insert(3, "def");
        a.Delete(0, 2);

        var late = new SharedText(5);
        foreach (var operation in a.OperationsSince(new VersionVector()))
        {
            late.Apply(operation);
        }

        Assert.Equal(a.ToString(), late.ToString());
        Assert.Empty(a.OperationsSince(late.Version));
        var edit = late.Insert(0, "!")!;
        Assert.Equal(1, edit.Sequence);
        a.Apply(edit);
        Assert.Equal("!cdef", a.ToString());
    }

    [Fact]
    public void Typing_a_character_at_a_time_keeps_runs_and_still_merges()
    {
        var a = new SharedText(1);
        var b = new SharedText(2);
        foreach (var c in "hello")
        {
            b.Apply(a.Insert(a.Length, new string(c, 1))!);
        }

        foreach (var c in " world")
        {
            a.Apply(b.Insert(b.Length, new string(c, 1))!);
        }

        Assert.Equal("hello world", a.ToString());
        Assert.Equal("hello world", b.ToString());
    }

    private static string Apply(string text, IEnumerable<TextChange> changes)
    {
        foreach (var change in changes)
        {
            text = text.Remove(change.Offset, change.RemovedLength).Insert(change.Offset, change.InsertedText);
        }

        return text;
    }

    /// <summary>
    /// Devices that exchange operations in a random order, repeating some, as a network with retries could. Each also
    /// keeps an editor's copy of its text, changed only by the changes the text reports, which must stay the same.
    /// </summary>
    private sealed class Network
    {
        private readonly Random _random;
        private readonly List<List<TextOperation>> _inboxes = [];
        private readonly string[] _editors;
        private readonly bool[] _offline;

        public Network(int devices, Random random)
        {
            _random = random;
            _editors = new string[devices];
            _offline = new bool[devices];
            for (var i = 0; i < devices; i++)
            {
                Texts.Add(new SharedText(i + 1));
                _inboxes.Add([]);
                _editors[i] = string.Empty;
            }
        }

        public List<SharedText> Texts { get; } = [];

        public void Edit(int device, Func<SharedText, TextOperation?> edit)
        {
            var before = Texts[device].ToString();
            if (edit(Texts[device]) is not { } operation)
            {
                return;
            }

            // A local edit's change is the editor's own; the editor's copy follows the text.
            _editors[device] = Texts[device].ToString();
            Assert.NotEqual(before, _editors[device]);
            Send(device, operation);
        }

        public void RandomEdit(int device)
        {
            var text = Texts[device];
            if (text.Length > 0 && _random.Next(3) == 0)
            {
                var index = _random.Next(text.Length);
                var length = Math.Min(text.Length - index, 1 + _random.Next(6));
                var expected = text.ToString().Remove(index, length);
                Edit(device, t => t.Delete(index, length));
                Assert.Equal(expected, text.ToString());
            }
            else
            {
                var index = _random.Next(text.Length + 1);
                var inserted = RandomText();
                var expected = text.ToString().Insert(index, inserted);
                Edit(device, t => t.Insert(index, inserted));
                Assert.Equal(expected, text.ToString());
            }
        }

        public void DeliverSome()
        {
            var device = _random.Next(Texts.Count);
            if (_offline[device] || _inboxes[device].Count == 0)
            {
                return;
            }

            var inbox = _inboxes[device];
            var index = _random.Next(inbox.Count);
            var operation = inbox[index];

            // Sometimes an operation arrives twice.
            if (_random.Next(5) != 0)
            {
                inbox.RemoveAt(index);
            }

            Deliver(device, operation);
        }

        public void DeliverAll()
        {
            for (var device = 0; device < Texts.Count; device++)
            {
                if (_offline[device])
                {
                    continue;
                }

                var inbox = _inboxes[device];
                while (inbox.Count > 0)
                {
                    var index = _random.Next(inbox.Count);
                    var operation = inbox[index];
                    inbox.RemoveAt(index);
                    Deliver(device, operation);
                }
            }
        }

        public void Disconnect(int device) => _offline[device] = true;

        public void Reconnect(int device)
        {
            _offline[device] = false;
            _inboxes[device].Clear();
            var offline = Texts[device];
            foreach (var other in Texts.Where(t => !ReferenceEquals(t, offline)))
            {
                foreach (var operation in other.OperationsSince(offline.Version))
                {
                    Deliver(device, operation);
                }

                foreach (var operation in offline.OperationsSince(other.Version))
                {
                    _editors[Texts.IndexOf(other)] = ApplyChanges(_editors[Texts.IndexOf(other)], other.Apply(operation));
                }
            }
        }

        public void AssertConverged()
        {
            var expected = Texts[0].ToString();
            for (var device = 0; device < Texts.Count; device++)
            {
                Assert.Equal(0, Texts[device].PendingCount);
                Assert.Equal(expected, Texts[device].ToString());
                Assert.Equal(expected, _editors[device]);
            }
        }

        private void Send(int from, TextOperation operation)
        {
            for (var device = 0; device < Texts.Count; device++)
            {
                if (device != from && !_offline[from] && !_offline[device])
                {
                    _inboxes[device].Add(operation);
                }
            }
        }

        private void Deliver(int device, TextOperation operation) =>
            _editors[device] = ApplyChanges(_editors[device], Texts[device].Apply(operation));

        private static string ApplyChanges(string text, IReadOnlyList<TextChange> changes)
        {
            foreach (var change in changes)
            {
                Assert.InRange(change.Offset, 0, text.Length);
                text = text.Remove(change.Offset, change.RemovedLength).Insert(change.Offset, change.InsertedText);
            }

            return text;
        }

        private string RandomText()
        {
            const string Alphabet = "abcdefghij \n{}();";
            var length = 1 + _random.Next(_random.Next(4) == 0 ? 12 : 3);
            return new string(Enumerable.Range(0, length).Select(_ => Alphabet[_random.Next(Alphabet.Length)]).ToArray());
        }
    }
}
