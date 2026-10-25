using Microsoft.Extensions.DependencyInjection;
using TabStudio.App.Services;
using TabStudio.App.ViewModels;
using TabStudio.App.Views;
using TabStudio.LanguageServers.Hosting;
using TabStudio.Testing;
using static TabStudio.App.Tests.Views.EditorHarness;

namespace TabStudio.App.Tests.Views;

/// <summary>
/// A Python file in the IDE with the real Pyright and debugpy, which the CI job installs: the completion list, squiggles
/// and Quick Info from Pyright, and the debugger stopped at a breakpoint by debugpy with Locals and the Call Stack.
/// </summary>
public sealed class PythonScreenshotTests : IDisposable
{
    private const string Program = """
        import os


        def greet(name: str) -> str:
            message = "Hello, " + name
            return message


        print(greet("Ada"))
        print(mesage)
        """;

    private readonly TempDirectory _temp = new();
    private readonly ServiceProvider _services;
    private readonly MainViewModel _main;

    public PythonScreenshotTests()
    {
        _services = AppServices.Create(new AppPaths(_temp.Resolve("app")));
        _main = _services.GetRequiredService<MainViewModel>();
    }

    [RealServerAvaloniaFact("pyright-langserver")]
    public async Task Pyright_squiggles_an_undefined_name_and_lists_it()
    {
        var window = Show();
        var (workspace, document) = await OpenAsync("main.py", Program);
        ActiveEditor(window);

        await UiTestHelpers.WaitUntilAsync(() => document.Diagnostics.Any(d => d.Message.Contains("mesage", StringComparison.Ordinal)), because: "Pyright's diagnostic");
        workspace.SelectPanelCommand.Execute(BottomPanel.ErrorList);
        await UiTestHelpers.WaitUntilAsync(() => workspace.ErrorList.Items.Any(i => i.Code == "reportUndefinedVariable"), because: "the Error List row");

        UiTestHelpers.SaveScreenshot(window, "lsp-10-python-squiggles");

        var row = workspace.ErrorList.Items.First(i => i.Code == "reportUndefinedVariable");
        Assert.True(row.IsError);
        Assert.Equal("10", row.Line);
        Assert.Equal("Pyright ready", workspace.LanguageServerText);
    }

    [RealServerAvaloniaFact("pyright-langserver")]
    public async Task Pyright_completes_the_members_of_a_module()
    {
        var window = Show();
        await OpenAsync("main.py", Program);
        var view = ActiveEditor(window);
        var completion = view.Completion!;

        StartLineAfter(view.Editor, "print(greet(\"Ada\"))");
        Type(view.Editor, "os.pa");
        await UiTestHelpers.WaitUntilAsync(() => completion.IsOpen && completion.Items.Any(i => i.Entry.DisplayText == "path"), because: "the members of os");
        await UiTestHelpers.WaitUntilAsync(() => completion.HasDescription && view.IsCompletionShown, TimeSpan.FromSeconds(20), because: "the description");

        UiTestHelpers.SaveScreenshot(window, "lsp-11-python-completion");

        Assert.StartsWith("pa", completion.SelectedItem?.Entry.DisplayText, StringComparison.Ordinal);
    }

    [RealServerAvaloniaFact("pyright-langserver")]
    public async Task Pyright_quick_info_shows_the_signature()
    {
        var window = Show();
        var (workspace, _) = await OpenAsync("main.py", Program);
        var view = ActiveEditor(window);
        view.Editor.CaretOffset = OffsetOf(view.Editor, "greet(\"Ada\")") + 2;

        workspace.QuickInfoCommand.Execute(null);
        await UiTestHelpers.WaitUntilAsync(() => view.QuickInfo is not null, because: "Quick Info");

        UiTestHelpers.SaveScreenshot(window, "lsp-12-python-quick-info");

        var signature = string.Concat(view.QuickInfo!.Signature.Select(r => r.Text));
        Assert.Contains("greet(name: str) -> str", signature, StringComparison.Ordinal);
    }

    [RealServerAvaloniaFact("python3", PythonModule = "debugpy")]
    public async Task Debugpy_stops_at_a_breakpoint_with_Locals_and_the_Call_Stack()
    {
        var window = Show();
        var (workspace, document) = await OpenAsync("program.py", Program.Replace("print(mesage)", string.Empty, StringComparison.Ordinal), waitForServer: false);
        workspace.Breakpoints.Toggle(document.FilePath, 6);
        Assert.Equal("Python: Current File", workspace.RunLabel);

        await workspace.StartDebuggingCommand.ExecuteAsync(null);
        await UiTestHelpers.WaitUntilAsync(() => workspace.IsPaused, TimeSpan.FromSeconds(90), because: "debugpy to stop at the breakpoint");
        await workspace.PauseShown;
        await UiTestHelpers.WaitUntilAsync(() => workspace.Locals.Tree.Rows.Any(r => r.Name == "message"), because: "the locals");
        await workspace.Watch.AddAsync("len(message)");
        await UiTestHelpers.WaitUntilAsync(() => workspace.Watch.Tree.Rows.Any(r => r.Value == "10"), because: "the watch");

        UiTestHelpers.SaveScreenshot(window, "lsp-13-python-debugging");

        Assert.Contains(workspace.Locals.Tree.Rows, r => r.Name == "name" && r.Value == "'Ada'");
        Assert.Contains(workspace.Locals.Tree.Rows, r => r.Name == "message" && r.Value == "'Hello, Ada'");
        Assert.Equal("greet", workspace.CallStack.Frames[0].MethodName);
        Assert.Contains(workspace.CallStack.Frames, f => f.MethodName == "<module>");
        Assert.Equal(6, document.Document.GetLocation(document.CurrentStatement!.Start).Line);

        workspace.StartDebuggingCommand.Execute(null);
        await UiTestHelpers.WaitUntilAsync(() => !workspace.IsDebugging, TimeSpan.FromSeconds(60), because: "the end of the program");
        Assert.Contains("Hello, Ada", workspace.Output.Text, StringComparison.Ordinal);
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

    private async Task<(WorkspaceViewModel Workspace, DocumentViewModel Document)> OpenAsync(string name, string text, bool waitForServer = true)
    {
        var folder = _temp.Resolve("python");
        var path = _temp.Write("python/" + name, text + "\n");
        await _main.OpenWorkspaceAsync(folder);
        var workspace = _main.Workspace!;
        var document = await workspace.OpenDocumentAsync(path) ?? throw new InvalidOperationException("The file did not open.");
        if (!waitForServer)
        {
            return (workspace, document);
        }

        await UiTestHelpers.WaitUntilAsync(
            () => document.LanguageServer?.ServerState is LanguageServerState.Running or LanguageServerState.Failed,
            TimeSpan.FromSeconds(90),
            because: "Pyright to start");
        Assert.Equal(LanguageServerState.Running, document.LanguageServer!.ServerState);
        return (workspace, document);
    }
}
