using TabStudio.Debugging.Inspection;

namespace TabStudio.Debugging.Tests;

/// <summary>Values, types, paths and breakpoint conditions of Visual Basic code read as Visual Basic.</summary>
public sealed class VisualBasicValueTests
{
    private readonly ValueInspector _inspector = new(new Evaluator(TimeSpan.FromSeconds(5)), visualBasic: true);

    [Fact]
    public void Formats_literals_as_visual_basic_writes_them()
    {
        Assert.Equal("Nothing", Value(null, "String").Value);
        Assert.Equal("String", Value(null, "String").Type);
        Assert.Equal("True", Value(true).Value);
        Assert.Equal("\"a\"\"b\"", Value("a\"b").Value);
        Assert.Equal("\"one\" & vbCrLf & \"two\"", Value("one\r\ntwo").Value);
        Assert.Equal("vbLf", Value("\n").Value);
        Assert.Equal("\"\"", Value(string.Empty).Value);
        Assert.Equal("\"x\"c", Value('x').Value);
        Assert.Equal("ReadOnly Or Hidden", Value(FileAttributes.ReadOnly | FileAttributes.Hidden).Value);
    }

    [Fact]
    public void Formats_types_as_visual_basic_writes_them()
    {
        Assert.Equal("Integer", Value(3).Type);
        Assert.Equal("Integer()", Value(new[] { 1, 2 }).Type);
        Assert.Equal("Integer(,)", Value(new int[2, 2]).Type);
        Assert.Equal("List(Of String)", Value(new List<string>()).Type);
        Assert.Equal("Dictionary(Of String, Integer)", Value(new Dictionary<string, int>()).Type);
        Assert.Equal("Object {String}", Value("text", "Object").Type);
        Assert.Equal("Integer?", Value(5, "Integer?").Type);
        Assert.Equal("(Integer, String)", Value((1, "a")).Type);
        Assert.Equal("Date", Value(new DateTime(2026, 10, 28, 0, 0, 0, DateTimeKind.Utc)).Type);
    }

    [Fact]
    public void Items_and_entries_are_named_with_parentheses_and_found_by_visual_basic_paths()
    {
        var prices = Value(new Dictionary<string, int> { ["tea"] = 3 }, "Dictionary(Of String, Integer)", "prices");
        var items = Value(new List<int> { 4, 5 }, "List(Of Integer)", "items");
        DebugValue[] locals = [prices, items];

        Assert.Equal(new[] { "(0)", "(1)" }, items.Expand().Select(v => v.Name));
        Assert.Equal("(\"tea\")", Assert.Single(prices.Expand()).Name);
        Assert.Equal("5", ValuePath.Find(locals, "Items(1)", visualBasic: true)?.Value);
        Assert.Equal("5", ValuePath.Find(locals, "items[1]", visualBasic: true)?.Value);
        Assert.Equal("3", ValuePath.Find(locals, "PRICES(\"tea\")", visualBasic: true)?.Value);
        Assert.Null(ValuePath.Find(locals, "prices(\"Tea\")", visualBasic: true));
        Assert.Null(ValuePath.Find(locals, "Items[1]", visualBasic: false));
    }

    [Theory]
    [InlineData("count = 3", true)]
    [InlineData("count <> 3", false)]
    [InlineData("Count = 3", true)]
    [InlineData("count >= 3 AndAlso name = \"Ada\"", true)]
    [InlineData("count > 5 OrElse flag", true)]
    [InlineData("count > 5 Or flag = False", false)]
    [InlineData("missing Is Nothing", true)]
    [InlineData("name IsNot Nothing And ratio < 1", true)]
    [InlineData("Not flag", false)]
    [InlineData("Not count = 4", true)]
    [InlineData("flag = True", true)]
    [InlineData("day = Friday", true)]
    [InlineData("day = DayOfWeek.monday", false)]
    [InlineData("letter = \"x\"c", true)]
    [InlineData("name = \"a AndAlso b\"", false)]
    [InlineData("count == 3 && flag", true)]
    public void Evaluates_visual_basic_conditions(string condition, bool expected)
    {
        Assert.Equal(expected, BreakpointCondition.IsMet(condition, Locals(), visualBasic: true, out var error));
        Assert.Null(error);
    }

    [Theory]
    [InlineData("absent > 1")]
    [InlineData("name")]
    [InlineData("count = unknown")]
    public void A_visual_basic_condition_that_cannot_be_evaluated_stops_and_says_why(string condition)
    {
        Assert.True(BreakpointCondition.IsMet(condition, Locals(), visualBasic: true, out var error));
        Assert.False(string.IsNullOrEmpty(error));
    }

    private DebugValue Value(object? value, string? type = null, string name = "value") => new(name, type, value, _inspector);

    private DebugValue[] Locals() =>
    [
        new("count", "Integer", 3, _inspector),
        new("name", "String", "Ada", _inspector),
        new("missing", "String", null, _inspector),
        new("day", "DayOfWeek", DayOfWeek.Friday, _inspector),
        new("ratio", "Double", 0.5, _inspector),
        new("letter", "Char", 'x', _inspector),
        new("flag", "Boolean", true, _inspector),
    ];
}
