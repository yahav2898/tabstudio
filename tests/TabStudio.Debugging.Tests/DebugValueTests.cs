using System.Globalization;
using TabStudio.Debugging.Inspection;

namespace TabStudio.Debugging.Tests;

public sealed class DebugValueTests
{
    private readonly ValueInspector _inspector = new(new Evaluator(TimeSpan.FromSeconds(5)));

    [Fact]
    public void Formats_strings_and_characters_as_csharp_literals()
    {
        Assert.Equal("\"a\\\"b\\n\"", Value("a\"b\n").Value);
        Assert.Equal("'x'", Value('x').Value);
        Assert.Equal("'\\''", Value('\'').Value);
        Assert.Equal("\"\\u0001\"", Value("\u0001").Value);
    }

    [Fact]
    public void Formats_numbers_in_the_invariant_culture()
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
        try
        {
            Assert.Equal("0.5", Value(0.5).Value);
            Assert.Equal("1234.5", Value(1234.5m).Value);
            Assert.Equal("-3", Value(-3).Value);
            Assert.Equal("1.5", Value(1.5f).Value);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void Formats_null_enums_dates_and_tuples()
    {
        Assert.Equal("null", Value(null, "string").Value);
        Assert.Equal("string", Value(null, "string").Type);
        Assert.Equal("Friday", Value(DayOfWeek.Friday).Value);
        Assert.Equal("ReadOnly | Hidden", Value(FileAttributes.ReadOnly | FileAttributes.Hidden).Value);
        Assert.Equal("2026-10-25T14:30:00.0000000Z", Value(new DateTime(2026, 10, 25, 14, 30, 0, DateTimeKind.Utc)).Value);
        Assert.Equal("true", Value(true).Value);
        Assert.Equal("(1, \"a\")", Value((1, "a")).Value);
        Assert.Equal("(int, string)", Value((1, "a")).Type);
    }

    [Fact]
    public void Shows_counts_for_collections_and_lengths_for_arrays()
    {
        var list = Value(new List<int> { 1, 2, 3 });
        var array = Value(new[] { 4, 5 });

        Assert.Equal("Count = 3", list.Value);
        Assert.Equal("List<int>", list.Type);
        Assert.True(list.HasChildren);
        Assert.Equal(new[] { "[0]", "[1]", "[2]" }, list.Expand().Select(c => c.Name));
        Assert.Equal(new[] { "1", "2", "3" }, list.Expand().Select(c => c.Value));
        Assert.Equal("Length = 2", array.Value);
        Assert.Equal("int[]", array.Type);
        Assert.Equal("5", array.Expand()[1].Value);
    }

    [Fact]
    public void Expands_dictionaries_into_entries()
    {
        var dictionary = Value(new Dictionary<string, int> { ["a"] = 1 });

        var entry = Assert.Single(dictionary.Expand());

        Assert.Equal("Count = 1", dictionary.Value);
        Assert.Equal("Dictionary<string, int>", dictionary.Type);
        Assert.Equal("[\"a\"]", entry.Name);
        Assert.Equal("1", entry.Value);
        Assert.Equal("int", entry.Type);
    }

    [Fact]
    public void Expands_objects_into_public_members_without_indexers_or_throwing_properties()
    {
        var sample = Value(new Sample());

        var children = sample.Expand();

        Assert.Equal("{DebugValueTests.Sample}", sample.Value);
        Assert.Equal(new[] { "Number", "Text" }, children.Select(c => c.Name));
        Assert.Equal("4", children[0].Value);
        Assert.Equal("\"t\"", children[1].Value);
        Assert.False(children[0].HasChildren);
    }

    [Fact]
    public void Shows_the_runtime_type_when_it_is_more_specific_than_the_declared_one()
    {
        Assert.Equal("object {string}", Value("text", "object").Type);
        Assert.Equal("int?", Value(5, "int?").Type);
        Assert.Equal("IEnumerable<int> {List<int>}", Value(new List<int>(), "IEnumerable<int>").Type);
    }

    [Fact]
    public void Uses_to_string_overrides_in_braces()
    {
        Assert.Equal("{Pair { Left = 1, Right = 2 }}", Value(new Pair(1, 2)).Value);
    }

    private DebugValue Value(object? value, string? declaredType = null) => new("x", declaredType, value, _inspector);

    private sealed record Pair(int Left, int Right);

    private sealed class Sample
    {
        public string Text = "t";

        public int Number => Text.Length + 3;

        public int Throws => throw new InvalidOperationException(Text);

        public int this[int index] => index;
    }
}
