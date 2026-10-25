using TabStudio.Notebooks.Formatting;
using TabStudio.Notebooks.Outputs;

namespace TabStudio.Notebooks.Tests;

public sealed class ValueFormatterTests
{
    private readonly ValueFormatter _formatter = ValueFormatter.Default;

    [Theory]
    [InlineData(null, "<null>")]
    [InlineData("text as it is", "text as it is")]
    [InlineData(42, "42")]
    [InlineData(1.5, "1.5")]
    [InlineData(true, "True")]
    [InlineData('x', "x")]
    [InlineData(DayOfWeek.Friday, "Friday")]
    public void Simple_values_are_plain_text_only(object? value, string expected)
    {
        var formatted = _formatter.Format(value);

        Assert.Equal(expected, formatted.Data.PlainText);
        Assert.Equal(1, formatted.Data.Count);
        Assert.Null(formatted.Table);
    }

    [Fact]
    public void Dates_and_decimals_use_the_invariant_culture()
    {
        Assert.Equal("1234.5", _formatter.Format(1234.5m).Data.PlainText);
        Assert.Equal("10/25/2026 13:45:00", _formatter.Format(new DateTime(2026, 10, 25, 13, 45, 0)).Data.PlainText);
    }

    [Fact]
    public void A_sequence_of_objects_becomes_a_table_with_a_column_per_property()
    {
        var people = new[] { new Person("Ada", 36), new Person("Alan", 41) };

        var formatted = _formatter.Format(people);

        var table = Assert.IsType<TableOutput>(formatted.Table);
        Assert.Equal(new[] { "Name", "Age" }, table.Columns);
        Assert.Equal(new[] { "Ada", "36" }, table.Rows[0]);
        Assert.Equal(new[] { "Alan", "41" }, table.Rows[1]);
        Assert.False(table.IsTruncated);
        Assert.Equal(2, table.TotalRowCount);
        Assert.Contains("<th>Name</th><th>Age</th>", formatted.Data.Html, StringComparison.Ordinal);
        Assert.Contains("<tr><td>Ada</td><td>36</td></tr>", formatted.Data.Html, StringComparison.Ordinal);
        Assert.Equal("Name  Age\n----  ---\nAda   36\nAlan  41", formatted.Data.PlainText);
    }

    [Fact]
    public void LINQ_results_of_anonymous_types_and_tuples_become_tables()
    {
        var anonymous = _formatter.Format(Enumerable.Range(1, 3).Select(i => new { Number = i, Square = i * i }));
        var tuples = _formatter.Format(new List<(string, int)> { ("a", 1) });

        Assert.Equal(new[] { "Number", "Square" }, anonymous.Table?.Columns);
        Assert.Equal(new[] { "3", "9" }, anonymous.Table?.Rows[2]);
        Assert.Equal(new[] { "Item1", "Item2" }, tuples.Table?.Columns);
    }

    [Fact]
    public void Only_the_first_hundred_rows_are_shown()
    {
        var list = _formatter.Format(Enumerable.Range(0, 1000).ToList());
        var endless = _formatter.Format(Count());

        Assert.Equal(100, list.Table?.Rows.Count);
        Assert.True(list.Table?.IsTruncated);
        Assert.Equal(1000, list.Table?.TotalRowCount);
        Assert.Contains("(900 more rows)", list.Data.Html, StringComparison.Ordinal);
        Assert.Equal(100, endless.Table?.Rows.Count);
        Assert.Null(endless.Table?.TotalRowCount);
        Assert.EndsWith("99, …]", endless.Data.PlainText, StringComparison.Ordinal);

        static IEnumerable<int> Count()
        {
            for (var i = 0; ; i++)
            {
                yield return i;
            }
        }
    }

    [Fact]
    public void A_sequence_of_simple_values_has_one_value_column()
    {
        var formatted = _formatter.Format(new[] { 1, 2, 3 });

        Assert.Equal(new[] { ValueFormatter.ValueColumn }, formatted.Table?.Columns);
        Assert.Equal("[1, 2, 3]", formatted.Data.PlainText);
    }

    [Fact]
    public void A_dictionary_becomes_a_key_value_table()
    {
        var formatted = _formatter.Format(new Dictionary<string, int> { ["apples"] = 3, ["pears"] = 5 });

        var table = Assert.IsType<TableOutput>(formatted.Table);
        Assert.Equal(new[] { "Key", "Value" }, table.Columns);
        Assert.Equal(new[] { "apples", "3" }, table.Rows[0]);
        Assert.Equal(new[] { "pears", "5" }, table.Rows[1]);
    }

    [Fact]
    public void An_object_becomes_a_one_row_table_and_keeps_its_own_text()
    {
        var formatted = _formatter.Format(new Person("Grace", 85));

        Assert.Equal("Person { Name = Grace, Age = 85 }", formatted.Data.PlainText);
        Assert.Equal(new[] { "Grace", "85" }, Assert.Single(formatted.Table!.Rows));
    }

    [Fact]
    public void Nested_collections_are_listed_inside_a_cell()
    {
        var formatted = _formatter.Format(new[] { new Team("Core", ["Ada", "Alan"]) });

        Assert.Equal(new[] { "Core", "[Ada, Alan]" }, formatted.Table?.Rows[0]);
    }

    [Fact]
    public void Html_in_values_is_escaped()
    {
        var formatted = _formatter.Format(new[] { new Person("<script>&", 1) });

        Assert.Contains("<td>&lt;script&gt;&amp;</td>", formatted.Data.Html, StringComparison.Ordinal);
        Assert.DoesNotContain("<script>", formatted.Data.Html, StringComparison.Ordinal);
    }

    [Fact]
    public void A_property_that_throws_shows_the_error_in_its_cell()
    {
        var formatted = _formatter.Format(new[] { new Fragile() });

        Assert.Equal(new[] { "fine", "<InvalidOperationException: broken>" }, formatted.Table?.Rows[0]);
    }

    [Fact]
    public void Tasks_and_types_are_shown_as_text_without_reading_their_properties()
    {
        Assert.Null(_formatter.Format(Task.FromResult(1)).Table);
        Assert.Null(_formatter.Format(typeof(string)).Table);
        Assert.Equal("System.String", _formatter.Format(typeof(string)).Data.PlainText);
    }

    private sealed record Person(string Name, int Age);

    private sealed record Team(string Name, string[] Members);

    private sealed class Fragile
    {
        private readonly string _message = "broken";

        public string Good => _message.Length > 0 ? "fine" : string.Empty;

        public string Bad => throw new InvalidOperationException(_message);
    }
}
