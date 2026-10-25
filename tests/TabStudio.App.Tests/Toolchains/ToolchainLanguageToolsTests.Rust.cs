using TabStudio.App.Services;
using TabStudio.App.ViewModels;
using TabStudio.LanguageServers.Features;
using TabStudio.LanguageServers.Hosting;
using TabStudio.Toolchains.Projects;

namespace TabStudio.App.Tests.Toolchains;

public sealed partial class ToolchainLanguageToolsTests
{
    [RealServerAvaloniaFact("cargo", "rust-analyzer", "lldb-dap")]
    public async Task A_Rust_template_gets_completion_diagnostics_navigation_output_and_an_LLDB_breakpoint()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        var window = Show();
        var (workspace, document) = await OpenTemplateAsync(ProjectTemplates.RustId, "main.rs");
        using var trace = new SavedLogs(workspace, "toolchains-rust");
        await UiTestHelpers.WaitUntilAsync(() => document.LanguageServer?.ServerState is LanguageServerState.Running or LanguageServerState.Failed, TimeSpan.FromSeconds(120), because: "rust-analyzer");
        Assert.NotNull(document.LanguageServer);
        Assert.True(document.LanguageServer.ServerState == LanguageServerState.Running, workspace.LanguageServerOutput.Text);
        var server = document.LanguageServer!;
        var original = document.Document.Text;
        var offset = original.IndexOf("name.trim", StringComparison.Ordinal) + 5;
        var completion = await AwaitCompletionsAsync(server, offset, i => i.Label.StartsWith("trim", StringComparison.Ordinal), deadline.Token);
        Assert.Contains(completion!.Items, i => i.Label.StartsWith("trim", StringComparison.Ordinal));
        var call = original.LastIndexOf("greet(if", StringComparison.Ordinal) + 1;
        Assert.NotEmpty(await server.GetDefinitionsAsync(call, deadline.Token));
        Assert.True((await server.FindReferencesAsync(call, true, deadline.Token)).Count >= 2);
        Assert.NotNull(await server.RenameAsync(call, "welcome", deadline.Token));

        document.Document.Text = original.Replace("""greet(if name.is_empty() { "world" } else { name })""", "greet(undefined_name)", StringComparison.Ordinal);
        await workspace.SaveCommand.ExecuteAsync(null);
        await UiTestHelpers.WaitUntilAsync(() => document.Diagnostics.Any(d => d.Message.Contains("undefined_name", StringComparison.Ordinal)), TimeSpan.FromSeconds(90), because: "Rust's squiggle");
        await workspace.RunCommand.ExecuteAsync(null);
        Assert.Equal(IdeState.Failed, workspace.State);
        Assert.Contains(workspace.ErrorList.Items, e => e.Description.Contains("undefined_name", StringComparison.Ordinal) && e.File == "main.rs");
        document.Document.Text = original;
        AnswerInput(workspace, "Ada");
        await workspace.RunCommand.ExecuteAsync(null);
        Assert.Contains("Hello, Ada! This is Rust", workspace.Output.Text, StringComparison.Ordinal);
        Assert.Contains("has exited with code 0", workspace.Output.Text, StringComparison.Ordinal);
        Assert.Contains(workspace.Compatibility, r => r.ToolchainId == "rust" && r.IsReady);

        var line = LineOf(document, "let message = format!");
        workspace.Breakpoints.Toggle(document.FilePath, line);
        AnswerInput(workspace, "Ada");
        await workspace.StartDebuggingCommand.ExecuteAsync(null);
        Assert.True(workspace.IsDebugging, workspace.Output.Text);
        await UiTestHelpers.WaitUntilAsync(() => workspace.IsPaused, TimeSpan.FromSeconds(120), because: "LLDB's breakpoint");
        await workspace.PauseShown;
        await UiTestHelpers.WaitUntilAsync(() => workspace.Locals.Tree.Rows.Any(r => r.Name == "answer"), because: "Rust locals");
        Assert.Contains(workspace.Locals.Tree.Rows, r => r.Name == "answer" && r.Value.Contains("42", StringComparison.Ordinal));
        Assert.Contains(workspace.CallStack.Frames, f => f.MethodName.Contains("greet", StringComparison.Ordinal));
        await workspace.Watch.AddAsync("answer");
        Assert.Contains(workspace.Watch.Tree.Rows, r => r.Value.Contains("42", StringComparison.Ordinal));
        UiTestHelpers.SaveScreenshot(window, "toolchains-rust-breakpoint");
        workspace.Breakpoints.Toggle(document.FilePath, line);
        workspace.StartDebuggingCommand.Execute(null);
        await UiTestHelpers.WaitUntilAsync(() => !workspace.IsDebugging, TimeSpan.FromSeconds(60), because: "Rust exit");
        Assert.Contains("Hello, Ada! This is Rust", workspace.Output.Text, StringComparison.Ordinal);
        Assert.False(workspace.Output.IsAcceptingInput);
        trace.Passed = true;
    }

}
