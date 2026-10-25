using System.Collections.Concurrent;
using TabStudio.LanguageServers.Configuration;
using TabStudio.LanguageServers.DebugAdapters;
using TabStudio.LanguageServers.Features;
using TabStudio.LanguageServers.Hosting;
using TabStudio.LanguageServers.Loopback;
using TabStudio.Testing;

namespace TabStudio.LanguageServers.Tests.Loopback;

public sealed class LoopbackTests
{
    private const string Script = """
        # Greets someone.
        let name = "Ada"
        let count = 3
        print name
        print missing
        """;

    [Fact]
    public void Scripts_are_read_line_by_line()
    {
        var statements = LoopbackScript.Parse(Script);

        Assert.Equal(LoopbackStatementKind.None, statements[0].Kind);
        Assert.Equal(("name", 4, "\"Ada\""), (statements[1].Name, statements[1].NameColumn, statements[1].Value));
        Assert.Equal(LoopbackStatementKind.Print, statements[3].Kind);
        Assert.Equal("\"Ada\"", LoopbackScript.Evaluate("name", new Dictionary<string, string> { ["name"] = "\"Ada\"" }));
        Assert.Null(LoopbackScript.Evaluate("missing", new Dictionary<string, string>()));
        Assert.Equal("Ada", LoopbackScript.Display("\"Ada\""));
    }

    [Fact]
    public async Task The_loopback_server_reports_problems_completes_names_and_hovers()
    {
        using var temp = new TempDirectory();
        temp.Write(".tabstudio/settings.json", LoopbackLauncher.Settings);
        var path = temp.Write("greet.loopback", Script);
        await using var host = new LanguageServerHost(WorkspaceConfiguration.Load(temp.Path), new LoopbackLauncher(ProcessServerLauncher.Instance));

        var document = host.OpenDocument(path, Script) ?? throw new InvalidOperationException("No server.");
        Assert.True(await document.WaitUntilReadyAsync(CancellationToken.None).WaitAsync(TestTimeouts.Default));
        await WaitUntilAsync(() => document.Diagnostics.Count > 0);

        var diagnostic = Assert.Single(document.Diagnostics);
        Assert.Equal(("LB0001", 5, 7), (diagnostic.Code, diagnostic.Line, diagnostic.Column));
        Assert.NotNull(diagnostic.CodeLink);
        var completion = await document.GetCompletionsAsync(Script.IndexOf("print missing", StringComparison.Ordinal) + 6, LspCompletionTriggerKind.Invoked, null, CancellationToken.None);
        Assert.Contains(completion!.Items, i => i.Label == "count" && i.Kind == LspCompletionItemKind.Variable);
        var hover = await document.GetHoverAsync(Script.IndexOf("print name", StringComparison.Ordinal) + 7, CancellationToken.None);
        Assert.Contains("(variable) name: string", hover!.Contents.Value, StringComparison.Ordinal);
        var definition = Assert.Single(await document.GetDefinitionsAsync(Script.IndexOf("print name", StringComparison.Ordinal) + 7, CancellationToken.None));
        Assert.Equal((2, 5), (definition.Line, definition.Column));

        document.ApplyChange(Script.IndexOf("missing", StringComparison.Ordinal), "missing".Length, "count");
        await WaitUntilAsync(() => document.Diagnostics.Count == 0);
    }

    [Fact]
    public async Task The_loopback_adapter_stops_at_a_breakpoint_shows_locals_and_runs_to_the_end()
    {
        using var temp = new TempDirectory();
        var path = temp.Write("greet.loopback", "let name = \"Ada\"\nlet count = 3\nprint name\nprint count\n");
        var configuration = LanguageServerRegistry.BuiltIn.WithWorkspaceSettings(JsonSettings.ParseObject(LoopbackLauncher.Settings));
        var adapter = configuration.FindAdapter(LoopbackLauncher.DebugAdapterType)!;
        var launcher = new LoopbackLauncher(ProcessServerLauncher.Instance);
        await using var session = await DebugAdapterSession.StartAsync(adapter, launcher.Resolve(adapter.Command)!, temp.Path, launcher).WaitAsync(TestTimeouts.Default);
        var stopped = new TaskCompletionSource<DapStoppedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
        var exited = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var output = new ConcurrentQueue<string>();
        session.Stopped += (_, e) => stopped.TrySetResult(e);
        session.Exited += (_, e) => exited.TrySetResult(e.ExitCode);
        session.OutputReceived += (_, e) => output.Enqueue(e.Output);

        var launch = LaunchConfiguration.ForCurrentFile(adapter, "Loopback").Substitute(new LaunchVariables(temp.Path, path));
        var breakpoints = new Dictionary<string, IReadOnlyList<DapSourceBreakpoint>> { [path] = [new DapSourceBreakpoint(3), new DapSourceBreakpoint(4, LogMessage: "count is {count}")] };
        var set = await session.LaunchAsync(launch, breakpoints, [], CancellationToken.None).WaitAsync(TestTimeouts.Default);
        var stop = await stopped.Task.WaitAsync(TestTimeouts.Default);

        Assert.All(set[path], b => Assert.True(b.IsVerified));
        Assert.Equal("breakpoint", stop.Reason);
        var frame = Assert.Single(await session.GetStackTraceAsync(1, CancellationToken.None));
        Assert.Equal((path, 3), (frame.FilePath, frame.Line));
        var scope = Assert.Single(await session.GetScopesAsync(frame.Id, CancellationToken.None));
        var locals = await session.GetVariablesAsync(scope.VariablesReference, CancellationToken.None);
        Assert.Equal(new[] { "name", "count" }, locals.Select(v => v.Name));
        await session.SetVariableAsync(scope.VariablesReference, "name", "\"Grace\"", CancellationToken.None);

        await session.ContinueAsync(1, CancellationToken.None);

        Assert.Equal(0, await exited.Task.WaitAsync(TestTimeouts.Default));
        Assert.Equal("Grace\ncount is 3\n3\n", string.Concat(output));
    }

    [Fact]
    public async Task A_name_without_a_value_stops_the_script_at_the_error()
    {
        using var temp = new TempDirectory();
        var path = temp.Write("broken.loopback", "let name = \"Ada\"\nprint nobody\n");
        var configuration = LanguageServerRegistry.BuiltIn.WithWorkspaceSettings(JsonSettings.ParseObject(LoopbackLauncher.Settings));
        var adapter = configuration.FindAdapter(LoopbackLauncher.DebugAdapterType)!;
        var launcher = new LoopbackLauncher(ProcessServerLauncher.Instance);
        await using var session = await DebugAdapterSession.StartAsync(adapter, LoopbackLauncher.DebugAdapterCommand, temp.Path, launcher).WaitAsync(TestTimeouts.Default);
        var stopped = new TaskCompletionSource<DapStoppedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
        session.Stopped += (_, e) => stopped.TrySetResult(e);

        var launch = LaunchConfiguration.ForCurrentFile(adapter, "Loopback").Substitute(new LaunchVariables(temp.Path, path));
        await session.LaunchAsync(launch, new Dictionary<string, IReadOnlyList<DapSourceBreakpoint>>(), ["uncaught"], CancellationToken.None).WaitAsync(TestTimeouts.Default);
        var stop = await stopped.Task.WaitAsync(TestTimeouts.Default);
        var exception = await session.GetExceptionInfoAsync(1, CancellationToken.None);

        Assert.Equal("exception", stop.Reason);
        Assert.Equal(("NameError", "'nobody' is not defined."), (exception!.ExceptionId, exception.Message));
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + TestTimeouts.Default;
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "Timed out.");
            await Task.Delay(20);
        }
    }
}
