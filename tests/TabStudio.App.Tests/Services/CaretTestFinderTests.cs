using TabStudio.App.Services;

namespace TabStudio.App.Tests.Services;

public sealed class CaretTestFinderTests
{
    private const string FilePath = "/work/Calculator.Tests/MathTests.cs";

    private const string Source = """
        using Xunit;

        namespace Calculator.Tests;

        public class MathTests
        {
            [Fact]
            public void Adds()
            {
                Assert.Equal(4, 2 + 2);
            }

            [Theory]
            [InlineData(1, 2)]
            [InlineData(3, 4)]
            public void Orders(int low, int high) => Assert.True(low < high);

            private static int Helper() => 42;
        }

        public class OtherTests
        {
            [Fact]
            public void Works() { }
        }
        """;

    [Fact]
    public void Inside_a_test_method_runs_that_test()
    {
        var tests = Find("Assert.Equal(4");

        Assert.NotNull(tests);
        Assert.Equal(CaretTestScope.Test, tests.Scope);
        Assert.Equal(new[] { "Calculator.Tests.MathTests.Adds" }, tests.Names);
        Assert.Equal("Run Test 'Adds'", tests.MenuText);
    }

    [Fact]
    public void On_a_data_row_runs_that_row()
    {
        var tests = Find("InlineData(3, 4)");

        Assert.NotNull(tests);
        Assert.Equal(CaretTestScope.DataRow, tests.Scope);
        Assert.Equal(new[] { "Calculator.Tests.MathTests.Orders(3, 4)" }, tests.Names);
        Assert.Equal("Orders(low: 3, high: 4)", tests.Description);
    }

    [Fact]
    public void Elsewhere_in_a_test_class_runs_the_class()
    {
        var tests = Find("Helper()");

        Assert.NotNull(tests);
        Assert.Equal(CaretTestScope.Class, tests.Scope);
        Assert.Equal(new[] { "Calculator.Tests.MathTests" }, tests.Names);
        Assert.Equal("Run Tests in 'MathTests'", tests.MenuText);
    }

    [Fact]
    public void Between_two_tests_runs_the_class()
    {
        var offset = Source.IndexOf("[Theory]", StringComparison.Ordinal) - 2;

        var tests = CaretTestFinder.Find(Source, FilePath, offset);

        Assert.Equal(CaretTestScope.Class, tests?.Scope);
    }

    [Fact]
    public void Outside_the_test_classes_runs_every_test_in_the_file()
    {
        var tests = Find("using Xunit");

        Assert.NotNull(tests);
        Assert.Equal(CaretTestScope.File, tests.Scope);
        Assert.Equal(new[] { "Calculator.Tests.MathTests", "Calculator.Tests.OtherTests" }, tests.Names);
        Assert.Equal("MathTests.cs", tests.Description);
    }

    [Fact]
    public void A_file_without_tests_has_nothing_to_run()
    {
        Assert.Null(CaretTestFinder.Find("namespace Calculator; public static class Arithmetic { }", FilePath, 10));
        Assert.Null(CaretTestFinder.Find(string.Empty, FilePath, 0));
    }

    [Fact]
    public void Visual_basic_rows_tests_and_classes_are_found_with_the_root_namespace()
    {
        const string source = """
            Imports Xunit

            Public Class MathTests
                <Fact>
                Public Sub Adds()
                    Assert.Equal(4, 2 + 2)
                End Sub

                <Theory>
                <InlineData(1, 2)>
                <InlineData(3, 4)>
                Public Sub Orders(low As Integer, high As Integer)
                    Assert.True(low < high)
                End Sub

                Private Shared Function Helper() As Integer
                    Return 42
                End Function
            End Class
            """;
        CaretTests? At(string text) =>
            CaretTestFinder.Find(source, "/work/Calculator.Tests/MathTests.vb", source.IndexOf(text, StringComparison.Ordinal) + 1, "Calculator.Tests");

        Assert.Equal(new[] { "Calculator.Tests.MathTests.Adds" }, At("Assert.Equal(4")?.Names);
        Assert.Equal(new[] { "Calculator.Tests.MathTests.Orders(3, 4)" }, At("InlineData(3, 4)")?.Names);
        Assert.Equal("Orders(low: 3, high: 4)", At("InlineData(3, 4)")?.Description);
        Assert.Equal(CaretTestScope.Class, At("Return 42")?.Scope);
        Assert.Equal(new[] { "Calculator.Tests.MathTests" }, At("Imports Xunit")?.Names);
    }

    private static CaretTests? Find(string text) =>
        CaretTestFinder.Find(Source, FilePath, Source.IndexOf(text, StringComparison.Ordinal) + 1);
}
