using Avalonia.Headless.XUnit;
using Microsoft.Extensions.DependencyInjection;
using TabStudio.App.Services;
using TabStudio.App.ViewModels;
using TabStudio.App.Views;
using TabStudio.LanguageServers.Hosting;
using TabStudio.LanguageServers.Loopback;
using TabStudio.Testing;
using static TabStudio.App.Tests.Views.EditorHarness;

namespace TabStudio.App.Tests.Views;

/// <summary>
/// Language servers and debug adapters in the IDE, with the built-in loopback server and adapter, which run in the
/// app: squiggles and the Error List, completion, Quick Info, the debugger paused in a script, and the servers' log.
/// </summary>
public sealed class LanguageServerScreenshotTests : IDisposable
{
    private const string Script = """
        # Greets the user, as a loopback script.
        let greeting = "Hello"
        let name = "Ada"
        print greeting
        print name
        print nobody
        """;

    private readonly TempDirectory _temp = new();
    private readonly ServiceProvider _services;
    private readonly MainViewModel _main;

    public LanguageServerScreenshotTests()
    {
        _services = AppServices.Create(new AppPaths(_temp.Resolve("app")));
        _main = _services.GetRequiredService<MainViewModel>();
    }

    [AvaloniaFact]
    public async Task Squiggles_and_the_Error_List_with_the_server_codes()
    {
        var window = Show();
        var (workspace, document) = await OpenScriptAsync(Script);
        ActiveEditor(window);

        await UiTestHelpers.WaitUntilAsync(() => workspace.ErrorList.Items.Any(i => i.Code == "LB0001"), because: "the server's diagnostic in the Error List");
        workspace.SelectPanelCommand.Execute(BottomPanel.ErrorList);
        await UiTestHelpers.WaitUntilAsync(() => true);

        UiTestHelpers.SaveScreenshot(window, "lsp-01-squiggles-error-list");

        var row = workspace.ErrorList.Items.Single(i => i.Code == "LB0001");
        Assert.Equal("'nobody' is not defined.", row.Description);
        Assert.Equal("6", row.Line);
        Assert.True(row.HasHelpLink);
        Assert.Contains(document.Diagnostics, d => d.Id == "LB0001");
        Assert.Equal("Loopback Server ready", workspace.LanguageServerText);
    }

    [AvaloniaFact]
    public async Task Completion_lists_the_names_the_script_set()
    {
        var window = Show();
        var (_, document) = await OpenScriptAsync(Script);
        var view = ActiveEditor(window);
        var completion = view.Completion!;

        StartLineAfter(view.Editor, "print name");
        Type(view.Editor, "print gre");
        await UiTestHelpers.WaitUntilAsync(() => completion.IsOpen && completion.HasDescription, because: "the list and its description");

        UiTestHelpers.SaveScreenshot(window, "lsp-02-completion");

        Assert.Equal("greeting", completion.SelectedItem?.Entry.DisplayText);
        Assert.True(document.HasLanguageServer);
    }

    [AvaloniaFact]
    public async Task Quick_info_of_a_name()
    {
        var window = Show();
        var (workspace, _) = await OpenScriptAsync(Script);
        var view = ActiveEditor(window);
        view.Editor.CaretOffset = OffsetOf(view.Editor, "print greeting") + "print gree".Length;

        workspace.QuickInfoCommand.Execute(null);
        await UiTestHelpers.WaitUntilAsync(() => view.QuickInfo is not null, because: "Quick Info");

        UiTestHelpers.SaveScreenshot(window, "lsp-03-quick-info");

        Assert.True(view.QuickInfo!.HasSignature);
        Assert.Contains(view.QuickInfo.Sections, s => string.Concat(s.Runs.Select(r => r.Text)).Contains("line 2", StringComparison.Ordinal));
    }

    [AvaloniaFact]
    public async Task Paused_in_a_script_with_Locals_and_the_Call_Stack()
    {
        var window = Show();
        var (workspace, document) = await OpenScriptAsync(Script.Replace("print nobody", "print \"bye\"", StringComparison.Ordinal));
        workspace.Breakpoints.Toggle(document.FilePath, 5);

        await workspace.StartDebuggingCommand.ExecuteAsync(null);
        await UiTestHelpers.WaitUntilAsync(() => workspace.IsPaused, because: "the breakpoint");
        await workspace.PauseShown;
        await UiTestHelpers.WaitUntilAsync(() => workspace.Locals.Tree.Rows.Count == 2 && document.CurrentStatement is not null, because: "the locals and the current statement");

        UiTestHelpers.SaveScreenshot(window, "lsp-04-debugging");

        Assert.Equal(new[] { "greeting", "name" }, workspace.Locals.Tree.Rows.Select(r => r.Name));
        Assert.Equal("\"Ada\"", workspace.Locals.Tree.Rows[1].Value);
        Assert.True(workspace.Locals.Tree.Rows[1].IsEditable);
        var frame = Assert.Single(workspace.CallStack.Frames);
        Assert.Equal(5, frame.Location?.Line);
        Assert.StartsWith("Paused at a breakpoint", workspace.StatusText, StringComparison.Ordinal);

        workspace.StartDebuggingCommand.Execute(null);
        await UiTestHelpers.WaitUntilAsync(() => !workspace.IsDebugging, because: "the end of the script");
        Assert.Contains("Ada", workspace.Output.Text, StringComparison.Ordinal);
        Assert.Contains("has exited with code 0", workspace.Output.Text, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task The_language_servers_log_in_the_Output_panel()
    {
        var window = Show();
        var (workspace, _) = await OpenScriptAsync(Script);
        await UiTestHelpers.WaitUntilAsync(() => workspace.LanguageServerOutput.Text.Contains("Starting Loopback Server", StringComparison.Ordinal), because: "the log");

        workspace.ShowLanguageServerLogCommand.Execute(null);
        await UiTestHelpers.WaitUntilAsync(() => true);

        UiTestHelpers.SaveScreenshot(window, "lsp-05-output-language-servers");

        Assert.True(workspace.IsOutputSelected);
        Assert.Same(workspace.LanguageServerOutput, workspace.ShownOutput);
        Assert.Equal("Language Servers", workspace.SelectedOutputSource?.Name);
    }

    public void Dispose()
    {
        _main.Workspace?.Dispose();
        _services.Dispose();
        _temp.Dispose();
    }

    private MainWindow Show()
    {
        var window = new MainWindow { DataContext = _main, Width = 1280, Height = 800 };
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        return window;
    }

    private async Task<(WorkspaceViewModel Workspace, DocumentViewModel Document)> OpenScriptAsync(string text)
    {
        var folder = _temp.Resolve("scripts");
        _temp.Write("scripts/.tabstudio/settings.json", LoopbackLauncher.Settings);
        var path = _temp.Write("scripts/greet.loopback", text);
        await _main.OpenWorkspaceAsync(folder);
        var workspace = _main.Workspace!;
        var document = await workspace.OpenDocumentAsync(path) ?? throw new InvalidOperationException("The script did not open.");
        await UiTestHelpers.WaitUntilAsync(() => document.LanguageServer?.ServerState == LanguageServerState.Running, because: "the loopback server");
        return (workspace, document);
    }
}
