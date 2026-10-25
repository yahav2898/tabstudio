using Avalonia.Headless.XUnit;
using Microsoft.Extensions.DependencyInjection;
using TabStudio.App.Services;
using TabStudio.App.Services.Search;
using TabStudio.App.ViewModels;
using TabStudio.App.ViewModels.Search;
using TabStudio.Compilation.Features;
using TabStudio.LanguageServers.Hosting;
using TabStudio.LanguageServers.Loopback;
using TabStudio.Testing;

namespace TabStudio.App.Tests.ViewModels;

/// <summary>
/// The IDE's commands in a file a language server owns, with the built-in loopback server: navigation, references,
/// rename, the breadcrumb, Quick Open's symbols, settings, and breakpoints with hit counts and logpoints.
/// </summary>
public sealed class LanguageServerCommandTests : IDisposable
{
    private const string Script = """
        let greeting = "Hello"
        let name = "Ada"
        print greeting
        print name
        """;

    private readonly TempDirectory _temp = new();
    private readonly ServiceProvider _services;
    private readonly MainViewModel _main;

    public LanguageServerCommandTests()
    {
        _services = AppServices.Create(new AppPaths(_temp.Resolve("app")));
        _main = _services.GetRequiredService<MainViewModel>();
    }

    [AvaloniaFact]
    public async Task Go_to_definition_and_find_all_references_use_the_server()
    {
        var (workspace, document) = await OpenScriptAsync();
        document.CaretOffset = Script.IndexOf("print name", StringComparison.Ordinal) + "print n".Length;

        await workspace.GoToDefinitionCommand.ExecuteAsync(null);
        await UiTestHelpers.WaitUntilAsync(() => document.PendingNavigation is not null || document.CaretLine == 2, because: "the definition");
        Assert.Equal((2, 5), document.PendingNavigation ?? (document.CaretLine, document.CaretColumn));

        await workspace.FindAllReferencesCommand.ExecuteAsync(null);
        await UiTestHelpers.WaitUntilAsync(() => workspace.References.Items.Count == 2, because: "the references");
        Assert.Contains(workspace.References.Items, r => r.IsDefinition && r.Line == 2);
    }

    [AvaloniaFact]
    public async Task Rename_applies_the_servers_edits_after_the_prompt()
    {
        var (workspace, document) = await OpenScriptAsync();
        document.CaretOffset = Script.IndexOf("print greeting", StringComparison.Ordinal) + "print gr".Length;

        var rename = workspace.RenameSymbolCommand.ExecuteAsync(null);
        await UiTestHelpers.WaitUntilAsync(() => workspace.Prompt.IsOpen, because: "the rename prompt");
        Assert.Equal("greeting", workspace.Prompt.Text);
        workspace.Prompt.Text = "salutation";
        workspace.Prompt.ConfirmCommand.Execute(null);
        await rename;

        Assert.Equal(Script.Replace("greeting", "salutation", StringComparison.Ordinal), document.Document.Text);
        Assert.Equal("Renamed 'greeting' to 'salutation'", workspace.StatusText);
    }

    [AvaloniaFact]
    public async Task The_breadcrumb_and_the_search_show_the_servers_symbols()
    {
        var (workspace, document) = await OpenScriptAsync();

        await document.UpdateSymbolPathAsync(Script.IndexOf("\"Ada\"", StringComparison.Ordinal));
        var crumb = Assert.Single(document.SymbolPath);
        Assert.Equal("name", crumb.Name);
        Assert.Equal(SymbolGlyph.Local, crumb.Kind);

        workspace.Search.OpenCommand.Execute(null);
        workspace.Search.Query = "#greet";
        await workspace.Search.Completion;
        var symbol = Assert.Single(workspace.Search.Rows.OfType<SearchResultRow>(), row => row.Title == "greeting");
        Assert.Equal(SearchKind.Symbol, symbol.Item.Kind);
        Assert.Equal("Variable, greet.loopback", symbol.Detail);
        await workspace.Search.AcceptCommand.ExecuteAsync(symbol);
        Assert.Equal(1, document.PendingNavigation?.Line ?? document.CaretLine);
    }

    [AvaloniaFact]
    public async Task A_server_that_cannot_format_says_so()
    {
        var (workspace, _) = await OpenScriptAsync();

        await workspace.FormatDocumentCommand.ExecuteAsync(null);

        Assert.Equal("Loopback Server cannot format documents", workspace.StatusText);
    }

    [AvaloniaFact]
    public async Task Hit_counts_and_logpoints_reach_the_adapter_and_its_output()
    {
        var (workspace, document) = await OpenScriptAsync();
        var options = (IEditorBreakpointOptions)workspace;
        Assert.True(options.HasBreakpointOptions(document.FilePath));

        var logpoint = options.EditLogMessageAsync(document.FilePath, 4);
        await UiTestHelpers.WaitUntilAsync(() => workspace.Prompt.IsOpen, because: "the logpoint prompt");
        workspace.Prompt.Text = "name is {name}";
        workspace.Prompt.ConfirmCommand.Execute(null);
        await logpoint;

        var breakpoint = Assert.Single(workspace.Breakpoints.All);
        Assert.Equal(BreakpointGlyph.Logpoint, breakpoint.Glyph);
        Assert.Equal("Loopback: Current File", workspace.RunLabel);
        await workspace.StartDebuggingCommand.ExecuteAsync(null);
        await UiTestHelpers.WaitUntilAsync(() => !workspace.IsDebugging, because: "the script to run to its end");

        Assert.Contains("name is Ada", workspace.Output.Text, StringComparison.Ordinal);
        Assert.Contains("has exited with code 0", workspace.Output.Text, StringComparison.Ordinal);
        Assert.Null(breakpoint.IsVerifiedByAdapter);
    }

    [AvaloniaFact]
    public async Task Missing_servers_are_named_in_the_status_bar()
    {
        var folder = _temp.Resolve("python");
        var path = _temp.Write("python/main.py", "print('hi')\n");
        _temp.Write("python/.tabstudio/settings.json", """{ "languageServers": { "pyright": { "command": "no-such-pyright" } } }""");
        await _main.OpenWorkspaceAsync(folder);
        var workspace = _main.Workspace!;

        var document = await workspace.OpenDocumentAsync(path);

        Assert.NotNull(document);
        Assert.False(document.HasLanguageServer);
        Assert.Equal("Pyright not installed", workspace.LanguageServerText);
        Assert.Contains("Python IntelliSense needs Pyright", workspace.StatusText, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        _main.Workspace?.Dispose();
        _services.Dispose();
        _temp.Dispose();
    }

    private async Task<(WorkspaceViewModel Workspace, DocumentViewModel Document)> OpenScriptAsync()
    {
        var folder = _temp.Resolve("scripts");
        _temp.Write("scripts/.tabstudio/settings.json", LoopbackLauncher.Settings);
        var path = _temp.Write("scripts/greet.loopback", Script);
        await _main.OpenWorkspaceAsync(folder);
        var workspace = _main.Workspace!;
        var document = await workspace.OpenDocumentAsync(path) ?? throw new InvalidOperationException("The script did not open.");
        await UiTestHelpers.WaitUntilAsync(() => document.LanguageServer?.ServerState == LanguageServerState.Running, because: "the loopback server");
        return (workspace, document);
    }
}
