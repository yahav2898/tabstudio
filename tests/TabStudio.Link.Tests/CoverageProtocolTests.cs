using System.Reflection.Metadata;
using TabStudio.Compilation;
using TabStudio.Core.Workspaces;
using TabStudio.Link.Language;
using TabStudio.Link.Messaging;
using TabStudio.LiveTesting;
using TabStudio.LiveTesting.Building;
using TabStudio.LiveTesting.Results;
using TabStudio.Testing;
using TabStudio.TestRunner;

namespace TabStudio.Link.Tests;

/// <summary>
/// Live Unit Testing builds over a loopback link: the host compiles and instruments a real library and its xUnit
/// project with Roslyn, and the tablet gets the same build it would have made itself, runs the tests and sees the
/// coverage.
/// </summary>
public sealed class CoverageProtocolTests : IAsyncLifetime, IDisposable
{
    private const string Library = """
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

    private const string Tests = """
        using Calc;
        using Xunit;

        namespace Calc.Tests;

        public class ArithmeticTests
        {
            [Fact]
            public void Adds() => Assert.Equal(5, Arithmetic.Add(2, 3));

            [Fact]
            public void Subtracts() => Assert.Equal(1, Arithmetic.Subtract(3, 2));
        }
        """;

    /// <summary>xUnit v2's assemblies, from this test project's own package references.</summary>
    private static readonly IReadOnlyList<string> XunitAssemblies =
    [
        typeof(FactAttribute).Assembly.Location,
        typeof(Assert).Assembly.Location,
        typeof(global::Xunit.Abstractions.ITestOutputHelper).Assembly.Location,
    ];

    private readonly TempDirectory _tablet = new();
    private readonly TempDirectory _host = new();
    private readonly RoslynWorkspaceHost _hostRoslyn = new();
    private readonly FakeBackend _backend;
    private LinkLanguageServer? _server;
    private LinkConnection? _clientConnection;
    private LinkConnection? _serverConnection;
    private LinkLanguageClient? _client;
    private Workspace? _workspace;

    public CoverageProtocolTests()
    {
        _backend = new FakeBackend { Roslyn = _hostRoslyn, TestPackages = XunitAssemblies };
    }

    private LinkLanguageClient Client => _client!;

    private Workspace Workspace => _workspace!;

    private ProjectInfo TestProject => Workspace.Projects.Single(p => p.IsTestProject);

    private string LibraryFile => _tablet.Resolve("Calc/Arithmetic.cs");

    private string TestFile => _tablet.Resolve("Calc.Tests/ArithmeticTests.cs");

    public async Task InitializeAsync()
    {
        _tablet.Write("Calc.slnx", """
            <Solution>
              <Project Path="Calc/Calc.csproj" />
              <Project Path="Calc.Tests/Calc.Tests.csproj" />
            </Solution>
            """);
        _tablet.Write("Calc/Calc.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
              </PropertyGroup>
            </Project>
            """);
        _tablet.Write("Calc/Arithmetic.cs", Library);
        _tablet.Write("Calc.Tests/Calc.Tests.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
                <IsTestProject>true</IsTestProject>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="xunit" Version="2.9.3" />
              </ItemGroup>
              <ItemGroup>
                <ProjectReference Include="..\Calc\Calc.csproj" />
              </ItemGroup>
            </Project>
            """);
        _tablet.Write("Calc.Tests/ArithmeticTests.cs", Tests);
        _workspace = WorkspaceLoader.Load(_tablet.Resolve("Calc.slnx"));

        _server = new LinkLanguageServer(_host.Path, _ => Task.FromResult<ILinkLanguageBackend>(_backend));
        var (client, server) = await Loopback.ConnectAsync();
        _clientConnection = new LinkConnection(client);
        _serverConnection = new LinkConnection(server);
        _serverConnection.Start(_server);
        _clientConnection.Start(new TestHandler());
        _client = new LinkLanguageClient(_clientConnection);
        await Client.LoadAsync(Workspace, FileSystemTextProvider.Instance, optimize: false, [], null, CancellationToken.None);
    }

    public async Task DisposeAsync()
    {
        if (_clientConnection is not null)
        {
            await _clientConnection.DisposeAsync();
        }

        if (_serverConnection is not null)
        {
            await _serverConnection.DisposeAsync();
        }
    }

    public void Dispose()
    {
        _server?.Dispose();
        _hostRoslyn.Dispose();
        _tablet.Dispose();
        _host.Dispose();
    }

    [Fact]
    public async Task A_coverage_build_on_the_host_is_the_build_the_tablet_would_make()
    {
        using var tabletRoslyn = new RoslynWorkspaceHost();
        tabletRoslyn.Load(Workspace, FileSystemTextProvider.Instance);
        tabletRoslyn.SetPackageReferences(TestProject.FilePath, XunitAssemblies);
        var local = await new CoverageBuilder(tabletRoslyn).BuildAsync(TestProject);

        var remote = await BuildOnHostAsync();

        Assert.True(local.Success, string.Join('\n', local.Result.Diagnostics));
        Assert.True(remote.Success, string.Join('\n', remote.Result.Diagnostics));
        Assert.Equal(local.Projects.Select(p => (p.Project, p.IsInstrumented)), remote.Projects.Select(p => (p.Project, p.IsInstrumented)));
        Assert.Same(TestProject, remote.Projects[^1].Project);
        Assert.Equal(local.Sources.Keys.Order(StringComparer.Ordinal), remote.Sources.Keys.Order(StringComparer.Ordinal));
        Assert.All(local.Sources, s => Assert.Equal(s.Value.ToString(), remote.Sources[s.Key].ToString()));

        var expected = Assert.Single(local.Modules);
        var actual = Assert.Single(remote.Modules);
        Assert.Equal(_tablet.Resolve("Calc/Calc.csproj"), actual.ProjectFilePath);
        Assert.Equal(CoverageBuilder.SlotOf(actual.ProjectFilePath), actual.Index);
        Assert.Equal(expected.Index, actual.Index);
        Assert.Equal(expected.Members, actual.Members);
        Assert.Equal(expected.Statements, actual.Statements);
        Assert.Equal(expected.PointMembers, actual.PointMembers);
        Assert.All(actual.Members, m => Assert.Equal(LibraryFile, m.FilePath));

        // The PDBs name the tablet's files, so stack traces and the debugger point at them.
        var library = Assert.Single(remote.Result.Dependencies);
        Assert.Contains(LibraryFile, PdbDocuments(library));
        Assert.DoesNotContain(PdbDocuments(library), d => d.StartsWith(_host.Path, StringComparison.Ordinal));
        Assert.Contains(TestFile, PdbDocuments(remote.Result.EntryAssembly!));
    }

    [Fact]
    public async Task The_tablet_runs_the_hosts_build_and_its_coverage_recorder_sees_every_test()
    {
        using var engine = new LiveTestEngine((project, uninstrumented, token) => BuildOnHostAsync(uninstrumented, token), _ => XunitAssemblies);

        var summary = await engine.RunAsync([TestProject], new QuietListener());

        Assert.Equal(2, summary.TestsRun);
        Assert.Empty(summary.FailedBuilds);
        Assert.Equal(LineCoverageState.Covered, LineOf(engine, "return a + b;").State);
        Assert.Equal(LineCoverageState.Failing, LineOf(engine, "return a + b;", occurrence: 2).State);
        Assert.Equal(LineCoverageState.NotCovered, LineOf(engine, "return -value;").State);
        Assert.Equal(1, _backend.CoverageBuilds);
    }

    [Fact]
    public async Task Later_builds_send_only_what_changed()
    {
        var first = await BuildOnHostAsync();
        await Client.UpdateTextAsync(LibraryFile, Library.Replace("return -value;", "return 0 - value;", StringComparison.Ordinal));

        var second = await BuildOnHostAsync();
        var third = await BuildOnHostAsync();

        // The edited file came again; the test file and the assemblies of the unchanged build came from this device.
        Assert.Contains("return 0 - value;", second.Sources[LibraryFile].ToString(), StringComparison.Ordinal);
        Assert.Same(first.Sources[TestFile], second.Sources[TestFile]);
        Assert.NotSame(first.Sources[LibraryFile], second.Sources[LibraryFile]);
        Assert.Same(second.Result.EntryAssembly, third.Result.EntryAssembly);
        Assert.Same(Assert.Single(second.Result.Dependencies), Assert.Single(third.Result.Dependencies));
        Assert.Same(second.Sources[LibraryFile], third.Sources[LibraryFile]);
        Assert.Equal(3, _backend.CoverageBuilds);
    }

    [Fact]
    public async Task A_failed_build_reports_the_errors_on_the_tablets_paths()
    {
        await Client.UpdateTextAsync(LibraryFile, Library.Replace("return -value;", "return -valu;", StringComparison.Ordinal));

        var build = await BuildOnHostAsync();

        Assert.False(build.Success);
        Assert.Empty(build.Modules);
        var error = Assert.Single(build.Result.Diagnostics, d => d.Id == "CS0103");
        Assert.Equal(LibraryFile, error.Location?.FilePath);
    }

    private Task<CoverageBuild> BuildOnHostAsync(IReadOnlySet<string>? uninstrumented = null, CancellationToken cancellationToken = default)
    {
        var slots = Workspace.Projects.ToDictionary(p => p.FilePath, p => CoverageBuilder.SlotOf(p.FilePath), StringComparer.Ordinal);
        return Client.BuildForCoverageAsync(
            TestProject,
            uninstrumented ?? new HashSet<string>(StringComparer.Ordinal),
            slots,
            path => Workspace.Projects.FirstOrDefault(p => string.Equals(p.FilePath, path, StringComparison.Ordinal)),
            cancellationToken);
    }

    private LineCoverage LineOf(LiveTestEngine engine, string text, int occurrence = 1)
    {
        var file = engine.Index.GetFile(LibraryFile);
        Assert.NotNull(file);
        var source = file.Text.ToString();
        var index = -1;
        for (var i = 0; i < occurrence; i++)
        {
            index = source.IndexOf(text, index + 1, StringComparison.Ordinal);
        }

        var line = source[..index].Count(c => c == '\n') + 1;
        return Assert.Single(file.Lines, l => l.Line == line);
    }

    private static List<string> PdbDocuments(CompiledAssembly assembly)
    {
        using var provider = MetadataReaderProvider.FromPortablePdbImage([.. assembly.Symbols!]);
        var reader = provider.GetMetadataReader();
        return [.. reader.Documents.Select(d => reader.GetString(reader.GetDocument(d).Name))];
    }

    /// <summary>Follows a live run without recording anything.</summary>
    private sealed class QuietListener : ILiveTestListener
    {
        public void BuildStarted(ProjectInfo testProject)
        {
        }

        public void BuildFinished(ProjectInfo testProject, CoverageBuild build)
        {
        }

        public void TestsDiscovered(ProjectInfo testProject, IReadOnlyList<TestCase> tests)
        {
        }

        public void TestsQueued(ProjectInfo testProject, IReadOnlyList<TestCase> tests)
        {
        }

        public void TestStarted(ProjectInfo testProject, TestCase test)
        {
        }

        public void TestFinished(ProjectInfo testProject, TestResult result)
        {
        }
    }
}
