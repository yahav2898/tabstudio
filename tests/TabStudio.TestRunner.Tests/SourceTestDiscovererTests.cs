using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.VisualBasic;
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

    private const string VisualBasicRootNamespace = "MyRoot";

    private const string VisualBasicXunitSource = """"
        Imports Xunit

        Namespace Calculator.Tests

            Public MustInherit Class CalculatorTestsBase
                <Fact>
                Public Sub InheritedFact()
                End Sub
            End Class

            Public Class CalculatorTests
                Inherits CalculatorTestsBase

                <Fact>
                Public Sub Adds()
                End Sub

                <Theory>
                <InlineData(1, "one")>
                <InlineData(2, "say ""two""", Skip:="flaky")>
                Public Sub Names(number As Integer, name As String)
                End Sub

                <Theory>
                <InlineData(-4, True, Nothing)>
                <InlineData(GetType(String), DayOfWeek.Monday, 2.5)>
                Public Sub Values(value As Object, flag As Object, other As Object)
                End Sub

                <Theory>
                <MemberData(NameOf(Rows))>
                Public Sub FromMembers(number As Integer)
                End Sub

                Public Shared ReadOnly Property Rows As IEnumerable(Of Object())
                    Get
                        Return New List(Of Object())()
                    End Get
                End Property

                Public Class Nested
                    <Fact(DisplayName:="Nested fact", Skip:="later")>
                    Public Sub Inner()
                    End Sub
                End Class
            End Class

            Public Module ModuleTests
                <fact>
                Sub InModule()
                End Sub
            End Module

        End Namespace
        """";

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

    [Fact]
    public void Finds_visual_basic_xunit_tests_with_the_semantic_model_and_its_root_namespace()
    {
        var classes = SourceTestDiscoverer.Discover(VisualBasicXunitCompilation());

        var calculator = Assert.Single(classes, c => c.Name == "CalculatorTests");
        Assert.Equal("MyRoot.Calculator.Tests.CalculatorTests", calculator.FullName);
        Assert.Equal("MyRoot.Calculator.Tests", calculator.NamespaceName);
        Assert.Equal(TestFramework.XUnit, calculator.Framework);
        Assert.Equal(UserProject.LineOf(VisualBasicXunitSource, "Public Class CalculatorTests"), calculator.Location.Line);

        var adds = Assert.Single(calculator.Methods, m => m.MethodName == "Adds");
        Assert.Equal("MyRoot.Calculator.Tests.CalculatorTests.Adds", adds.FullyQualifiedName);
        Assert.Equal(UserProject.VisualBasicFilePath, adds.Location.FilePath);
        Assert.Equal(UserProject.LineOf(VisualBasicXunitSource, "Public Sub Adds()"), adds.Location.Line);
        Assert.Equal(UserProject.ColumnOf(VisualBasicXunitSource, "Adds()"), adds.Location.Column);
        Assert.Equal("Adds".Length, adds.SpanLength);

        var names = Assert.Single(calculator.Methods, m => m.MethodName == "Names");
        Assert.Equal(
            new[] { "MyRoot.Calculator.Tests.CalculatorTests.Names(1, \"one\")", "MyRoot.Calculator.Tests.CalculatorTests.Names(2, \"say \\\"two\\\"\")" },
            names.DataRows.Select(r => r.Id));
        Assert.Equal("Names(number: 1, name: \"one\")", names.DataRows[0].DisplayName);
        Assert.Equal(UserProject.LineOf(VisualBasicXunitSource, "<InlineData(2, "), names.DataRows[1].Location.Line);
        Assert.Equal("flaky", names.DataRows[1].SkipReason);

        Assert.Equal(
            new[] { "(-4, true, null)", "(typeof(String), Monday, 2.5)" },
            Assert.Single(calculator.Methods, m => m.MethodName == "Values").DataRows.Select(r => r.Arguments));
        Assert.True(Assert.Single(calculator.Methods, m => m.MethodName == "FromMembers").HasDynamicData);
        Assert.True(Assert.Single(calculator.Methods, m => m.MethodName == "InheritedFact").IsInherited);

        var inner = Assert.Single(Assert.Single(classes, c => c.FullName == "MyRoot.Calculator.Tests.CalculatorTests+Nested").Methods);
        Assert.Equal("Nested fact", inner.DisplayName);
        Assert.Equal("later", inner.SkipReason);
        Assert.Equal("MyRoot.Calculator.Tests.ModuleTests.InModule", Assert.Single(Assert.Single(classes, c => c.Name == "ModuleTests").Methods).FullyQualifiedName);
        Assert.DoesNotContain(classes, c => c.Name == "CalculatorTestsBase");
    }

    [Fact]
    public void Visual_basic_source_names_match_the_tests_the_runner_finds()
    {
        var fromModel = SourceTestDiscoverer.Discover(VisualBasicXunitCompilation());
        var tree = VisualBasicSyntaxTree.ParseText(VisualBasicXunitSource, path: UserProject.VisualBasicFilePath);
        var fromSyntax = SourceTestDiscoverer.Discover(tree, VisualBasicRootNamespace);
        using var session = UserProject.LoadVisualBasic(VisualBasicXunitSource, UserProject.XunitAssemblies, VisualBasicRootNamespace);

        var runnerIds = session.Tests
            .Where(t => t.MethodName != "FromMembers")
            .Select(t => t.Id)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Contains("MyRoot.Calculator.Tests.ModuleTests.InModule", runnerIds);
        Assert.Equal(runnerIds, SourceIds(fromModel));
        Assert.Equal(runnerIds.Where(id => !id.EndsWith(".InheritedFact", StringComparison.Ordinal)), SourceIds(fromSyntax));
    }

    [Fact]
    public void Finds_visual_basic_nunit_and_mstest_tests_from_syntax_alone()
    {
        const string source = """
            Imports NUnit.Framework
            Imports Microsoft.VisualStudio.TestTools.UnitTesting

            Namespace Global.Mixed.Tests
                <TestFixture, Category("Slow")>
                Public Class NUnitTests
                    <Test>
                    Public Sub Plain()
                    End Sub

                    <TestCase(1, 2, ExpectedResult:=3, TestName:="One plus two")>
                    <TestCase(-4, 4, ExpectedResult:=0)>
                    Public Function Adds(a As Integer, b As Integer) As Integer
                        Return a + b
                    End Function

                    <Test, Ignore("not yet")>
                    Public Sub Ignored()
                    End Sub
                End Class

                <TestClass>
                Public Class MSTestTests
                    <TestMethod>
                    Public Sub Plain()
                    End Sub

                    <DataTestMethod>
                    <DataRow(1, "a")>
                    <DynamicData(NameOf(Rows))>
                    Public Sub Rows(number As Integer, text As String)
                    End Sub

                    <TestMethod>
                    Public Shared Sub NotRunByMSTest()
                    End Sub
                End Class

                Public Class NotTests
                    <TestMethod>
                    Public Sub NoTestClass()
                    End Sub
                End Class
            End Namespace
            """;

        var classes = SourceTestDiscoverer.Discover(VisualBasicSyntaxTree.ParseText(source, path: UserProject.VisualBasicFilePath), VisualBasicRootNamespace);

        Assert.Equal(2, classes.Count);
        var nunit = Assert.Single(classes, c => c.Framework == TestFramework.NUnit);
        Assert.Equal("Mixed.Tests.NUnitTests", nunit.FullName);
        Assert.Equal(3, nunit.Methods.Count);
        var adds = Assert.Single(nunit.Methods, m => m.MethodName == "Adds");
        Assert.Equal(new[] { "One plus two", "Adds(-4,4)" }, adds.DataRows.Select(r => r.DisplayName));
        Assert.Equal("Mixed.Tests.NUnitTests.Adds(-4, 4)", adds.DataRows[1].Id);
        Assert.Contains(new TestTrait("Category", "Slow"), adds.Traits);
        var ignored = Assert.Single(nunit.Methods, m => m.MethodName == "Ignored");
        Assert.Equal("not yet", ignored.SkipReason);
        Assert.Equal(UserProject.LineOf(source, "Public Sub Ignored()"), ignored.Location.Line);

        var mstest = Assert.Single(classes, c => c.Framework == TestFramework.MSTest);
        Assert.Equal(new[] { "Plain", "Rows" }, mstest.Methods.Select(m => m.MethodName));
        var rows = Assert.Single(mstest.Methods, m => m.MethodName == "Rows");
        Assert.True(rows.HasDynamicData);
        Assert.Equal("Rows (1,\"a\")", Assert.Single(rows.DataRows).DisplayName);
    }

    [Fact]
    public async Task Runs_nunit_tests_of_a_visual_basic_module()
    {
        const string source = """
            Imports NUnit.Framework

            Public Module ShopTests
                <Test>
                Public Sub Passes()
                    If 1 + 1 <> 2 Then Throw New InvalidOperationException("arithmetic")
                End Sub

                <TestCase(2, 3, ExpectedResult:=5)>
                Public Function Adds(a As Integer, b As Integer) As Integer
                    Return a + b
                End Function
            End Module
            """;

        using var session = UserProject.LoadVisualBasic(source, UserProject.NUnitAssemblies, "Shop");
        var summary = await session.RunAllAsync();

        Assert.Equal(new[] { "Shop.ShopTests.Adds(2, 3)", "Shop.ShopTests.Passes" }, session.Tests.Select(t => t.Id).Order(StringComparer.Ordinal));
        Assert.All(summary.Results, r => Assert.True(r.Outcome == TestOutcome.Passed, $"{r.Test.DisplayName}: {r.Message}"));
        Assert.Equal(2, summary.Passed);
    }

    private static List<string> SourceIds(IReadOnlyList<SourceTestClass> classes) =>
        classes
            .SelectMany(c => c.Methods)
            .SelectMany(m => m.DataRows.Count > 0 ? m.DataRows.Select(r => r.Id) : m.HasDynamicData ? Enumerable.Empty<string>() : new[] { m.FullyQualifiedName })
            .Order(StringComparer.Ordinal)
            .ToList();

    private static VisualBasicCompilation VisualBasicXunitCompilation() =>
        UserProject.CreateVisualBasicCompilation(VisualBasicXunitSource, UserProject.XunitAssemblies, VisualBasicRootNamespace);

    private static CSharpCompilation XunitCompilation() =>
        new CSharpCompiler().CreateCompilation(UserProject.Request(XunitSource, UserProject.XunitAssemblies));
}
