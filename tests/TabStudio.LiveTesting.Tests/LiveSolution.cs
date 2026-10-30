using TabStudio.Compilation;
using TabStudio.Core.Workspaces;
using TabStudio.LiveTesting.Building;
using TabStudio.LiveTesting.Results;
using TabStudio.Testing;
using TabStudio.TestRunner;

namespace TabStudio.LiveTesting.Tests;

/// <summary>
/// A small solution on disk, loaded in a workspace host the way the IDE loads it: a library with a bug and an xUnit
/// project that tests it, with the xUnit assemblies of this test project standing in for restored packages. Both
/// projects are C#, or both Visual Basic.
/// </summary>
internal sealed class LiveSolution : IDisposable
{
    public const string Library = """
        namespace Calc;

        public static class Arithmetic
        {
            public static int Add(int a, int b)
            {
                return a + b;
            }

            public static int Subtract(int a, int b)
            {
                return a + b;
            }

            public static int Negate(int value)
            {
                return -value;
            }
        }
        """;

    public const string Tests = """
        using Calc;
        using Xunit;

        namespace Calc.Tests;

        public class ArithmeticTests
        {
            [Fact]
            public void Adds() => Assert.Equal(5, Arithmetic.Add(2, 3));

            [Fact]
            public void Subtracts() => Assert.Equal(1, Arithmetic.Subtract(3, 2));

            [Theory]
            [InlineData(1, 1, 2)]
            [InlineData(2, 2, 4)]
            public void Adds_pairs(int a, int b, int sum) => Assert.Equal(sum, Arithmetic.Add(a, b));
        }
        """;

    /// <summary>The library in Visual Basic: a module in the project's root namespace, <c>Calculator</c>.</summary>
    public const string VisualBasicLibrary = """
        Public Module Arithmetic
            Public Function Add(a As Integer, b As Integer) As Integer
                Return a + b
            End Function

            Public Function Subtract(a As Integer, b As Integer) As Integer
                Return a + b
            End Function

            Public Function Negate(value As Integer) As Integer
                Return -value
            End Function
        End Module
        """;

    /// <summary>The tests in Visual Basic, in the test project's root namespace, <c>Calc.Tests</c>.</summary>
    public const string VisualBasicTests = """
        Imports Calculator
        Imports Xunit

        Public Class ArithmeticTests
            <Fact>
            Public Sub Adds()
                Assert.True(Arithmetic.Add(2, 3) = 5)
            End Sub

            <Fact>
            Public Sub Subtracts()
                Assert.True(Arithmetic.Subtract(3, 2) = 1)
            End Sub

            <Theory>
            <InlineData(1, 1, 2)>
            <InlineData(2, 2, 4)>
            Public Sub Adds_pairs(a As Integer, b As Integer, sum As Integer)
                Assert.True(Arithmetic.Add(a, b) = sum)
            End Sub
        End Class
        """;

    private readonly TempDirectory _temp = new();

