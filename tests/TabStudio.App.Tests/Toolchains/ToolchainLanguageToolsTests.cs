using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using TabStudio.App.Services;
using TabStudio.App.Services.LanguageServers;
using TabStudio.App.Services.Toolchains;
using TabStudio.App.ViewModels;
using TabStudio.App.Views;
using TabStudio.LanguageServers.Hosting;
using TabStudio.Testing;
using TabStudio.Toolchains.Runtimes;
using static TabStudio.App.Tests.Views.EditorHarness;

namespace TabStudio.App.Tests.Toolchains;

/// <summary>
/// Python and Node.js projects with the language server and debug adapter their toolchain brings, on the computer's
/// own toolchains (the CI job installs Pyright, debugpy and js-debug): IntelliSense from Pyright through the
/// toolchain runtime, and F5 debugging to a breakpoint, with the program's input answered in the Output panel.
/// </summary>
public sealed partial class ToolchainLanguageToolsTests : IDisposable
{
    private const string NodeProgram = """
        const { createInterface } = require('node:readline/promises');

        function greet(name) {
          const message = `Hello, ${name}!`;
          return message;
        }

        async function main() {
          const rl = createInterface({ input: process.stdin, output: process.stdout });
          const name = (await rl.question('What is your name? ')).trim();
          console.log(greet(name));
          rl.close();
        }

        main();
        """;

    private readonly TempDirectory _temp = new();
    private readonly ServiceProvider _services;
    private readonly MainViewModel _main;

    public ToolchainLanguageToolsTests()
    {
        // Every message to and from the servers and adapters goes to their logs, which a failing test saves.
        _services = AppServices.Create(
            new AppPaths(_temp.Resolve("app")),
            services => services.AddSingleton(new LanguageServerServices(new ToolchainServerLauncher(new HostRuntime())) { TraceMessages = true }));
        _main = _services.GetRequiredService<MainViewModel>();
    }

    [RealServerAvaloniaFact("pyright-langserver")]
    public async Task A_Python_app_from_the_template_gets_Pyright_through_the_toolchain_runtime()
    {
        var window = Show();
        var (workspace, document) = await OpenTemplateAsync(ProjectTemplates.PythonId, "main.py");
        await UiTestHelpers.WaitUntilAsync(() => document.LanguageServer?.ServerState is LanguageServerState.Running or LanguageServerState.Failed, TimeSpan.FromSeconds(90), because: "Pyright to start");
        var view = ActiveEditor(window);
        var completion = view.Completion!;

        StartLineAfter(view.Editor, "print(greet(name))");
        Type(view.Editor, "print(nmae)");
        await UiTestHelpers.WaitUntilAsync(() => document.Diagnostics.Any(d => d.Message.Contains("\"nmae\" is not defined", StringComparison.Ordinal)), because: "Pyright's squiggle");
        StartLineAfter(view.Editor, "print(nmae)");
        Type(view.Editor, "platform.py");
        await UiTestHelpers.WaitUntilAsync(() => completion.IsOpen && completion.Items.Any(i => i.Entry.DisplayText == "python_version"), because: "the members of platform");
        await UiTestHelpers.WaitUntilAsync(() => completion.HasDescription && view.IsCompletionShown, TimeSpan.FromSeconds(20), because: "the description");

        UiTestHelpers.SaveScreenshot(window, "toolchains-lsp-01-python-pyright");

        Assert.IsType<ToolchainServerLauncher>(_services.GetRequiredService<IServerProcessLauncher>());
        Assert.Equal(LanguageServerState.Running, document.LanguageServer!.ServerState);
        Assert.Equal("Pyright ready", workspace.LanguageServerText);
        Assert.Null(workspace.ServerInstallOffer);
        Assert.Contains("[Pyright] Starting Pyright for ", workspace.LanguageServerOutput.Text, StringComparison.Ordinal);
        Assert.Contains(document.Diagnostics, d => d.Id == "reportUndefinedVariable" && d.Location?.Line == LineOf(document, "print(nmae)"));
    }

