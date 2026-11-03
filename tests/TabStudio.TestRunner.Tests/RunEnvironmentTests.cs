namespace TabStudio.TestRunner.Tests;

/// <summary>
/// What tests see of the process they run in, as under <c>dotnet test</c>: their assembly's file, the output folder
/// as the current directory and base directory, their own types by name, <c>async void</c> tests waited for, and
/// arguments converted as xUnit converts them. Each case comes from a pinned repository's tests that failed in
/// TabStudio and pass under <c>dotnet test</c>: markdig, Bogus and CsvHelper (the assembly's file), scriban (the
/// base directory), YamlDotNet (the current directory and <c>[TypeConverter]</c>), FluentValidation
/// (<c>async void</c> and <c>[InlineData(0)]</c> for a string).
/// </summary>
[Collection(RunEnvironmentGroup.Name)]
public sealed class RunEnvironmentTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "tabstudio-output-" + Guid.NewGuid().ToString("N")[..8], "bin", "Debug", "net10.0");

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path.GetFullPath(Path.Combine(_folder, "..", "..", "..")), recursive: true);
        }
        catch (IOException)
        {
            // Another test's files are still open; the temporary folder is cleaned up later.
        }
    }

    private const string EnvironmentSource = """
        using System;
        using System.ComponentModel;
        using System.IO;
        using System.Runtime.Loader;
        using Xunit;

        namespace Output.Tests;

        public class EnvironmentTests
        {
            private const string Folder = @"FOLDER";

            [Fact]
            public void AssemblyIsAFileInTheOutputFolder() =>
                Assert.Equal(Folder, Path.GetDirectoryName(typeof(EnvironmentTests).Assembly.Location));

            [Fact]
            public void BaseDirectoryIsTheOutputFolder() =>
                Assert.Equal(Folder + Path.DirectorySeparatorChar, AppContext.BaseDirectory);

            [Fact]
            public void CurrentDirectoryIsTheOutputFolder() =>
                Assert.Equal(Folder, Directory.GetCurrentDirectory());

            [Fact]
            public void FindsItsOwnTypesByName() =>
                Assert.Same(AssemblyLoadContext.GetLoadContext(typeof(EnvironmentTests).Assembly), AssemblyLoadContext.CurrentContextualReflectionContext);

            [Fact]
            public void UsesItsTypeConverter() =>
                Assert.IsType<CelsiusConverter>(TypeDescriptor.GetConverter(typeof(Celsius)));
        }

        [TypeConverter(typeof(CelsiusConverter))]
        public sealed class Celsius
        {
        }

        public sealed class CelsiusConverter : TypeConverter
        {
        }
        """;

    private const string AsyncVoidSource = """
        using System.Threading.Tasks;
        using Xunit;

        namespace Output.Tests;

        public class AsyncVoidTests
        {
            [Fact]
            public async void PassesAfterAwait()
            {
                await Task.Delay(20);
                Assert.True(true);
            }

            [Fact]
            public async void FailsAfterAwait()
            {
                await Task.Delay(20);
                await Task.Yield();
                Assert.Equal(1, 2);
            }

            [Theory]
            [InlineData(0)]
            [InlineData("text")]
            public void TakesAString(string value) => Assert.NotNull(value);
        }
        """;

    private const string HookSource = """
        using System.Reflection;
        using Xunit;
        using Xunit.Sdk;

        namespace Output.Tests;

        public sealed class RecordAttribute : BeforeAfterTestAttribute
        {
            public static string Log = "";

            public override void Before(MethodInfo methodUnderTest) => Log += "before " + methodUnderTest.Name + ";";

            public override void After(MethodInfo methodUnderTest) => Log += "after;";
        }

        [Record]
        public class HookTests
        {
            [Fact]
            public void SeesItsBefore() => Assert.EndsWith("before SeesItsBefore;", RecordAttribute.Log);

            [Fact]
            public void FailsAfterItsBefore() => throw new System.InvalidOperationException(RecordAttribute.Log);
        }
        """;

    [Fact]
    public async Task BeforeAfterTest_attributes_run_around_each_test_as_Verify_needs()
    {
        var summary = await UserProject.RunAllAsync(HookSource, UserProject.XunitAssemblies);

        Assert.Equal(TestOutcome.Passed, summary.Result("SeesItsBefore").Outcome);
        var failed = summary.Result("FailsAfterItsBefore");
        Assert.Equal(TestOutcome.Errored, failed.Outcome);
        Assert.Contains("before FailsAfterItsBefore;", failed.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Tests_run_from_their_output_folder_as_under_dotnet_test()
    {
        var before = Directory.GetCurrentDirectory();
        var baseBefore = AppContext.BaseDirectory;
        var summary = await UserProject.RunAllAsync(EnvironmentSource.Replace("FOLDER", _folder, StringComparison.Ordinal), UserProject.XunitAssemblies, new TestRunOptions { OutputDirectory = _folder });

        Assert.All(summary.Results, r => Assert.True(r.Outcome == TestOutcome.Passed, $"{r.Test.DisplayName}: {r.Message}"));
        Assert.Equal(5, summary.Passed);

        // The IDE's own directories come back when the run ends.
        Assert.Equal(before, Directory.GetCurrentDirectory());
        Assert.Equal(baseBefore, AppContext.BaseDirectory);
    }

    [Fact]
    public async Task A_changed_build_loads_from_a_sibling_folder_because_a_loaded_file_cannot_be_replaced()
    {
        var source = EnvironmentSource.Replace("FOLDER", _folder, StringComparison.Ordinal);
        using var first = UserProject.Load(source, UserProject.XunitAssemblies, new TestRunOptions { OutputDirectory = _folder });
        Assert.Equal(_folder, first.OutputDirectory);
        Assert.True(File.Exists(Path.Combine(_folder, first.Tests[0].AssemblyName + ".dll")));

        // The same assembly name with other code, as after an edit: the first session's file stays as it was.
        var changed = UserProject.Compile(source + "\npublic sealed class Added { }\n", UserProject.XunitAssemblies) with { Name = first.Tests[0].AssemblyName };
        using var second = TestSession.Load([changed], UserProject.XunitAssemblies, new TestRunOptions { OutputDirectory = _folder });
        Assert.Equal(_folder + "-2", second.OutputDirectory);

        var summary = await second.RunSelectedAsync(["Output.Tests.EnvironmentTests.FindsItsOwnTypesByName"]);
        Assert.Equal(1, summary.Passed);

        // A third build takes the next name and deletes the sibling before it, so edits leave at most one behind.
        var third = UserProject.Compile(source + "\npublic sealed class AddedAgain { }\n", UserProject.XunitAssemblies) with { Name = first.Tests[0].AssemblyName };
        using var thirdSession = TestSession.Load([third], UserProject.XunitAssemblies, new TestRunOptions { OutputDirectory = _folder });
        Assert.Equal(_folder + "-3", thirdSession.OutputDirectory);
        Assert.False(Directory.Exists(_folder + "-2"));
        Assert.True(Directory.Exists(_folder));
    }

    [Fact]
    public async Task Under_the_debugger_a_test_s_own_timeout_does_not_abandon_it()
    {
        const string source = """
            using NUnit.Framework;

            namespace Output.Tests;

            public class SlowTests
            {
                [Test, Timeout(50)]
                public void PausedLongerThanItsTimeout() => System.Threading.Thread.Sleep(400);
            }
            """;

        var timed = await UserProject.RunAllAsync(source, UserProject.NUnitAssemblies);
        Assert.Equal(TestOutcome.Errored, Assert.Single(timed.Results).Outcome);

        var debugged = await UserProject.RunAllAsync(source, UserProject.NUnitAssemblies, new TestRunOptions { DefaultTimeout = null, IgnoreTestTimeouts = true });
        Assert.Equal(TestOutcome.Passed, Assert.Single(debugged.Results).Outcome);
    }

    [Fact]
    public async Task Async_void_tests_are_waited_for_and_strings_take_numbers_as_in_xunit()
    {
        var summary = await UserProject.RunAllAsync(AsyncVoidSource, UserProject.XunitAssemblies);

        Assert.Equal(TestOutcome.Passed, summary.Result("PassesAfterAwait").Outcome);
        var failed = summary.Result("FailsAfterAwait");
        Assert.Equal(TestOutcome.Failed, failed.Outcome);
        Assert.Contains("Assert.Equal", failed.Message, StringComparison.Ordinal);
        Assert.All(summary.Results.Where(r => r.Test.MethodName == "TakesAString"), r => Assert.Equal(TestOutcome.Passed, r.Outcome));
        Assert.Equal(4, summary.Passed + summary.Failed);
    }
}

/// <summary>The tests that change the process's current directory run one at a time.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class RunEnvironmentGroup
{
    public const string Name = "Run environment";
}
