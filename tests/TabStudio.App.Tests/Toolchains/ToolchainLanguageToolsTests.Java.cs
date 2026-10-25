using TabStudio.App.Services;
using TabStudio.App.ViewModels;
using TabStudio.LanguageServers.Features;
using TabStudio.LanguageServers.Hosting;
using TabStudio.Toolchains.Projects;

namespace TabStudio.App.Tests.Toolchains;

public sealed partial class ToolchainLanguageToolsTests
{
    [RealServerAvaloniaFact("java", "mvn", "jdtls")]
    public async Task A_Java_template_gets_completion_diagnostics_navigation_output_and_a_Java_debug_breakpoint()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(300));
        var window = Show();
        var (workspace, document) = await OpenTemplateAsync(ProjectTemplates.JavaId, "App.java");
        using var trace = new SavedLogs(workspace, "toolchains-java");
        await UiTestHelpers.WaitUntilAsync(() => document.LanguageServer?.ServerState is LanguageServerState.Running or LanguageServerState.Failed, TimeSpan.FromSeconds(180), because: "JDT");
        Assert.NotNull(document.LanguageServer);
        Assert.True(document.LanguageServer.ServerState == LanguageServerState.Running, workspace.LanguageServerOutput.Text);
        var server = document.LanguageServer!;
        var original = document.Document.Text;
        var offset = original.IndexOf("name.isEmpty", StringComparison.Ordinal) + 5;
        var completion = await AwaitCompletionsAsync(server, offset, i => i.Label.StartsWith("isEmpty", StringComparison.Ordinal), deadline.Token);
        Assert.Contains(completion!.Items, i => i.Label.StartsWith("isEmpty", StringComparison.Ordinal));
        var call = original.LastIndexOf("greet(name.isEmpty", StringComparison.Ordinal) + 1;
        Assert.NotEmpty(await server.GetDefinitionsAsync(call, deadline.Token));
        Assert.True((await server.FindReferencesAsync(call, true, deadline.Token)).Count >= 2);
        Assert.NotNull(await server.RenameAsync(call, "welcome", deadline.Token));

        document.Document.Text = original.Replace("""greet(name.isEmpty() ? "world" : name)""", "greet(undefined_name)", StringComparison.Ordinal);
        await UiTestHelpers.WaitUntilAsync(() => document.Diagnostics.Any(d => d.Message.Contains("undefined_name", StringComparison.Ordinal)), TimeSpan.FromSeconds(90), because: "Java's squiggle");
        await workspace.RunCommand.ExecuteAsync(null);
        Assert.Equal(IdeState.Failed, workspace.State);
        Assert.Contains(workspace.ErrorList.Items, e => e.Description.Contains("undefined_name", StringComparison.Ordinal) && e.File == "App.java");
        document.Document.Text = original;
        AnswerInput(workspace, "Ada");
        await workspace.RunCommand.ExecuteAsync(null);
        Assert.Contains("Hello, Ada! This is Java", workspace.Output.Text, StringComparison.Ordinal);
        Assert.Contains("has exited with code 0", workspace.Output.Text, StringComparison.Ordinal);
        Assert.Contains(workspace.Compatibility, r => r.ToolchainId == "java" && r.IsReady);

        var line = LineOf(document, "return message");
        workspace.Breakpoints.Toggle(document.FilePath, line);
        AnswerInput(workspace, "Ada");
        await workspace.StartDebuggingCommand.ExecuteAsync(null);
        Assert.True(workspace.IsDebugging, workspace.Output.Text);
        await UiTestHelpers.WaitUntilAsync(() => workspace.IsPaused, TimeSpan.FromSeconds(180), because: "Java debug breakpoint");
        await workspace.PauseShown;
        await UiTestHelpers.WaitUntilAsync(() => workspace.Locals.Tree.Rows.Any(r => r.Name == "answer"), because: "Java locals");
        Assert.Contains(workspace.Locals.Tree.Rows, r => r.Name == "answer" && r.Value.Contains("42", StringComparison.Ordinal));
        Assert.Contains(workspace.CallStack.Frames, f => f.MethodName.Contains("greet", StringComparison.Ordinal));
        await workspace.Watch.AddAsync("answer");
        Assert.Contains(workspace.Watch.Tree.Rows, r => r.Value.Contains("42", StringComparison.Ordinal));
        UiTestHelpers.SaveScreenshot(window, "toolchains-java-breakpoint");
        workspace.StartDebuggingCommand.Execute(null);
        await UiTestHelpers.WaitUntilAsync(() => !workspace.IsDebugging, TimeSpan.FromSeconds(60), because: "Java exit");
        Assert.Contains("Hello, Ada! This is Java", workspace.Output.Text, StringComparison.Ordinal);
        Assert.False(workspace.Output.IsAcceptingInput);
        trace.Passed = true;
    }

}
