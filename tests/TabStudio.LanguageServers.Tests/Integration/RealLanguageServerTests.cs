using TabStudio.LanguageServers.Configuration;
using TabStudio.LanguageServers.Features;
using TabStudio.LanguageServers.Hosting;
using TabStudio.Testing;
using Xunit.Abstractions;

namespace TabStudio.LanguageServers.Tests.Integration;

/// <summary>
/// Pyright and typescript-language-server, as installed on the PATH: completion, diagnostics, hover, definition and
/// rename through the same host the editor uses. The CI job installs them with npm.
/// </summary>
public sealed class RealLanguageServerTests(ITestOutputHelper output)
{
    [DartServerFact]
    public async Task Dart_reports_diagnostics_and_completes_String_members()
    {
        await using var workspace = new RealWorkspace(output);
        workspace.Write("pubspec.yaml", "name: analysis_test\nenvironment:\n  sdk: '>=3.10.0 <4.0.0'\n");
        const string source = "void main() {\n  print(missingName);\n  'hello'.toU\n}\n";
        var document = await workspace.OpenAsync("bin/main.dart", source);
        var error = await RealWorkspace.WaitForDiagnosticAsync(document, d => d.Message.Contains("missingName", StringComparison.Ordinal));
        Assert.Equal(LspDiagnosticSeverity.Error, error.Severity);
        var offset = source.IndexOf("toU", StringComparison.Ordinal) + 3;
        var list = await document.GetCompletionsAsync(offset, LspCompletionTriggerKind.Invoked, null, CancellationToken.None).WaitAsync(TestTimeouts.RealServer);
        Assert.NotNull(list);
        Assert.Contains(list.Items, item => item.Label.StartsWith("toUpperCase", StringComparison.Ordinal));
    }

    private const string PythonProgram = """
        import os


        def greet(name: str) -> str:
            return "Hello, " + name


        message = greet("Ada")
        print(mesage)
        os.pa
        """;

    private const string TypeScriptProgram = """
        function greet(name: string): string {
            return "Hello, " + name;
        }

        const message = greet("Ada");
        console.log(mesage);
        console.
        """;

