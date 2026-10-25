namespace TabStudio.TestRunner.Tests;

public sealed class XunitDataAndFixtureTests
{
    [Fact]
    public async Task Collection_fixtures_are_shared_by_every_class_of_the_collection()
    {
        var source = """
            using Xunit;

            namespace Shared.Tests;

            public sealed class Database : IDisposable
            {
                public static int Created;

                public Database() => Created++;

                public void Dispose() { }
            }

            [CollectionDefinition("Database")]
            public class DatabaseCollection : ICollectionFixture<Database> { }

            [Collection("Database")]
            public class First
            {
                private readonly Database _database;

                public First(Database database) => _database = database;

                [Fact]
                public void UsesTheSharedFixture() => Assert.Equal(1, Database.Created);
            }

            [Collection("Database")]
            public class Second
            {
                public Second(Database database) => Assert.NotNull(database);

                [Fact]
                public void UsesTheSameFixture() => Assert.Equal(1, Database.Created);
            }

            public class MissingFixture
            {
                public MissingFixture(Database database) { }

                [Fact]
                public void CannotBeCreated() { }
            }
            """;

        var summary = await UserProject.RunAllAsync(source, UserProject.XunitAssemblies);

        Assert.Equal(TestOutcome.Passed, summary.Result("UsesTheSharedFixture").Outcome);
        Assert.Equal(TestOutcome.Passed, summary.Result("UsesTheSameFixture").Outcome);
        var missing = summary.Result("CannotBeCreated");
        Assert.Equal(TestOutcome.Errored, missing.Outcome);
        Assert.Contains("did not have matching fixture data: Database database", missing.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Theories_take_member_types_value_tasks_params_and_generic_arguments()
    {
        var source = """
            using Xunit;

            namespace Data.Tests;

            public static class SharedData
            {
                public static TheoryData<string, int> Words => new() { { "tablet", 6 }, { "pen", 3 } };
            }

            public class DataTests
            {
                [Theory]
                [MemberData(nameof(SharedData.Words), MemberType = typeof(SharedData))]
                public void CountsLetters(string word, int length) => Assert.Equal(length, word.Length);

                [Theory]
                [InlineData(11, 2, 3, 6)]
                [InlineData(5)]
                public void SumsParams(int expected, params int[] values) => Assert.Equal(expected, values.Length == 0 ? 5 : values.Sum());

                [Theory]
                [InlineData(42)]
                [InlineData("text")]
                public void InfersGenericArguments<T>(T value) => Assert.IsType<T>(value);

                [Theory]
                [InlineData(2)]
                public async ValueTask AwaitsValueTasks(int delay)
                {
                    await Task.Delay(delay);
                    Assert.True(delay > 0);
                }

                [Theory]
                [InlineData(1, 2)]
                public void RejectsExtraValues(int only) { }
            }
            """;

        var summary = await UserProject.RunAllAsync(source, UserProject.XunitAssemblies);

        Assert.Equal(TestOutcome.Passed, summary.Result("CountsLetters(word: \"tablet\", length: 6)").Outcome);
        Assert.Equal(TestOutcome.Passed, summary.Result("CountsLetters(word: \"pen\", length: 3)").Outcome);
        Assert.Equal(TestOutcome.Passed, summary.Result("SumsParams(expected: 11, values: 2, ???: 3, ???: 6)").Outcome);
        Assert.Equal(TestOutcome.Passed, summary.Result("SumsParams(expected: 5)").Outcome);
        Assert.Equal(TestOutcome.Passed, summary.Result("InfersGenericArguments(value: 42)").Outcome);
        Assert.Equal(TestOutcome.Passed, summary.Result("InfersGenericArguments(value: \"text\")").Outcome);
        Assert.Equal(TestOutcome.Passed, summary.Result("AwaitsValueTasks(delay: 2)").Outcome);

        var extra = summary.Result("RejectsExtraValues(only: 1, ???: 2)");
        Assert.Equal(TestOutcome.Errored, extra.Outcome);
        Assert.Contains("expected 1 parameter value, but 2 parameter values were provided", extra.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_data_source_that_throws_is_reported_on_its_theory()
    {
        var source = """
            using Xunit;

            namespace Broken.Tests;

            public class BrokenData
            {
                public static IEnumerable<object[]> Rows => throw new InvalidOperationException("no rows today");

                [Theory]
                [MemberData(nameof(Rows))]
                public void NeverRuns(int value) { }
            }
            """;

        var summary = await UserProject.RunAllAsync(source, UserProject.XunitAssemblies);

        var result = Assert.Single(summary.Results);
        Assert.Equal(TestOutcome.Errored, result.Outcome);
        Assert.Equal("[MemberDataAttribute] could not provide data: System.InvalidOperationException: no rows today", result.Message);
    }
}
