using TabStudio.Compilation;
using TabStudio.Compilation.Features;
using TabStudio.Core.Workspaces;
using TabStudio.Link.Language;
using TabStudio.Link.Messaging;
using TabStudio.Testing;

namespace TabStudio.Link.Tests;

/// <summary>The language service's messages between a tablet and a host, over a fake language service on the host.</summary>
public sealed class LanguageProtocolTests : IAsyncLifetime, IDisposable
{
    private readonly TempDirectory _tablet = new();
    private readonly TempDirectory _host = new();
    private readonly FakeBackend _backend = new();
    private LinkLanguageServer? _server;
    private LinkConnection? _clientConnection;
    private LinkConnection? _serverConnection;
    private LinkLanguageClient? _client;
    private Workspace? _workspace;

    private string Program => _tablet.Resolve("HelloTablet/Program.cs");

    private LinkLanguageClient Client => _client!;

    public async Task InitializeAsync()
    {
        _tablet.Write("HelloTablet/HelloTablet.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
        _tablet.Write("HelloTablet/Program.cs", "Console.WriteLine(\"saved\");");
        _tablet.Write("HelloTablet/Assets/logo.png", "not really a png");
        _tablet.Write("HelloTablet/bin/Debug/HelloTablet.dll", "build output");
        _workspace = WorkspaceLoader.Load(_tablet.Resolve("HelloTablet"));

        _server = new LinkLanguageServer(_host.Path, _ => Task.FromResult<ILinkLanguageBackend>(_backend));
        var (client, server) = await Loopback.ConnectAsync();
        _clientConnection = new LinkConnection(client);
        _serverConnection = new LinkConnection(server);
        _serverConnection.Start(_server);
        _clientConnection.Start(new TestHandler());
        _client = new LinkLanguageClient(_clientConnection);
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
        _tablet.Dispose();
        _host.Dispose();
    }

    [Fact]
    public async Task Loading_mirrors_the_workspace_with_unsaved_text_and_without_build_output()
    {
        var documents = await LoadAsync("Console.WriteLine(\"unsaved\");");

        Assert.Equal([Program], documents);
        var hostProject = Assert.Single(_backend.Workspace!.Projects);
        Assert.StartsWith(_host.Path, hostProject.FilePath, StringComparison.Ordinal);
        Assert.Equal("Console.WriteLine(\"unsaved\");", File.ReadAllText(Path.Combine(hostProject.DirectoryPath, "Program.cs")));
        Assert.True(File.Exists(Path.Combine(hostProject.DirectoryPath, "Assets", "logo.png")));
        Assert.False(Directory.Exists(Path.Combine(hostProject.DirectoryPath, "bin")));
        var (from, to) = Assert.Single(_backend.PathMap);
        Assert.StartsWith(_host.Path, from, StringComparison.Ordinal);
        Assert.Equal(_tablet.Resolve("HelloTablet") + Path.DirectorySeparatorChar, to);
    }

    [Fact]
    public async Task Loading_again_sends_only_what_changed()
    {
        await LoadAsync();
        var afterFirst = _server!.RequestCount;

        await LoadAsync();
        var afterUnchanged = _server.RequestCount;
        await File.WriteAllTextAsync(_tablet.Resolve("HelloTablet/Greeter.cs"), "class Greeter;");
        await LoadAsync();

        Assert.Equal(2, afterUnchanged - afterFirst);
        Assert.Equal(3, _server.RequestCount - afterUnchanged);
        Assert.Equal(3, _backend.Loads);
    }

    [Fact]
    public async Task Results_come_back_with_the_tablets_paths()
    {
        await LoadAsync();

        var diagnostic = Assert.Single(await Client.GetDocumentDiagnosticsAsync(Program, CancellationToken.None));
        var reference = Assert.Single(await Client.FindReferencesAsync(Program, 0, CancellationToken.None));
        var definition = await Client.FindDefinitionAsync(Program, 0, CancellationToken.None);
        var rename = await Client.GetRenameEditsAsync(Program, 0, "Welcomer", null, CancellationToken.None);

        Assert.Equal(Program, diagnostic.Location!.FilePath);
        Assert.Equal("Program.cs", diagnostic.FileName);
        Assert.Equal(Program, reference.FilePath);
        Assert.Equal(Program, Assert.Single(definition!.Locations).FilePath);
        Assert.Equal(Program, Assert.Single(rename.Edits).FilePath);
        Assert.Null(await Client.GetQuickInfoAsync(Program, 0, CancellationToken.None));
    }

    [Fact]
    public async Task Completion_round_trips_its_items_and_commits()
    {
        await LoadAsync();

        var completions = await Client.GetCompletionsAsync(Program, 8, CompletionTrigger.Insertion('.'), CancellationToken.None);
        var item = Assert.Single(completions.Items);
        var commit = await Client.GetCompletionChangeAsync(Program, item, '(', CancellationToken.None);
        var description = await Client.GetCompletionDescriptionAsync(item, CancellationToken.None);

        Assert.Equal("WriteLine", item.DisplayText);
        Assert.Equal(new CompletionHandle(1, 0), item.Handle);
        Assert.Equal("WriteLine(", Assert.Single(commit.Edits).NewText);
        Assert.True(commit.IncludesCommitCharacter);
        Assert.Equal("void", Assert.Single(description).Text);
        Assert.Equal([new ClassifiedRange(0, 7, "class name")], await Client.GetClassificationsAsync(Program, CancellationToken.None));
    }

    [Fact]
    public async Task Builds_bring_back_their_assemblies_and_symbols()
    {
        await LoadAsync();

        var build = await Client.BuildAsync(_workspace!.Projects[0].FilePath, CancellationToken.None);
        var debug = await Client.BuildForDebuggingAsync(_workspace.Projects[0].FilePath, CancellationToken.None);

        Assert.True(build.Success);
        Assert.Equal([1, 2, 3], build.EntryAssembly!.Image);
        Assert.Equal([4, 5], build.EntryAssembly.Symbols);
        var dependency = Assert.Single(build.Dependencies);
        Assert.Equal("Greetings", dependency.Name);
        Assert.Null(dependency.Symbols);
        Assert.Equal(TimeSpan.FromMilliseconds(12), build.Duration);
        var statement = Assert.Single(debug.DebugInfo.Statements);
        Assert.Equal(Program, statement.Location.FilePath);
        Assert.Same(statement, debug.DebugInfo.FindStatement(Program, 1));
        Assert.Equal("Program.<Main>$(string[])", debug.DebugInfo.GetMethod(1)?.Name);
    }

    [Fact]
    public async Task Edits_reach_the_host_before_the_next_request()
    {
        await LoadAsync();

        await Client.UpdateTextAsync(Program, "Console.WriteLine(\"typed\");");
        await Client.GetDocumentDiagnosticsAsync(Program, CancellationToken.None);

        var hostProgram = Path.Combine(_backend.Workspace!.Projects[0].DirectoryPath, "Program.cs");
        Assert.Equal("Console.WriteLine(\"typed\");", _backend.Texts[hostProgram]);
    }

    [Fact]
    public async Task Settings_follow_the_tablet()
    {
        await Client.LoadAsync(_workspace!, FileSystemTextProvider.Instance, optimize: true, [], null, CancellationToken.None);
        Assert.True(_backend.Optimize);

        await Client.SetOptimizationAsync(false);
        await Client.GetProjectDiagnosticsAsync(_workspace!.Projects[0].FilePath, CancellationToken.None);

        Assert.False(_backend.Optimize);
    }

    [Fact]
    public async Task Requests_before_a_workspace_is_loaded_say_so()
    {
        var error = await Assert.ThrowsAsync<LinkRemoteException>(() => Client.GetDocumentDiagnosticsAsync(Program, CancellationToken.None));

        Assert.Equal(LinkErrorCodes.NoWorkspace, error.Code);
    }

    [Fact]
    public async Task Files_outside_the_workspace_have_no_results()
    {
        await LoadAsync();

        Assert.Empty(await Client.GetDocumentDiagnosticsAsync("/somewhere/else/Program.cs", CancellationToken.None));
    }

    private Task<IReadOnlyList<string>> LoadAsync(string? unsavedProgram = null) =>
        Client.LoadAsync(_workspace!, new Unsaved(Program, unsavedProgram), optimize: false, [], null, CancellationToken.None);

    /// <summary>The disk, with one file's unsaved text from the editor.</summary>
    private sealed class Unsaved(string path, string? text) : ISourceTextProvider
    {
        public string GetText(string filePath) =>
            text is not null && string.Equals(filePath, path, StringComparison.Ordinal) ? text : File.ReadAllText(filePath);
    }
}