    [RealServerFact("pyright-langserver")]
    public async Task Pyright_reports_an_undefined_name_as_an_error()
    {
        await using var workspace = new RealWorkspace(output);
        var document = await workspace.OpenAsync("main.py", PythonProgram);

        var diagnostic = await RealWorkspace.WaitForDiagnosticAsync(document, d => d.Message.Contains("mesage", StringComparison.Ordinal));

        Assert.Equal(LspDiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal(9, diagnostic.Line);
        Assert.Equal(7, diagnostic.Column);
        Assert.Equal("mesage", document.Snapshot.Text.Substring(diagnostic.Span.Start, diagnostic.Span.Length));
        Assert.Equal("reportUndefinedVariable", diagnostic.Code);
    }

    [RealServerFact("pyright-langserver")]
    public async Task Pyright_completes_the_members_of_a_module()
    {
        await using var workspace = new RealWorkspace(output);
        var document = await workspace.OpenAsync("main.py", PythonProgram);

        var list = await document.GetCompletionsAsync(PythonProgram.Length, LspCompletionTriggerKind.Invoked, null, CancellationToken.None).WaitAsync(TestTimeouts.RealServer);

        Assert.NotNull(list);
        var path = Assert.Single(list.Items, i => i.Label == "path");
        Assert.Equal("pa", document.Snapshot.Text.Substring(path.Edit.Span.Start, path.Edit.Span.Length));
        Assert.Equal("path", path.Edit.NewText);
    }

    [RealServerFact("pyright-langserver")]
    public async Task Pyright_shows_the_signature_in_quick_info_and_finds_the_definition()
    {
        await using var workspace = new RealWorkspace(output);
        var document = await workspace.OpenAsync("main.py", PythonProgram);
        var call = PythonProgram.IndexOf("greet(\"Ada\")", StringComparison.Ordinal) + 2;

        var hover = await document.GetHoverAsync(call, CancellationToken.None).WaitAsync(TestTimeouts.RealServer);
        var definitions = await document.GetDefinitionsAsync(call, CancellationToken.None).WaitAsync(TestTimeouts.RealServer);

        Assert.NotNull(hover);
        Assert.Contains("greet(name: str) -> str", hover.Contents.Value, StringComparison.Ordinal);
        var definition = Assert.Single(definitions);
        Assert.Equal(document.FilePath, definition.FilePath);
        Assert.Equal((4, 5), (definition.Line, definition.Column));
    }

    [RealServerFact("pyright-langserver")]
    public async Task Pyright_renames_a_function_and_its_calls_across_files()
    {
        await using var workspace = new RealWorkspace(output);
        var other = workspace.Write("app.py", "from main import greet\n\nprint(greet(\"Grace\"))\n");
        var document = await workspace.OpenAsync("main.py", PythonProgram);
        workspace.Open(other);
        var declaration = PythonProgram.IndexOf("greet", StringComparison.Ordinal) + 1;

        var target = await document.PrepareRenameAsync(declaration, CancellationToken.None).WaitAsync(TestTimeouts.RealServer);
        var edit = await document.RenameAsync(declaration, "welcome", CancellationToken.None).WaitAsync(TestTimeouts.RealServer);

        Assert.NotNull(target);
        Assert.Equal("greet", PythonProgram.Substring(target.Span.Start, target.Span.Length));
        Assert.NotNull(edit);
        var main = Assert.Single(edit.Changes, c => c.FilePath == document.FilePath);
        Assert.Equal(2, main.Edits.Count);
        Assert.All(main.Edits, e => Assert.Equal(("greet", "welcome"), (PythonProgram.Substring(e.Span.Start, e.Span.Length), e.NewText)));
        var app = Assert.Single(edit.Changes, c => c.FilePath == other);
        Assert.Equal(2, app.Edits.Count);
    }

    [RealServerFact("typescript-language-server")]
    public async Task TypeScript_reports_an_undefined_name_with_its_code()
    {
        await using var workspace = new RealWorkspace(output);
        var document = await workspace.OpenAsync("main.ts", TypeScriptProgram);

        var diagnostic = await RealWorkspace.WaitForDiagnosticAsync(document, d => d.Message.Contains("mesage", StringComparison.Ordinal));

        Assert.Equal(LspDiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal("2552", diagnostic.Code);
        Assert.Equal((6, 13), (diagnostic.Line, diagnostic.Column));
        Assert.Contains("message", diagnostic.Message, StringComparison.Ordinal);
    }

    [RealServerFact("typescript-language-server")]
    public async Task TypeScript_completes_members_after_a_dot()
    {
        await using var workspace = new RealWorkspace(output);
        var document = await workspace.OpenAsync("main.ts", TypeScriptProgram);

        var list = await document.GetCompletionsAsync(TypeScriptProgram.Length, LspCompletionTriggerKind.TriggerCharacter, '.', CancellationToken.None).WaitAsync(TestTimeouts.RealServer);

        Assert.NotNull(list);
        var log = Assert.Single(list.Items, i => i.Label == "log");
        Assert.Equal(LspCompletionItemKind.Method, log.Kind);
        var resolved = await document.ResolveCompletionAsync(log, CancellationToken.None).WaitAsync(TestTimeouts.RealServer);
        Assert.Contains("log(", resolved.Detail ?? string.Empty, StringComparison.Ordinal);
    }

    [RealServerFact("typescript-language-server")]
    public async Task TypeScript_finds_the_definition_and_renames_it()
    {
        await using var workspace = new RealWorkspace(output);
        var document = await workspace.OpenAsync("main.ts", TypeScriptProgram);
        var call = TypeScriptProgram.IndexOf("greet(\"Ada\")", StringComparison.Ordinal) + 1;

        var definition = Assert.Single(await document.GetDefinitionsAsync(call, CancellationToken.None).WaitAsync(TestTimeouts.RealServer));
        var edit = await document.RenameAsync(call, "welcome", CancellationToken.None).WaitAsync(TestTimeouts.RealServer);

        Assert.Equal((1, 10), (definition.Line, definition.Column));
        Assert.NotNull(edit);
        var change = Assert.Single(edit.Changes);
        Assert.Equal(2, change.Edits.Count);
        Assert.All(change.Edits, e => Assert.Equal("greet", TypeScriptProgram.Substring(e.Span.Start, e.Span.Length)));
    }

    /// <summary>A temporary workspace with a host that starts the real servers, logging what they say to the test's output.</summary>
    private sealed class RealWorkspace : IAsyncDisposable
    {
        private readonly TempDirectory _temp = new();
        private readonly ITestOutputHelper _output;

        public RealWorkspace(ITestOutputHelper output)
        {
            _output = output;
            Host = new LanguageServerHost(WorkspaceConfiguration.Load(_temp.Path), ProcessServerLauncher.Instance, new LanguageServerHostOptions
            {
                InitializeTimeout = TestTimeouts.RealServer,
                ClientVersion = "tests",
            });
            Host.LogReceived += (_, e) => Log($"[{e.ServerName}] {e.Level}: {e.Message}");
        }

        public LanguageServerHost Host { get; }

        public string Write(string relativePath, string text) => _temp.Write(relativePath, text);

        public async Task<LanguageServerDocument> OpenAsync(string relativePath, string text)
        {
            var document = Open(Write(relativePath, text));
            Assert.True(await document.WaitUntilReadyAsync(CancellationToken.None).WaitAsync(TestTimeouts.RealServer), document.ServerMessage);
            return document;
        }

        public LanguageServerDocument Open(string path) =>
            Host.OpenDocument(path, File.ReadAllText(path)) ?? throw new InvalidOperationException($"No language server for {path}.");

        public static async Task<LspDiagnostic> WaitForDiagnosticAsync(LanguageServerDocument document, Func<LspDiagnostic, bool> match)
        {
            var found = new TaskCompletionSource<LspDiagnostic>(TaskCreationOptions.RunContinuationsAsynchronously);
            void Check()
            {
                if (document.Diagnostics.FirstOrDefault(match) is { } diagnostic)
                {
                    found.TrySetResult(diagnostic);
                }
            }

            document.DiagnosticsChanged += (_, _) => Check();
            Check();
            return await found.Task.WaitAsync(TestTimeouts.RealServer);
        }

        public async ValueTask DisposeAsync()
        {
            await Host.DisposeAsync();
            _temp.Dispose();
        }

        private void Log(string line)
        {
            try
            {
                _output.WriteLine(line);
            }
            catch (InvalidOperationException)
            {
                // The test already ended.
            }
        }
    }
}
