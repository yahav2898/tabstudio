using TabStudio.App.Services;
using TabStudio.App.ViewModels;
using TabStudio.LanguageServers.Hosting;
using static TabStudio.App.Tests.Views.EditorHarness;

namespace TabStudio.App.Tests.Toolchains;

public sealed partial class ToolchainLanguageToolsTests
{
    [RealServerAvaloniaFact("kotlinc", "gradle", "kotlin-lsp", "jdtls")]
    public async Task A_Kotlin_Gradle_template_gets_kotlin_lsp_completion_build_errors_JUnit_and_a_JVM_breakpoint()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(8));
        var window = Show();
        var (workspace, document) = await OpenTemplateAsync(ProjectTemplates.KotlinId, "Main.kt");
        using var trace = new SavedLogs(workspace, "toolchains-kotlin");
        await UiTestHelpers.WaitUntilAsync(() => document.LanguageServer?.ServerState is LanguageServerState.Running or LanguageServerState.Failed, TimeSpan.FromMinutes(3), because: "kotlin-lsp");
        Assert.True(document.LanguageServer?.ServerState == LanguageServerState.Running, workspace.LanguageServerOutput.Text);
        var server = document.LanguageServer!;
        var original = document.Document.Text;

        // kotlin-lsp imports the Gradle build before it completes the standard library's members.
        var typed = original.Replace("    val answer = 42", "    name.leng\n    val answer = 42", StringComparison.Ordinal);
        document.Document.Text = typed;
        var offset = typed.IndexOf("name.leng\n", StringComparison.Ordinal) + "name.leng".Length;
        var completion = await AwaitCompletionsAsync(server, offset, i => i.Label.StartsWith("length", StringComparison.Ordinal), deadline.Token);
        Assert.Contains(completion.Items, i => i.Label.StartsWith("length", StringComparison.Ordinal));
        document.Document.Text = original;

        var view = ActiveEditor(window);
        StartLineAfter(view.Editor, "val answer = 42");
        Type(view.Editor, "name.leng");
        await UiTestHelpers.WaitUntilAsync(() => view.Completion!.IsOpen && view.Completion.Items.Any(i => i.Entry.DisplayText.StartsWith("length", StringComparison.Ordinal)), TimeSpan.FromSeconds(90), because: "kotlin-lsp's completion list");
        UiTestHelpers.SaveScreenshot(window, "toolchains-kotlin-completion");
        view.Completion!.Close();
        document.Document.Text = original;

        // A compiler error reaches the Error List, from Ctrl+Shift+B and from F5.
        document.Document.Text = original.Replace("""greet(name.ifEmpty { "world" })""", "greet(undefined_name)", StringComparison.Ordinal);
        await workspace.BuildCommand.ExecuteAsync(null);
        Assert.Equal(IdeState.Failed, workspace.State);
        Assert.Contains(workspace.ErrorList.Items, e => e.Description.Contains("undefined_name", StringComparison.Ordinal) && e.File == "Main.kt");
        document.Document.Text = original;

        AnswerInput(workspace, "Ada");
        await workspace.RunCommand.ExecuteAsync(null);
        Assert.Contains("Hello, Ada! This is Kotlin", workspace.Output.Text, StringComparison.Ordinal);
        Assert.Contains("has exited with code 0", workspace.Output.Text, StringComparison.Ordinal);
        Assert.Contains(workspace.Compatibility, r => r.ToolchainId == "kotlin" && r.IsReady);

        // JUnit in Test Explorer: found in source, run by Gradle's test task.
        await UiTestHelpers.WaitUntilAsync(() => workspace.Tests.Entries.Any(e => e.MethodName == "greets by name"), because: "the JUnit test in source");
        await workspace.Tests.RunAllCommand.ExecuteAsync(null);
        Assert.Contains(workspace.Tests.Entries, e => e.MethodName == "greets by name" && e.State == TestState.Passed);

        // Kotlin breakpoints through the Java toolchain's debugger.
        var line = LineOf(document, "return message");
        workspace.Breakpoints.Toggle(document.FilePath, line);
        AnswerInput(workspace, "Ada");
        await workspace.StartDebuggingCommand.ExecuteAsync(null);
        Assert.True(workspace.IsDebugging, workspace.Output.Text);
        await UiTestHelpers.WaitUntilAsync(() => workspace.IsPaused, TimeSpan.FromSeconds(180), because: "the Kotlin breakpoint");
        await workspace.PauseShown;
        await UiTestHelpers.WaitUntilAsync(() => workspace.Locals.Tree.Rows.Any(r => r.Name == "answer"), because: "Kotlin locals");
        Assert.Contains(workspace.Locals.Tree.Rows, r => r.Name == "answer" && r.Value.Contains("42", StringComparison.Ordinal));
        Assert.Contains(workspace.CallStack.Frames, f => f.MethodName.Contains("greet", StringComparison.Ordinal));
        Assert.EndsWith("Main.kt", workspace.ActiveDocument?.FilePath, StringComparison.Ordinal);
        Assert.Contains(workspace.CallStack.Frames, f => f.LocationText == $"Main.kt, line {line}");
        UiTestHelpers.SaveScreenshot(window, "toolchains-kotlin-breakpoint");
        workspace.StartDebuggingCommand.Execute(null);
        await UiTestHelpers.WaitUntilAsync(() => !workspace.IsDebugging, TimeSpan.FromSeconds(60), because: "Kotlin exit");
        Assert.Contains("Hello, Ada! This is Kotlin", workspace.Output.Text, StringComparison.Ordinal);
        trace.Passed = true;
    }
}