    [RealServerAvaloniaFact("python3", PythonModule = "debugpy")]
    public async Task F5_debugs_the_Python_app_to_a_breakpoint_and_its_input_is_answered()
    {
        var window = Show();
        var (workspace, document) = await OpenTemplateAsync(ProjectTemplates.PythonId, "main.py");
        var line = LineOf(document, "return f\"Hello, {name}!");
        workspace.Breakpoints.Toggle(document.FilePath, line);
        AnswerInput(workspace, "Ada");

        await workspace.StartDebuggingCommand.ExecuteAsync(null);
        await UiTestHelpers.WaitUntilAsync(() => workspace.IsPaused, TimeSpan.FromSeconds(90), because: "debugpy to stop at the breakpoint");
        await workspace.PauseShown;
        await UiTestHelpers.WaitUntilAsync(() => workspace.Locals.Tree.Rows.Any(r => r.Name == "name"), because: "the locals");
        await workspace.Watch.AddAsync("name.upper()");
        await UiTestHelpers.WaitUntilAsync(() => workspace.Watch.Tree.Rows.Any(r => r.Value == "'ADA'"), because: "the watch");

        UiTestHelpers.SaveScreenshot(window, "toolchains-lsp-02-python-breakpoint");

        Assert.True(workspace.IsDebuggingWithAdapter);
        Assert.Contains(workspace.Locals.Tree.Rows, r => r.Name == "name" && r.Value == "'Ada'");
        Assert.Equal("greet", workspace.CallStack.Frames[0].MethodName);
        Assert.Contains(workspace.CallStack.Frames, f => f.MethodName == "main");
        Assert.Equal(line, document.Document.GetLocation(document.CurrentStatement!.Start).Line);
        Assert.Contains("Debugging main.py with Python (debugpy)", workspace.Output.Text, StringComparison.Ordinal);
        Assert.Contains("What is your name? ", workspace.Output.Text, StringComparison.Ordinal);

        workspace.StartDebuggingCommand.Execute(null);
        await UiTestHelpers.WaitUntilAsync(() => !workspace.IsDebugging, TimeSpan.FromSeconds(60), because: "the end of the program");

        var output = workspace.Output.Text;
        Assert.Contains("Hello, Ada! This is Python 3.", output, StringComparison.Ordinal);
        Assert.Single(output.Split('\n'), l => l.Contains("Hello, Ada!", StringComparison.Ordinal));
        Assert.Contains("The program 'main.py' has exited with code 0 (0x0).", output, StringComparison.Ordinal);
        Assert.Equal(IdeState.Ready, workspace.State);
        Assert.False(workspace.Output.IsAcceptingInput);
    }

    [RealServerAvaloniaFact("python3", PythonModule = "debugpy")]
    public async Task Ctrl_F5_still_runs_the_Python_app_without_the_debugger()
    {
        var (workspace, document) = await OpenTemplateAsync(ProjectTemplates.PythonId, "main.py");
        workspace.Breakpoints.Toggle(document.FilePath, LineOf(document, "return f\"Hello, {name}!"));
        AnswerInput(workspace, "Ada");

        await workspace.RunCommand.ExecuteAsync(null);

        Assert.False(workspace.IsDebuggingWithAdapter);
        Assert.Contains("Running main.py with Python", workspace.Output.Text, StringComparison.Ordinal);
        Assert.Contains("Hello, Ada!", workspace.Output.Text, StringComparison.Ordinal);
    }

    [RealServerAvaloniaFact("node", "js-debug-adapter")]
    public async Task F5_debugs_a_Node_app_to_a_breakpoint_in_its_child_session()
    {
        var window = Show();
        var folder = _temp.Resolve("greeter");
        _temp.Write("greeter/package.json", """{ "name": "greeter", "version": "1.0.0", "scripts": { "start": "node app.js" } }""");
        var path = _temp.Write("greeter/app.js", NodeProgram);
        await _main.OpenWorkspaceAsync(folder);
        var workspace = _main.Workspace!;
        var document = await workspace.OpenDocumentAsync(path) ?? throw new InvalidOperationException("app.js did not open.");
        workspace.Breakpoints.Toggle(path, LineOf(document, "return message;"));
        AnswerInput(workspace, "Ada");
        using var trace = new SavedLogs(workspace, "toolchains-lsp-03-node-log");

        await workspace.StartDebuggingCommand.ExecuteAsync(null);
        await UiTestHelpers.WaitUntilAsync(() => workspace.IsPaused, TimeSpan.FromSeconds(90), because: "js-debug to stop at the breakpoint");
        await workspace.PauseShown;
        await UiTestHelpers.WaitUntilAsync(() => workspace.Locals.Tree.Rows.Any(r => r.Name == "message"), because: "the locals");

        UiTestHelpers.SaveScreenshot(window, "toolchains-lsp-03-node-breakpoint");

        Assert.Equal("Node.js toolchain", workspace.RunHostText);
        Assert.Contains(workspace.Locals.Tree.Rows, r => r.Name == "name" && r.Value.Contains("Ada", StringComparison.Ordinal));
        Assert.Contains(workspace.Locals.Tree.Rows, r => r.Name == "message" && r.Value.Contains("Hello, Ada!", StringComparison.Ordinal));
        Assert.EndsWith("greet", workspace.CallStack.Frames[0].MethodName, StringComparison.Ordinal);
        Assert.Contains("Debugging app.js with Node.js (js-debug)", workspace.Output.Text, StringComparison.Ordinal);

        workspace.StartDebuggingCommand.Execute(null);
        await UiTestHelpers.WaitUntilAsync(() => !workspace.IsDebugging, TimeSpan.FromSeconds(60), because: "the end of the program");

        var output = workspace.Output.Text;
        Assert.Single(output.Split('\n'), l => l.Contains("Hello, Ada!", StringComparison.Ordinal));
        Assert.Contains("The program 'app.js' has exited with code 0 (0x0).", output, StringComparison.Ordinal);
        trace.Passed = true;
    }

