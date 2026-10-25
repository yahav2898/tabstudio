namespace TabStudio.TestRunner.Tests;

public sealed class NUnitRunnerTests
{
    private const string Source = """
        using NUnit.Framework;

        namespace Shop.Tests;

        public abstract class TestBase
        {
            [SetUp]
            public void BaseSetUp() => Console.WriteLine("base setup");

            [TearDown]
            public void BaseTearDown() => Console.WriteLine("base teardown");
        }

        [TestFixture]
        [Category("Shop")]
        public class CartTests : TestBase
        {
            private static int _fixtures;
            private int _oneTimeSetUps;

            public CartTests() => _fixtures++;

            [OneTimeSetUp]
            public void OneTimeSetUp() => _oneTimeSetUps++;

            [SetUp]
            public void SetUp() => Console.WriteLine("setup");

            [TearDown]
            public void TearDown() => Console.WriteLine("teardown");

            [Test]
            public void SharesOneFixture()
            {
                Console.WriteLine("test");
                Assert.That(_fixtures, Is.EqualTo(1));
                Assert.That(_oneTimeSetUps, Is.EqualTo(1));
            }

            [TestCase(1, 2, ExpectedResult = 3)]
            [TestCase(2, 2, ExpectedResult = 5)]
            public int Adds(int a, int b) => a + b;

            [TestCase(10.0, ExpectedResult = 5)]
            public double Halves(decimal value) => (double)(value / 2);

            [TestCase(2, ExpectedResult = 4)]
            public async Task<int> DoublesAsync(int value)
            {
                await Task.Delay(10);
                return value * 2;
            }

            [TestCaseSource(nameof(Prices))]
            public void PricesArePositive(decimal price) => Assert.That(price, Is.GreaterThan(0));

            public static IEnumerable<TestCaseData> Prices()
            {
                yield return new TestCaseData(1.5m).SetName("Cheap");
                yield return new TestCaseData(-2m);
            }

            [Test]
            public async Task PassesAfterAwait()
            {
                await Task.Delay(10);
                Assert.Pass("done");
            }

            [Test]
            public void IgnoredAtRunTime() => Assert.Ignore("not today");

            [Test, Ignore("broken")]
            public void IgnoredByAttribute() { }

            [Test]
            public void FailsAnAssertion() => Assert.That(1 + 1, Is.EqualTo(3));

            [Test, Explicit]
            public void OnlyOnRequest() { }
        }
        """;

    [Fact]
    public void Discovers_tests_test_cases_and_sources()
    {
        using var session = UserProject.Load(Source, UserProject.NUnitAssemblies);

        Assert.Equal(13, session.Tests.Count);
        Assert.All(session.Tests, t => Assert.Equal(TestFramework.NUnit, t.Framework));
        Assert.Equal("Shop.Tests.CartTests.Adds(1, 2)", session.Tests.First(t => t.MethodName == "Adds").Id);
        Assert.Equal("Adds(1,2)", session.Tests.First(t => t.MethodName == "Adds").DisplayName);
        Assert.Contains(session.Tests, t => t.DisplayName == "Cheap");
        Assert.Contains(new TestTrait("Category", "Shop"), session.Tests.First(t => t.MethodName == "SharesOneFixture").Traits);
        Assert.True(Assert.Single(session.Tests, t => t.MethodName == "OnlyOnRequest").IsExplicit);
    }

    [Fact]
    public async Task Runs_tests_with_expected_results_and_framework_outcomes()
    {
        using var session = UserProject.Load(Source, UserProject.NUnitAssemblies);
        var summary = await session.RunAllAsync();

        Assert.Equal(12, summary.Total);
        Assert.Equal(TestOutcome.Passed, summary.Result("Adds(1,2)").Outcome);
        var wrongResult = summary.Result("Adds(2,2)");
        Assert.Equal(TestOutcome.Failed, wrongResult.Outcome);
        Assert.Equal("  Expected: 5\n  But was:  4", wrongResult.Message);
        Assert.Equal(TestOutcome.Passed, summary.Result("Halves(10)").Outcome);
        Assert.Equal(TestOutcome.Passed, summary.Result("DoublesAsync(2)").Outcome);
        Assert.Equal(TestOutcome.Passed, summary.Result("Cheap").Outcome);
        Assert.Equal(TestOutcome.Failed, summary.Result("PricesArePositive(-2)").Outcome);
        Assert.Equal(TestOutcome.Passed, summary.Result("PassesAfterAwait").Outcome);
        Assert.Equal(TestOutcome.Skipped, summary.Result("IgnoredAtRunTime").Outcome);
        Assert.Equal("not today", summary.Result("IgnoredAtRunTime").Message);
        Assert.Equal("broken", summary.Result("IgnoredByAttribute").Message);

        var failed = summary.Result("FailsAnAssertion");
        Assert.Equal(TestOutcome.Failed, failed.Outcome);
        Assert.Contains("Expected: 3", failed.Message, StringComparison.Ordinal);
        Assert.Equal(UserProject.LineOf(Source, "Assert.That(1 + 1, Is.EqualTo(3))"), failed.FailureLocation?.Line);

        var explicitRun = await session.RunSelectedAsync(["Shop.Tests.CartTests.OnlyOnRequest"]);
        Assert.Equal(TestOutcome.Passed, Assert.Single(explicitRun.Results).Outcome);
    }

    [Fact]
    public async Task Setups_and_teardowns_run_around_each_test_base_class_first()
    {
        var summary = await UserProject.RunAllAsync(Source, UserProject.NUnitAssemblies);

        var test = summary.Result("SharesOneFixture");
        Assert.Equal(TestOutcome.Passed, test.Outcome);
        Assert.Equal("base setup\nsetup\ntest\nteardown\nbase teardown\n", test.Output.ReplaceLineEndings("\n"));
    }

    [Fact]
    public async Task A_failing_one_time_setup_errors_every_test_of_the_fixture()
    {
        var source = """
            using NUnit.Framework;

            namespace Broken.Tests;

            public class BrokenFixture
            {
                [OneTimeSetUp]
                public void Start() => throw new InvalidOperationException("no database");

                [Test]
                public void First() { }

                [Test]
                public void Second() { }
            }
            """;

        var summary = await UserProject.RunAllAsync(source, UserProject.NUnitAssemblies);

        Assert.Equal(2, summary.Errored);
        Assert.All(summary.Results, r => Assert.Equal("OneTimeSetUp failed: System.InvalidOperationException: no database", r.Message));
    }
}
