using Avalonia.Headless.XUnit;
using TabStudio.App.Services;
using TabStudio.App.Tests.ViewModels;
using TabStudio.App.ViewModels;
using TabStudio.Testing;

namespace TabStudio.App.Tests.Toolchains;

/// <summary>F5 on Node.js projects, with the computer's own node and npm as the toolchain (CI's runners have them).</summary>
public sealed class NodeRunTests
{
    [AvaloniaFact]
    public async Task F5_runs_the_start_script_of_a_Node_app_that_reads_input()
    {
        using var fixture = new WorkspaceFixture();
        var folder = new ProjectTemplates(fixture.Paths).Create(ProjectTemplates.NodeId, "Greeter");
        fixture.AnswerInput("Ada");
        var workspace = fixture.Workspace;
        await workspace.LoadAsync(folder);
        await UiTestHelpers.WaitUntilAsync(() => workspace.ActiveDocument is not null, because: "index.js");

        Assert.Equal("Node.js toolchain", workspace.RunHostText);
        Assert.EndsWith("index.js", workspace.ActiveDocument!.FilePath, StringComparison.Ordinal);
        Assert.Null(workspace.ToolchainNotice);
        await workspace.RunCommand.ExecuteAsync(null);

        var output = workspace.Output.Text;
        Assert.Contains("Running npm start with Node.js", output, StringComparison.Ordinal);
        Assert.Contains("Hello, Ada! This is Node.js v", output, StringComparison.Ordinal);
        Assert.Contains("has exited with code 0", output, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task A_Node_error_lands_in_the_Error_List_at_the_line_that_threw()
    {
        using var fixture = new WorkspaceFixture();
        using var folder = new TempDirectory();
        folder.Write("package.json", """{ "name": "broken", "main": "app.js" }""");
        var app = folder.Write("app.js", "function check(value) {\n  if (!value) {\n    throw new TypeError('value is required');\n  }\n}\n\ncheck();\n");
        var workspace = fixture.Workspace;
        await workspace.LoadAsync(folder.Path);

        await workspace.RunCommand.ExecuteAsync(null);

        Assert.Equal(IdeState.Failed, workspace.State);
        var error = Assert.Single(workspace.ErrorList.Items);
        Assert.Equal("TypeError: value is required", error.Description);
        Assert.Equal(("app.js", "3"), (error.File, error.Line));
        var frame = workspace.Output.Text.Split('\n').First(l => l.Contains("at check (", StringComparison.Ordinal));
        Assert.Equal(app, Assert.Single(workspace.Output.LinkFinder!(frame)).FilePath);
    }

    [AvaloniaFact]
    public async Task Offers_npm_install_for_a_project_with_dependencies_and_runs_it()
    {
        using var fixture = new WorkspaceFixture();
        using var folder = new TempDirectory();
        folder.Write("package.json", """{ "name": "local", "main": "index.js", "dependencies": { "greeting": "file:./greeting" } }""");
        folder.Write("greeting/package.json", """{ "name": "greeting", "version": "1.0.0", "main": "index.js" }""");
        folder.Write("greeting/index.js", "module.exports = (name) => `Hi, ${name}!`;\n");
        folder.Write("index.js", "console.log(require('greeting')('npm'));\n");
        var workspace = fixture.Workspace;

        // With "Set up projects when they open" off, opening offers the install instead of running it.
        workspace.SetUpProjectsWhenTheyOpen = false;
        await workspace.LoadAsync(folder.Path);
        await UiTestHelpers.WaitUntilAsync(() => workspace.ToolchainNotice is not null, because: "the offer");

        var notice = workspace.ToolchainNotice!;
        Assert.Equal("npm install", notice.ActionText);
        await notice.Action.ExecuteAsync(null);

        Assert.Contains("npm install: succeeded", workspace.Output.Text, StringComparison.Ordinal);
        Assert.True(Directory.Exists(folder.Resolve("node_modules/greeting")));
        Assert.Null(workspace.ToolchainNotice);
        await workspace.RunCommand.ExecuteAsync(null);
        Assert.Contains("Hi, npm!", workspace.Output.Text, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task A_failed_npm_install_says_why_in_one_message_and_F5_warns_before_running()
    {
        using var fixture = new WorkspaceFixture();
        using var folder = new TempDirectory();
        // Its install script fails, so npm install fails, as a native module's build does.
        folder.Write("package.json", """{ "name": "local", "main": "index.js", "scripts": { "preinstall": "node -e \"console.error('cannot build'); process.exit(1)\"" }, "dependencies": { "greeting": "file:./greeting" } }""");
        folder.Write("greeting/package.json", """{ "name": "greeting", "version": "1.0.0", "main": "index.js" }""");
        folder.Write("greeting/index.js", "module.exports = (name) => `Hi, ${name}!`;\n");
        folder.Write("index.js", "console.log(require('greeting')('npm'));\n");
        var workspace = fixture.Workspace;
        workspace.SetUpProjectsWhenTheyOpen = false;
        await workspace.LoadAsync(folder.Path);
        await UiTestHelpers.WaitUntilAsync(() => workspace.ToolchainNotice is not null, because: "the offer");
        await workspace.ToolchainNotice!.Action.ExecuteAsync(null);

        // The project is named after its folder.
        var name = Path.GetFileName(folder.Path);
        Assert.True(workspace.Output.Text.Contains($"{name}'s packages were not installed: npm install", StringComparison.Ordinal), workspace.Output.Text);
        Assert.Equal($"{name}'s packages were not installed", workspace.ToolchainNotice?.Title);

        await workspace.RunCommand.ExecuteAsync(null);
        Assert.Contains($"{name}'s packages were not installed, so it may fail to start.", workspace.Output.Text, StringComparison.Ordinal);
    }
}