    public void Dispose()
    {
        _main.Workspace?.Dispose();
        _services.Dispose();
        _temp.Dispose();
    }

    /// <summary>
    /// Saves the Output panel, the debug adapters' log and the running processes beside the screenshots when a test
    /// fails, as CI publishes them.
    /// </summary>
    private sealed class SavedLogs(WorkspaceViewModel workspace, string name) : IDisposable
    {
        public bool Passed { get; set; }

        public void Dispose()
        {
            if (!Passed && Environment.GetEnvironmentVariable("TABSTUDIO_SCREENSHOTS") is { Length: > 0 } folder)
            {
                Directory.CreateDirectory(folder);
                var locals = string.Join("\n", workspace.Locals.Tree.Rows.Select(r => r.Name + " = " + r.Value));
                var stack = string.Join("\n", workspace.CallStack.Frames.Select(f => f.MethodName));
                File.WriteAllText(Path.Combine(folder, name + ".txt"), $"== Output\n{workspace.Output.Text}\n== State\n{workspace.State}: {workspace.StatusText}\n== Diagnostics\n{string.Join("\n", workspace.ActiveDocument?.Diagnostics.Select(d => d.Message) ?? [])}\n== Locals\n{locals}\n== Call Stack\n{stack}\n== Language Servers\n{workspace.LanguageServerOutput.Text}\n== Debug Adapters\n{workspace.DebugAdapterOutput.Text}\n== PATH\n{Environment.GetEnvironmentVariable("PATH")}\n== Processes\n{Processes()}");
            }
        }
    }

    private static string Processes()
    {
        try
        {
            using var ps = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("ps", ["-eo", "pid,ppid,pgid,stat,etime,args", "--forest"]) { RedirectStandardOutput = true })!;
            var text = ps.StandardOutput.ReadToEnd();
            ps.WaitForExit();
            return text;
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            return ex.Message;
        }
    }

    private static int LineOf(DocumentViewModel document, string text) =>
        document.Document.Lines.First(l => document.Document.GetText(l).Contains(text, StringComparison.Ordinal)).LineNumber;

    /// <summary>Answers the program's question in the Output panel's input box, as soon as it asks.</summary>
    private static void AnswerInput(WorkspaceViewModel workspace, string answer)
    {
        workspace.Output.PropertyChanged += Answer;

        void Answer(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(OutputViewModel.IsWaitingForInput) && workspace.Output.IsWaitingForInput)
            {
                workspace.Output.PropertyChanged -= Answer;
                workspace.Output.InputText = answer;
                workspace.Output.SubmitInputCommand.Execute(null);
            }
        }
    }

    private MainWindow Show()
    {
        var window = new MainWindow { DataContext = _main, Width = 1280, Height = 800 };
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        return window;
    }

    private async Task<(WorkspaceViewModel Workspace, DocumentViewModel Document)> OpenTemplateAsync(string template, string entry)
    {
        await _main.OpenWorkspaceAsync(_services.GetRequiredService<ProjectTemplates>().Create(template, "Greeter"));
        var workspace = _main.Workspace!;
        await UiTestHelpers.WaitUntilAsync(() => workspace.ActiveDocument?.FileName == entry, because: entry);
        return (workspace, workspace.ActiveDocument!);
    }
}
