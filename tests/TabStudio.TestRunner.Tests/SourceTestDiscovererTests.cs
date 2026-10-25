using Microsoft.CodeAnalysis.CSharp;
using TabStudio.Compilation;

namespace TabStudio.TestRunner.Tests;

public sealed class SourceTestDiscovererTests
{
    private const string XunitSource = """
        using Xunit;

        namespace Calculator.Tests;

        public abstract class CalculatorTestsBase
        {
            [Fact]
            public void InheritedFact() { }
        }

        public class CalculatorTests : CalculatorTestsBase
        {
            [Fact]
            public void Adds() { }

            [Theory]
            [InlineData(1, "one")]
            [InlineData(2, "two", Skip = "flaky")]
            public void Names(int number, string name) { }

            [Theory]
            [MemberData(nameof(Rows))]
            public void FromMembers(int number) { }

            public static IEnumerable<object[]> Rows => [];

            public class Nested
            {
                [Fact(DisplayName = "Nested fact", Skip = "later")]
                public void Inner() { }
            }
        }
        """;

    [Fact]
    public void Finds_xunit_tests_with_the_semantic_model_at_the_right_positions()
    {
        var classes = SourceTestDiscoverer.Discover(XunitCompilation());

        var calculator = Assert.Single(classes, c => c.Name == "CalculatorTests");
        Assert.Equal("Calculator.Tests.CalculatorTests", calculator.FullName);
        Assert.Equal("Calculator.Tests", calculator.NamespaceName);
        Assert.Equal(TestFramework.XUnit, calculator.Framework);
        Assert.Equal(UserProject.LineOf(XunitSource, "CalculatorTests : CalculatorTestsBase"), calculator.Location.Line);

        var adds = Assert.Single(calculator.Methods, m => m.MethodName == "Adds");
        Assert.Equal("Calculator.Tests.CalculatorTests.Adds", adds.FullyQualifiedName);
        Assert.Equal(UserProject.FilePath, adds.Location.FilePath);
        Assert.Equal(UserProject.LineOf(XunitSource, "public void Adds()"), adds.Location.Line);
        Assert.Equal(UserProject.ColumnOf(XunitSource, "Adds()"), adds.Location.Column);
        Assert.Equal("Adds".Length, adds.SpanLength);
        Assert.False(adds.IsParameterized);

        var names = Assert.Single(calculator.Methods, m => m.MethodName == "Names");
        Assert.True(names.IsParameterized);
        Assert.False(names.HasDynamicData);
        Assert.Equal(
            new[] { "Calculator.Tests.CalculatorTests.Names(1, \"one\")", "Calculator.Tests.CalculatorTests.Names(2, \"two\")" },
            names.DataRows.Select(r => r.Id));
        Assert.Equal("Names(number: 1, name: \"one\")", names.DataRows[0].DisplayName);
        Assert.Equal(UserProject.LineOf(XunitSource, "[InlineData(2, \"two\""), names.DataRows[1].Location.Line);
        Assert.Equal("flaky", names.DataRows[1].SkipReason);

        Assert.True(Assert.Single(calculator.Methods, m => m.MethodName == "FromMembers").HasDynamicData);

        var inherited = Assert.Single(calculator.Methods, m => m.MethodName == "InheritedFact");
        Assert.True(inherited.IsInherited);
        Assert.Equal("Calculator.Tests.CalculatorTests.InheritedFact", inherited.FullyQualifiedName);
        Assert.Equal(UserProject.LineOf(XunitSource, "public void InheritedFact()"), inherited.Location.Line);

        Assert.DoesNotContain(classes, c => c.Name == "CalculatorTestsBase");
        var nested = Assert.Single(classes, c => c.FullName == "Calculator.Tests.CalculatorTests+Nested");
        var inner = Assert.Single(nested.Methods);
        Assert.Equal("Nested fact", inner.DisplayName);
        Assert.Equal("later", inner.SkipReason);
    }

    [Fact]
    public void Source_names_match_the_tests_the_runner_finds()
    {
        var classes = SourceTestDiscoverer.Discover(XunitCompilation());
        using var session = UserProject.Load(XunitSource, UserProject.XunitAssemblies);

        var sourceIds = classes
            .SelectMany(c => c.Methods)
            .SelectMany(m => m.DataRows.Count > 0 ? m.DataRows.Select(r => r.Id) : m.HasDynamicData ? Enumerable.Empty<string>() : new[] { m.FullyQualifiedName })
            .Order(StringComparer.Ordinal);
        var runnerIds = session.Tests
            .Where(t => t.MethodName != "FromMembers")
            .Select(t => t.Id)
            .Order(StringComparer.Ordinal);

        Assert.Equal(runnerIds, sourceIds);
    }

    [Fact]
    public void Finds_nunit_and_mstest_tests_from_syntax_alone()
    {
        var source = """
            using NUnit.Framework;
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            namespace Mixed.Tests
            {
                [TestFixture, Category("Slow")]
                public class NUnitTests
                {
                    [Test]
                    public void Plain() { }

                    [TestCase(1, 2, ExpectedResult = 3, TestName = "One plus two")]
                    [TestCase(-4, 4, ExpectedResult = 0)]
                    public int Adds(int a, int b) => a + b;

                    [Test, Ignore("not yet")]
                    public void Ignored() { }
                }

                [TestClass]
                public class MSTestTests
                {
                    [TestMethod]
                    public void Plain() { }

                    [DataTestMethod]
                    [DataRow(1, "a")]
                    [DynamicData(nameof(Rows))]
                    public void Rows(int number, string text) { }
                }

                public class NotTests
                {
                    [TestMethod]
                    public void NoTestClass() { }
                }
            }
            """;

        var classes = SourceTestDiscoverer.Discover(CSharpSyntaxTree.ParseText(source, path: UserProject.FilePath));

        Assert.Equal(2, classes.Count);
        var nunit = Assert.Single(classes, c => c.Framework == TestFramework.NUnit);
        Assert.Equal("Mixed.Tests.NUnitTests", nunit.FullName);
        Assert.Equal(3, nunit.Methods.Count);
        var adds = Assert.Single(nunit.Methods, m => m.MethodName == "Adds");
        Assert.Equal(new[] { "One plus two", "Adds(-4,4)" }, adds.DataRows.Select(r => r.DisplayName));
        Assert.Equal("Mixed.Tests.NUnitTests.Adds(-4, 4)", adds.DataRows[1].Id);
        Assert.Contains(new TestTrait("Category", "Slow"), adds.Traits);
        Assert.Equal("not yet", Assert.Single(nunit.Methods, m => m.MethodName == "Ignored").SkipReason);
        Assert.Equal(UserProject.LineOf(source, "public void Ignored()"), Assert.Single(nunit.Methods, m => m.MethodName == "Ignored").Location.Line);

        var mstest = Assert.Single(classes, c => c.Framework == TestFramework.MSTest);
        var rows = Assert.Single(mstest.Methods, m => m.MethodName == "Rows");
        Assert.True(rows.HasDynamicData);
        Assert.Equal("Rows (1,\"a\")", Assert.Single(rows.DataRows).DisplayName);
    }

    private static CSharpCompilation XunitCompilation() =>
        new CSharpCompiler().CreateCompilation(UserProject.Request(XunitSource, UserProject.XunitAssemblies));
}
