namespace TabStudio.TestRunner.Tests;

public sealed class MSTestRunnerTests
{
    private const string Source = """
        using Microsoft.VisualStudio.TestTools.UnitTesting;

        namespace Billing.Tests;

        [TestClass]
        public class InvoiceTests
        {
            private static int _classInitializations;

            [ClassInitialize]
            public static void ClassInit(TestContext context) => _classInitializations++;

            [TestInitialize]
            public void Init() => Console.WriteLine("init");

            [TestCleanup]
            public void Cleanup() => Console.WriteLine("cleanup");

            [TestMethod]
            [TestCategory("Billing")]
            public void Passes()
            {
                Console.WriteLine("test");
                Assert.AreEqual(1, _classInitializations);
            }

            [TestMethod]
            public void Fails() => Assert.AreEqual(2, 1 + 2);

            [DataTestMethod]
            [DataRow(1, 2, 3)]
            [DataRow(2, 2, 4, DisplayName = "Two and two")]
            public void Adds(int a, int b, int sum) => Assert.AreEqual(sum, a + b);

            public static IEnumerable<object[]> Totals => new[] { new object[] { 10, 2, 12 } };

            [TestMethod]
            [DynamicData(nameof(Totals))]
            public void AddsTotals(int a, int b, int sum) => Assert.AreEqual(sum, a + b);

            [TestMethod, Ignore("later")]
            public void Ignored() { }

            [TestMethod]
            public void Inconclusive() => Assert.Inconclusive("unknown");

            [TestMethod]
            [ExpectedException(typeof(InvalidOperationException))]
            public void ThrowsExpected() => throw new InvalidOperationException();

            [TestMethod]
            [ExpectedException(typeof(InvalidOperationException))]
            public void DoesNotThrow() { }
        }
        """;

    [Fact]
    public void Discovers_test_methods_data_rows_and_dynamic_data()
    {
        using var session = UserProject.Load(Source, UserProject.MSTestAssemblies);

        Assert.Equal(9, session.Tests.Count);
        Assert.All(session.Tests, t => Assert.Equal(TestFramework.MSTest, t.Framework));
        Assert.Equal(
            new[] { "Adds (1,2,3)", "Two and two" },
            session.Tests.Where(t => t.MethodName == "Adds").Select(t => t.DisplayName));
        Assert.Equal("Billing.Tests.InvoiceTests.AddsTotals(10, 2, 12)", Assert.Single(session.Tests, t => t.MethodName == "AddsTotals").Id);
        Assert.Equal(new TestTrait("Category", "Billing"), Assert.Single(Assert.Single(session.Tests, t => t.MethodName == "Passes").Traits));
    }

    [Fact]
    public async Task Runs_tests_with_initializers_and_mstest_outcomes()
    {
        var summary = await UserProject.RunAllAsync(Source, UserProject.MSTestAssemblies);

        var passes = summary.Result("Passes");
        Assert.Equal(TestOutcome.Passed, passes.Outcome);
        Assert.Equal("init\ntest\ncleanup\n", passes.Output.ReplaceLineEndings("\n"));

        var fails = summary.Result("Fails");
        Assert.Equal(TestOutcome.Failed, fails.Outcome);
        Assert.Contains("Assert.AreEqual failed", fails.Message, StringComparison.Ordinal);
        Assert.Equal(UserProject.LineOf(Source, "Assert.AreEqual(2, 1 + 2)"), fails.FailureLocation?.Line);

        Assert.Equal(TestOutcome.Passed, summary.Result("Adds (1,2,3)").Outcome);
        Assert.Equal(TestOutcome.Passed, summary.Result("Two and two").Outcome);
        Assert.Equal(TestOutcome.Passed, summary.Result("AddsTotals (10,2,12)").Outcome);
        Assert.Equal("later", summary.Result("Ignored").Message);
        Assert.Equal(TestOutcome.Skipped, summary.Result("Inconclusive").Outcome);
        Assert.Equal(TestOutcome.Passed, summary.Result("ThrowsExpected").Outcome);
        Assert.Equal(TestOutcome.Failed, summary.Result("DoesNotThrow").Outcome);
        Assert.Equal(5, summary.Passed);
    }
}
