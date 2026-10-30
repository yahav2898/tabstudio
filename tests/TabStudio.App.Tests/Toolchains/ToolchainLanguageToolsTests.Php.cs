using TabStudio.App.Services;
using TabStudio.App.ViewModels;
using TabStudio.LanguageServers.Hosting;
using static TabStudio.App.Tests.Views.EditorHarness;

namespace TabStudio.App.Tests.Toolchains;

public sealed partial class ToolchainLanguageToolsTests
{
    [RealServerAvaloniaFact("php", "composer", "phpactor", "php-debug-adapter")]
    public async Task A_PHP_template_is_set_up_by_Composer_and_gets_Phpactor_completion_build_errors_PHPUnit_and_an_Xdebug_breakpoint()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(6));
        var window = Show();
        var (workspace, document) = await OpenTemplateAsync(ProjectTemplates.PhpId, "main.php");
        using var trace = new SavedLogs(workspace, "toolchains-php");
        var root = workspace.Workspace!.RootDirectory;

        // Opening sets the project up: composer install brings PHPUnit and the class autoloader.
        await UiTestHelpers.WaitUntilAsync(() => workspace.ProjectSetupTask.IsCompleted && !workspace.IsSettingUpProjects, TimeSpan.FromMinutes(4), because: "composer install on open");
        Assert.True(File.Exists(Path.Combine(root, "vendor", "autoload.php")), workspace.Output.Text);
        Assert.True(File.Exists(Path.Combine(root, "vendor", "bin", "phpunit")), workspace.Output.Text);

        await UiTestHelpers.WaitUntilAsync(() => document.LanguageServer?.ServerState is LanguageServerState.Running or LanguageServerState.Failed, TimeSpan.FromSeconds(120), because: "Phpactor");
        Assert.True(document.LanguageServer?.ServerState == LanguageServerState.Running, workspace.LanguageServerOutput.Text);
        var server = document.LanguageServer!;
        var original = document.Document.Text;
        var typed = original.Replace("echo $greeter->greet(", "$greeter->gr\necho $greeter->greet(", StringComparison.Ordinal);
        document.Document.Text = typed;
        var offset = typed.IndexOf("$greeter->gr\n", StringComparison.Ordinal) + "$greeter->gr".Length;
        var completion = await AwaitCompletionsAsync(server, offset, i => i.Label.StartsWith("greet", StringComparison.Ordinal), deadline.Token);
        Assert.Contains(completion.Items, i => i.Label.StartsWith("greet", StringComparison.Ordinal));
        document.Document.Text = original;

        // IntelliSense in the editor, as a person sees it.
        var view = ActiveEditor(window);
        StartLineAfter(view.Editor, "$greeter = new Greeter();");
        Type(view.Editor, "$greeter->gr");
        await UiTestHelpers.WaitUntilAsync(() => view.Completion!.IsOpen && view.Completion.Items.Any(i => i.Entry.DisplayText.StartsWith("greet", StringComparison.Ordinal)), TimeSpan.FromSeconds(60), because: "Phpactor's completion list");
        UiTestHelpers.SaveScreenshot(window, "toolchains-php-completion");
        view.Completion!.Close();
        document.Document.Text = original;

        // Ctrl+Shift+B checks the syntax of the project's own files with php -l.
        document.Document.Text = original.Replace("$greeter = new Greeter();", "$greeter = new Greeter(;", StringComparison.Ordinal);
        await workspace.BuildCommand.ExecuteAsync(null);
        Assert.Equal(IdeState.Failed, workspace.State);
        Assert.Contains(workspace.ErrorList.Items, e => e.File == "main.php" && e.Description.Contains("syntax error", StringComparison.Ordinal));
        document.Document.Text = original;
        await workspace.BuildCommand.ExecuteAsync(null);
        Assert.Equal(IdeState.Ready, workspace.State);

        AnswerInput(workspace, "Ada");
        await workspace.RunCommand.ExecuteAsync(null);
        Assert.Contains("Hello, Ada! This is PHP", workspace.Output.Text, StringComparison.Ordinal);
        Assert.Contains("has exited with code 0", workspace.Output.Text, StringComparison.Ordinal);
        Assert.Contains(workspace.Compatibility, r => r.ToolchainId == "php" && r.IsReady);

        // PHPUnit in Test Explorer, found in source before it runs.
        await UiTestHelpers.WaitUntilAsync(() => workspace.Tests.Entries.Any(e => e.MethodName == "testGreetsByName"), because: "the PHPUnit test in source");
        await workspace.Tests.RunAllCommand.ExecuteAsync(null);
        Assert.Contains(workspace.Tests.Entries, e => e.MethodName == "testGreetsByName" && e.State == TestState.Passed);

        var greeter = Path.Combine(root, "src", "Greeter.php");
        var line = File.ReadAllLines(greeter).ToList().FindIndex(l => l.Contains("return sprintf", StringComparison.Ordinal)) + 1;
        workspace.Breakpoints.Toggle(greeter, line);
        AnswerInput(workspace, "Ada");
        await workspace.StartDebuggingCommand.ExecuteAsync(null);
        Assert.True(workspace.IsDebugging, workspace.Output.Text);
        await UiTestHelpers.WaitUntilAsync(() => workspace.IsPaused, TimeSpan.FromSeconds(120), because: "Xdebug's breakpoint");
        await workspace.PauseShown;
        await UiTestHelpers.WaitUntilAsync(() => workspace.Locals.Tree.Rows.Any(r => r.Name == "$answer"), because: "PHP locals");
        Assert.Contains(workspace.Locals.Tree.Rows, r => r.Name == "$answer" && r.Value.Contains("42", StringComparison.Ordinal));
        Assert.Contains(workspace.CallStack.Frames, f => f.MethodName.Contains("greet", StringComparison.Ordinal));
        UiTestHelpers.SaveScreenshot(window, "toolchains-php-breakpoint");
        workspace.StartDebuggingCommand.Execute(null);
        await UiTestHelpers.WaitUntilAsync(() => !workspace.IsDebugging, TimeSpan.FromSeconds(60), because: "PHP exit");
        Assert.Contains("Hello, Ada! This is PHP", workspace.Output.Text, StringComparison.Ordinal);

        // F5 reports the script's own exit code, which php-debug always reported as 0.
        workspace.Breakpoints.Toggle(greeter, line);
        document.Document.Text = original.TrimEnd() + "\nexit(3);\n";
        AnswerInput(workspace, "Ada");
        await workspace.StartDebuggingCommand.ExecuteAsync(null);
        await UiTestHelpers.WaitUntilAsync(() => !workspace.IsDebugging, TimeSpan.FromSeconds(60), because: "PHP exit(3)");
        Assert.Contains("has exited with code 3", workspace.Output.Text, StringComparison.Ordinal);
        document.Document.Text = original;
        trace.Passed = true;
    }
}
