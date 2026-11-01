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
    public async Task XUnit_v3_theories_get_their_rows_from_v3_data_attributes()
    {
        // xUnit.net v3's data attributes derive from Xunit.v3.DataAttribute, not v2's Xunit.Sdk.DataAttribute, so every
        // v3 [Theory] said "No data found". The v3 types are declared here as the v3 assemblies declare them.
        var source = """
            using System;
            using System.Collections.Generic;
            using System.Reflection;
            using System.Threading.Tasks;

            namespace Xunit
            {
                [AttributeUsage(AttributeTargets.Method)] public class FactAttribute : Attribute { public string? Skip { get; set; } }
                [AttributeUsage(AttributeTargets.Method)] public class TheoryAttribute : FactAttribute { }
                public interface ITheoryDataRow { object?[] GetData(); }
                public sealed class TheoryDataRow(params object?[] data) : ITheoryDataRow { public object?[] GetData() => data; }
                public sealed class InlineDataAttribute(params object?[] data) : v3.DataAttribute
                {
                    public override ValueTask<IReadOnlyCollection<ITheoryDataRow>> GetData(MethodInfo testMethod, v3.DisposalTracker disposalTracker) =>
                        new([new TheoryDataRow(data)]);
                }
                public sealed class MemberDataAttribute(string memberName, params object?[] arguments) : v3.MemberDataAttributeBase(memberName, arguments) { }
            }

            namespace Xunit.v3
            {
                public sealed class DisposalTracker { }
                [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
                public abstract class DataAttribute : Attribute
                {
                    public string? Skip { get; set; }
                    public abstract ValueTask<IReadOnlyCollection<ITheoryDataRow>> GetData(MethodInfo testMethod, DisposalTracker disposalTracker);
                }
                public abstract class MemberDataAttributeBase(string memberName, object?[] arguments) : DataAttribute
                {
                    public string MemberName { get; } = memberName;
                    public object?[] Arguments { get; } = arguments;
                    public Type? MemberType { get; set; }
                    public override ValueTask<IReadOnlyCollection<ITheoryDataRow>> GetData(MethodInfo testMethod, DisposalTracker disposalTracker) =>
                        throw new NotSupportedException("The runner reads the member itself.");
                }
            }

            namespace V3.Tests
            {
                using Xunit;
                using Xunit.v3;

                public sealed class EvenAttribute : DataAttribute
                {
                    public override ValueTask<IReadOnlyCollection<ITheoryDataRow>> GetData(MethodInfo testMethod, DisposalTracker disposalTracker) =>
                        new([new TheoryDataRow(2), new TheoryDataRow(4)]);
                }

                public class Theories
                {
                    public static IEnumerable<object[]> Words => [["tablet", 6], ["pen", 3]];

                    [Theory, InlineData(1, 2, 3), InlineData(2, 2, 4)]
                    public void Adds(int a, int b, int sum) { if (a + b != sum) throw new Exception("wrong sum"); }

                    [Theory, MemberData(nameof(Words))]
                    public void CountsLetters(string word, int length) { if (word.Length != length) throw new Exception("wrong length"); }

                    [Theory, Even]
                    public void IsEven(int value) { if (value % 2 != 0) throw new Exception("odd"); }
                }
            }
            """;

        var summary = await UserProject.RunAllAsync(source, []);

        Assert.Equal(6, summary.Results.Count);
        Assert.All(summary.Results, r => Assert.Equal(TestOutcome.Passed, r.Outcome));
        Assert.DoesNotContain(summary.Results, r => r.Message?.Contains("No data found", StringComparison.Ordinal) == true);
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
