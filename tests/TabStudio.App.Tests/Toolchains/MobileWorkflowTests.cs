using Avalonia.Headless.XUnit;
using TabStudio.App.Services;
using TabStudio.App.Tests.ViewModels;
using TabStudio.App.ViewModels;
using TabStudio.SourceControl.GitHub;
using TabStudio.Testing;

namespace TabStudio.App.Tests.Toolchains;

public sealed class MobileWorkflowTests
{
    [AvaloniaFact]
    public async Task A_Node_servers_port_is_offered_and_opens_in_the_existing_web_preview()
    {
        using var fixture = new WorkspaceFixture();
        using var temp = new TempDirectory();
        temp.Write("package.json", """{ "name": "server", "main": "index.js" }""");
        temp.Write("index.js", """
            const http = require('node:http');
            const server = http.createServer((req, res) => res.end('hello from Node'));
            server.listen(0, '127.0.0.1', () => console.log(`Listening on http://127.0.0.1:${server.address().port}`));
            """);
        var workspace = fixture.Workspace;
        await workspace.LoadAsync(temp.Path);
        var run = workspace.RunCommand.ExecuteAsync(null);
        try
        {
            await UiTestHelpers.WaitUntilAsync(() => workspace.ServerNotice is not null, because: "the server port notice");
            var notice = workspace.ServerNotice!;
            Assert.Equal($"App listening on port {notice.Port}", notice.Title);
            notice.OpenPreview.Execute(null);
            Assert.True(workspace.WebPreview.IsRunning);
            Assert.Equal(notice.Port, workspace.WebPreview.AppAddress!.Port);
            Assert.True(workspace.IsWebPreviewSelected);
            using var http = new HttpClient();
            Assert.Equal("hello from Node", await http.GetStringAsync(workspace.WebPreview.AppAddress));
        }
        finally
        {
            workspace.StopCommand.Execute(null);
            await run;
        }

        Assert.Null(workspace.ServerNotice);
        Assert.False(workspace.WebPreview.IsRunning);
    }

    [AvaloniaFact]
    public async Task The_Capacitor_templates_real_Vite_web_part_runs_in_the_preview()
    {
        using var fixture = new WorkspaceFixture();
        var folder = new ProjectTemplates(fixture.Paths).Create(ProjectTemplates.CapacitorId, "PocketApp");
        var workspace = fixture.Workspace;
        await workspace.LoadAsync(folder);
        await UiTestHelpers.WaitUntilAsync(() => workspace.ToolchainNotice is not null, because: "npm install offer");
        await workspace.ToolchainNotice!.Action.ExecuteAsync(null);
        Assert.True(Directory.Exists(Path.Combine(folder, "node_modules", "vite")), workspace.Output.Text);
        var run = workspace.RunCommand.ExecuteAsync(null);
        try
        {
            await UiTestHelpers.WaitUntilAsync(() => workspace.ServerNotice is not null, because: "Vite's local URL");
            workspace.ServerNotice!.OpenPreview.Execute(null);
            using var http = new HttpClient();
            var html = await http.GetStringAsync(workspace.WebPreview.AppAddress);
            Assert.Contains("Built on my tablet", html, StringComparison.Ordinal);
            Assert.Contains("/src/main.ts", html, StringComparison.Ordinal);
        }
        finally
        {
            workspace.StopCommand.Execute(null);
            await run;
        }
    }

    [AvaloniaFact]
    public async Task Build_for_iOS_writes_the_reviewable_workflow_and_explains_every_secret()
    {
        using var fixture = new WorkspaceFixture();
        var folder = new ProjectTemplates(fixture.Paths).Create(ProjectTemplates.CapacitorId, "PocketApp");
        await fixture.Workspace.LoadAsync(folder);
        await fixture.Workspace.BuildForIosCommand.ExecuteAsync(null);
        var yaml = await File.ReadAllTextAsync(Path.Combine(folder, IosWorkflowGenerator.WorkflowPath));
        Assert.Equal(IosWorkflowGenerator.Generate(new IosWorkflowOptions(true)), yaml);
        Assert.True(WorkflowFile.TryParse(yaml, out var definition, out var error), error);
        Assert.True(definition!.CanDispatch);
        foreach (var secret in IosWorkflowGenerator.Secrets)
        {
            Assert.Contains(secret.Name, fixture.Workspace.Output.Text, StringComparison.Ordinal);
        }

        Assert.True(File.Exists(Path.Combine(folder, "docs", "IOS-BUILD.md")));
    }
}
