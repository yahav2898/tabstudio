using TabStudio.App.Editing;

namespace TabStudio.App.Tests.Editing;

public sealed class DataTipExpressionTests
{
    [Theory]
    [InlineData("var total = order.Total;", "order", "order")]
    [InlineData("var total = order.Total;", "Total", "order.Total")]
    [InlineData("Console.WriteLine(order.Customer.Name);", "Name", "order.Customer.Name")]
    [InlineData("Console.WriteLine(order.Customer.Name);", "Customer", "order.Customer")]
    [InlineData("var price = order.Items[0].Price;", "Price", "order.Items[0].Price")]
    [InlineData("var price = prices[\"tea]\"].Amount;", "Amount", "prices[\"tea]\"].Amount")]
    [InlineData("var count = items.Count;", "items", "items")]
    [InlineData("return this._name;", "_name", "this._name")]
    [InlineData("var name = Console.ReadLine();", "name", "name")]
    public void Evaluates_the_identifier_and_the_accesses_before_it(string line, string word, string expected)
    {
        var column = line.IndexOf(word, StringComparison.Ordinal) + 1;

        var target = DataTipExpression.Find(line, column);

        Assert.Equal(expected, target?.Expression);
        Assert.Equal(line.IndexOf(word, StringComparison.Ordinal), target?.Start);
        Assert.Equal(word.Length, target?.Length);
    }

    [Theory]
    [InlineData("var total = GetOrder().Total;", "Total")]
    [InlineData("var count = 42;", "42")]
    [InlineData("var total = order.Total;", "=")]
    public void Has_no_expression_off_an_identifier_or_after_a_call(string line, string text)
    {
        var column = line.IndexOf(text, StringComparison.Ordinal);

        Assert.Null(DataTipExpression.Find(line, column));
    }

    [Fact]
    public void The_end_of_an_identifier_still_counts_as_on_it()
    {
        const string Line = "var x = count;";

        Assert.Equal("count", DataTipExpression.Find(Line, Line.IndexOf(';', StringComparison.Ordinal))?.Expression);
    }
}