    /// <summary>Writes the solution and loads it.</summary>
    /// <param name="visualBasic">Whether both projects are Visual Basic projects instead of C# ones.</param>
    public LiveSolution(bool visualBasic = false)
    {
        var projectExtension = visualBasic ? "vbproj" : "csproj";
        var sourceExtension = visualBasic ? "vb" : "cs";
        _temp.Write("Live.slnx", $"""
            <Solution>
              <Project Path="Calc/Calc.{projectExtension}" />
              <Project Path="Calc.Tests/Calc.Tests.{projectExtension}" />
            </Solution>
            """);
        _temp.Write($"Calc/Calc.{projectExtension}", visualBasic
            ? """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                    <RootNamespace>Calculator</RootNamespace>
                  </PropertyGroup>
                </Project>
                """
            : """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <Nullable>enable</Nullable>
                  </PropertyGroup>
                </Project>
                """);
        LibraryFile = _temp.Write($"Calc/Arithmetic.{sourceExtension}", visualBasic ? VisualBasicLibrary : Library);
        var csharpProperties = visualBasic ? string.Empty : "<ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable>";
        _temp.Write($"Calc.Tests/Calc.Tests.{projectExtension}", $$"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                {{csharpProperties}}
                <IsTestProject>true</IsTestProject>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="xunit" Version="2.9.3" />
              </ItemGroup>
              <ItemGroup>
                <ProjectReference Include="..\Calc\Calc.{{projectExtension}}" />
              </ItemGroup>
            </Project>
            """);
        TestFile = _temp.Write($"Calc.Tests/ArithmeticTests.{sourceExtension}", visualBasic ? VisualBasicTests : Tests);

        Host.Load(WorkspaceLoader.Load(_temp.Path), FileSystemTextProvider.Instance);
        TestProject = Host.FindProject(_temp.Resolve($"Calc.Tests/Calc.Tests.{projectExtension}"))!;
        LibraryProject = Host.FindProject(_temp.Resolve($"Calc/Calc.{projectExtension}"))!;
        Host.SetPackageReferences(TestProject.FilePath, XunitAssemblies);
        Builder = new CoverageBuilder(Host);
        Engine = new LiveTestEngine((project, uninstrumented, token) => Builder.BuildAsync(project, uninstrumented, token), _ => XunitAssemblies);
    }

    /// <summary>xUnit v2's assemblies, from this test project's own package references.</summary>
    public static IReadOnlyList<string> XunitAssemblies { get; } =
    [
        typeof(FactAttribute).Assembly.Location,
        typeof(Assert).Assembly.Location,
        typeof(global::Xunit.Abstractions.ITestOutputHelper).Assembly.Location,
    ];

    public RoslynWorkspaceHost Host { get; } = new();

    public CoverageBuilder Builder { get; }

    public LiveTestEngine Engine { get; }

    public RecordingListener Listener { get; } = new();

    public ProjectInfo TestProject { get; }

    public ProjectInfo LibraryProject { get; }

    public string LibraryFile { get; }

    public string TestFile { get; }

    /// <summary>Runs the tests the changes since the last run affect.</summary>
    public Task<LiveRunSummary> RunAsync(params TestKey[] requested)
    {
        Listener.Clear();
        return Engine.RunAsync([TestProject], Listener, requested);
    }

    /// <summary>Replaces the library's text, as the editor does while the user types.</summary>
    public void EditLibrary(string text) => Host.UpdateText(LibraryFile, text);

    /// <summary>Replaces the test file's text.</summary>
    public void EditTests(string text) => Host.UpdateText(TestFile, text);

    /// <summary>The marker of a line of the library.</summary>
    public LineCoverage LibraryLine(string text)
    {
        var file = Engine.Index.GetFile(LibraryFile);
        Assert.NotNull(file);
        var line = LineOf(file.Text.ToString(), text);
        return Assert.Single(file.Lines, l => l.Line == line);
    }

    /// <summary>The method names of the tests the last run queued, sorted.</summary>
    public IReadOnlyList<string> QueuedMethods() =>
        [.. Listener.Queued.Select(t => t.MethodName).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];

    public static int LineOf(string source, string text)
    {
        var index = source.IndexOf(text, StringComparison.Ordinal);
        Assert.True(index >= 0, $"'{text}' is not in the source.");
        return source[..index].Count(c => c == '\n') + 1;
    }

    public void Dispose()
    {
        Engine.Dispose();
        Host.Dispose();
        _temp.Dispose();
    }

    /// <summary>Records what a live run reports.</summary>
    internal sealed class RecordingListener : ILiveTestListener
    {
        private readonly Lock _gate = new();
        private readonly List<TestCase> _queued = [];
        private readonly List<TestResult> _finished = [];
        private readonly List<CoverageBuild> _builds = [];

        public IReadOnlyList<TestCase> Queued
        {
            get
            {
                lock (_gate)
                {
                    return [.. _queued];
                }
            }
        }

        public IReadOnlyList<TestResult> Finished
        {
            get
            {
                lock (_gate)
                {
                    return [.. _finished];
                }
            }
        }

        public IReadOnlyList<CoverageBuild> Builds
        {
            get
            {
                lock (_gate)
                {
                    return [.. _builds];
                }
            }
        }

        public void Clear()
        {
            lock (_gate)
            {
                _queued.Clear();
                _finished.Clear();
                _builds.Clear();
            }
        }

        public void BuildStarted(ProjectInfo testProject)
        {
        }

        public void BuildFinished(ProjectInfo testProject, CoverageBuild build)
        {
            lock (_gate)
            {
                _builds.Add(build);
            }
        }

        public void TestsDiscovered(ProjectInfo testProject, IReadOnlyList<TestCase> tests)
        {
        }

        public void TestsQueued(ProjectInfo testProject, IReadOnlyList<TestCase> tests)
        {
            lock (_gate)
            {
                _queued.AddRange(tests);
            }
        }

        public void TestStarted(ProjectInfo testProject, TestCase test)
        {
        }

        public void TestFinished(ProjectInfo testProject, TestResult result)
        {
            lock (_gate)
            {
                _finished.Add(result);
            }
        }
    }
}
