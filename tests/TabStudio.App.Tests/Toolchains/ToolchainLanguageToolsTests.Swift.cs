using TabStudio.App.Services;
using TabStudio.App.ViewModels;
using TabStudio.LanguageServers.Hosting;
using static TabStudio.App.Tests.Views.EditorHarness;

namespace TabStudio.App.Tests.Toolchains;

public sealed partial class ToolchainLanguageToolsTests
{
    [RealServerAvaloniaFact("swift", "sourcekit-lsp", "lldb-dap")]
    public async Task A_Swift_package_template_gets_sourcekit_lsp_completion_build_errors_swift_test_and_an_LLDB_breakpoint()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(6));
        var window = Show();
        var (workspace, document) = await OpenTemplateAsync(ProjectTemplates.SwiftId, "main.swift");
        using var trace = new SavedLogs(workspace, "toolchains-swift");
        await UiTestHelpers.WaitUntilAsync(() => document.LanguageServer?.ServerState is LanguageServerState.Running or LanguageServerState.Failed, TimeSpan.FromSeconds(120), because: "sourcekit-lsp");
        Assert.True(document.LanguageServer?.ServerState == LanguageServerState.Running, workspace.LanguageServerOutput.Text);
        var server = document.LanguageServer!;
        var original = document.Document.Text;
        var typed = original.Replace("print(greet(", "name.cou\nprint(greet(", StringComparison.Ordinal);
        document.Document.Text = typed;
        var offset = typed.IndexOf("name.cou\n", StringComparison.Ordinal) + "name.cou".Length;
        var completion = await AwaitCompletionsAsync(server, offset, i => i.Label.StartsWith("count", StringComparison.Ordinal), deadline.Token);
        Assert.Contains(completion.Items, i => i.Label.StartsWith("count", StringComparison.Ordinal));
        document.Document.Text = original;

        var view = ActiveEditor(window);
        StartLineAfter(view.Editor, "let name = ");
        Type(view.Editor, "name.co");
        bool Listed() => view.Completion!.IsOpen && view.Completion.Items.Any(i => i.Entry.DisplayText.StartsWith("count", StringComparison.Ordinal));
        for (var attempt = 0; attempt < 3 && !Listed(); attempt++)
        {
            // sourcekit-lsp may still be indexing the package after a rebuild: type the last letter again.
            if (attempt > 0)
            {
                view.Editor.Document.Remove(view.Editor.CaretOffset - 1, 1);
            }

            Type(view.Editor, "u");
            var end = DateTime.UtcNow.AddSeconds(30);
            while (!Listed() && DateTime.UtcNow < end)
            {
                await Task.Delay(100);
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            }
        }

        Assert.True(Listed(), "sourcekit-lsp's completion list did not open in the editor.");
        UiTestHelpers.SaveScreenshot(window, "toolchains-swift-completion");
        view.Completion!.Close();
        document.Document.Text = original;

        document.Document.Text = original.Replace("print(greet(", "print(undefined_name(", StringComparison.Ordinal);
        await workspace.BuildCommand.ExecuteAsync(null).WaitAsync(TimeSpan.FromMinutes(5));
        Assert.Equal(IdeState.Failed, workspace.State);
        Assert.Contains(workspace.ErrorList.Items, e => e.Description.Contains("undefined_name", StringComparison.Ordinal) && e.File == "main.swift");
        document.Document.Text = original;

        AnswerInput(workspace, "Ada");
        await workspace.RunCommand.ExecuteAsync(null).WaitAsync(TimeSpan.FromMinutes(5));
        Assert.Contains("Hello, Ada! This is Swift", workspace.Output.Text, StringComparison.Ordinal);
        Assert.Contains("has exited with code 0", workspace.Output.Text, StringComparison.Ordinal);
        Assert.Contains(workspace.Compatibility, r => r.ToolchainId == "swift" && r.IsReady);

        // Swift Testing in Test Explorer, run by swift test.
        await UiTestHelpers.WaitUntilAsync(() => workspace.Tests.Entries.Any(e => e.MethodName == "greetsByName"), because: "the Swift test in source");
        await workspace.Tests.RunAllCommand.ExecuteAsync(null).WaitAsync(TimeSpan.FromMinutes(5));
        Assert.Contains(workspace.Tests.Entries, e => e.MethodName == "greetsByName" && e.State == TestState.Passed);

        var library = Path.Combine(workspace.Workspace!.RootDirectory, "Sources", "GreeterCore", "Greeting.swift");
        var line = File.ReadAllLines(library).ToList().FindIndex(l => l.Contains("return message", StringComparison.Ordinal)) + 1;
        workspace.Breakpoints.Toggle(library, line);
        AnswerInput(workspace, "Ada");
        await workspace.StartDebuggingCommand.ExecuteAsync(null).WaitAsync(TimeSpan.FromMinutes(5));
        Assert.True(workspace.IsDebugging, workspace.Output.Text);
        await UiTestHelpers.WaitUntilAsync(() => workspace.IsPaused, TimeSpan.FromSeconds(120), because: "LLDB's breakpoint");
        await workspace.PauseShown;
        await UiTestHelpers.WaitUntilAsync(() => workspace.Locals.Tree.Rows.Any(r => r.Name == "answer"), because: "Swift locals");
        Assert.Contains(workspace.Locals.Tree.Rows, r => r.Name == "answer" && r.Value.Contains("42", StringComparison.Ordinal));
        Assert.Contains(workspace.CallStack.Frames, f => f.MethodName.Contains("greet", StringComparison.Ordinal));
        UiTestHelpers.SaveScreenshot(window, "toolchains-swift-breakpoint");
        workspace.StartDebuggingCommand.Execute(null);
        await UiTestHelpers.WaitUntilAsync(() => !workspace.IsDebugging, TimeSpan.FromSeconds(60), because: "Swift exit");
        Assert.Contains("Hello, Ada! This is Swift", workspace.Output.Text, StringComparison.Ordinal);
        trace.Passed = true;
    }
}
