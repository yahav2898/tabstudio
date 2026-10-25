using System.Runtime.Loader;

namespace TabStudio.TestRunner.Tests;

public sealed class SharedRuntimeAssembliesTests
{
    private const string Source = """
        using Xunit;
        using Xunit.Abstractions;

        namespace Shared.Tests;

        public class OutputTests
        {
            private readonly ITestOutputHelper _output;

            public OutputTests(ITestOutputHelper output) => _output = output;

            [Fact]
            public void Writes()
            {
                _output.WriteLine("shared hello");
                Assert.Equal(4, 2 + 2);
            }
        }
        """;

    [Fact]
    public async Task Sessions_that_share_runtime_assemblies_load_the_framework_once()
    {
        var options = new TestRunOptions { ShareRuntimeAssemblies = true };
        using var first = UserProject.Load(Source, UserProject.XunitAssemblies, options);
        using var second = UserProject.Load(Source, UserProject.XunitAssemblies, options);

        var firstRun = await first.RunAllAsync();
        var secondRun = await second.RunAllAsync();

        AssertPassed(firstRun.Result("Writes"));
        AssertPassed(secondRun.Result("Writes"));
        Assert.Contains("shared hello", secondRun.Result("Writes").Output, StringComparison.Ordinal);
        var withXunit = AssemblyLoadContext.All.Count(c => c.Name == "TabStudio test packages" && c.Assemblies.Any(a => a.GetName().Name == "xunit.core"));
        Assert.Equal(1, withXunit);
    }

    [Fact]
    public async Task Sessions_with_their_own_copy_of_xunit_each_get_an_output_helper_of_their_own()
    {
        // Each session loads its own xunit.abstractions: the second's ITestOutputHelper is another type than the first's.
        using var first = UserProject.Load(Source, UserProject.XunitAssemblies);
        using var second = UserProject.Load(Source, UserProject.XunitAssemblies);

        var firstRun = await first.RunAllAsync();
        var secondRun = await second.RunAllAsync();

        AssertPassed(firstRun.Result("Writes"));
        AssertPassed(secondRun.Result("Writes"));
        Assert.Contains("shared hello", secondRun.Result("Writes").Output, StringComparison.Ordinal);
    }

    private static void AssertPassed(TestResult result) =>
        Assert.True(result.Outcome == TestOutcome.Passed, $"{result.Outcome}: {result.ExceptionType}: {result.Message}\n{result.StackTrace}");
}
