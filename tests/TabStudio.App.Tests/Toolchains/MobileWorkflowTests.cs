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
    public async Task A_CSharp_F5_server_offers_its_real_listener_in_the_browser_until_the_debug_run_ends()
    {
        var browser = new FakeBrowser();
        using var fixture = new WorkspaceFixture(browser: browser);
        await fixture.OpenSampleAsync();
        var workspace = fixture.Workspace;
        workspace.ActiveDocument!.Document.Text = """
            var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
            listener.Start();
            try
            {
                int port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
                Console.WriteLine($"Listening on http://127.0.0.1:{port}");
                Console.ReadLine();
            }
            finally
            {
                listener.Stop();
            }
            """;
        await workspace.StartDebuggingCommand.ExecuteAsync(null);
        try
        {
            await UiTestHelpers.WaitUntilAsync(() => workspace.ServerNotice is not null && workspace.Output.IsWaitingForInput,
                because: "the C# debugger's live server notice and input wait");
            var notice = workspace.ServerNotice!;
            using var client = new System.Net.Sockets.TcpClient();
            await client.ConnectAsync("127.0.0.1", notice.Port);
            Assert.True(workspace.IsDebugging);
            await notice.OpenBrowser.ExecuteAsync(null);
            Assert.Equal(new Uri($"http://127.0.0.1:{notice.Port}/"), Assert.Single(browser.Opened));
            Assert.Same(notice, workspace.ServerNotice);

            workspace.Output.InputText = "done";
            workspace.Output.SubmitInputCommand.Execute(null);
            await DebuggingTests.WaitForExitAsync(workspace);
            Assert.Null(workspace.ServerNotice);
            Assert.Contains("has exited with code 0", workspace.Output.Text, StringComparison.Ordinal);
        }
        finally
        {
            workspace.StopDebuggingCommand.Execute(null);
            await DebuggingTests.WaitForExitAsync(workspace);
        }
    }

    [AvaloniaFact]
    public async Task A_Node_dev_server_on_IPv6_loopback_alone_opens_in_the_web_preview()
    {
        // Vite and Next.js bind "localhost", which is ::1 alone on Android's Alpine environment and most Linux computers.
        if (!System.Net.Sockets.Socket.OSSupportsIPv6)
        {
            return;
        }

        using var fixture = new WorkspaceFixture();
        using var temp = new TempDirectory();
        temp.Write("package.json", """{ "name": "server", "main": "index.js" }""");
        temp.Write("index.js", """
            const http = require('node:http');
            const server = http.createServer((req, res) => res.end('hello over IPv6'));
            server.listen(0, '::1', () => console.log(`Local: http://localhost:${server.address().port}/`));
            """);
        var workspace = fixture.Workspace;
        await workspace.LoadAsync(temp.Path);
        var run = workspace.RunCommand.ExecuteAsync(null);
        try
        {
            await UiTestHelpers.WaitUntilAsync(() => workspace.ServerNotice is not null, because: "the server port notice");
            workspace.ServerNotice!.OpenPreview.Execute(null);
            Assert.Equal("[::1]", workspace.WebPreview.AppAddress!.Host);
            using var http = new HttpClient();
            Assert.Equal("hello over IPv6", await http.GetStringAsync(workspace.WebPreview.AppAddress));
        }
        finally
        {
            workspace.StopCommand.Execute(null);
            await run;
        }
    }

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
        // Exercise the explicit install offer; automatic setup is covered by ProjectSetupTests.
        workspace.SetUpProjectsWhenTheyOpen = false;
        await workspace.LoadAsync(folder);
        await UiTestHelpers.WaitUntilAsync(() => workspace.ToolchainNotice is not null, because: "npm install offer");
        await workspace.ToolchainNotice!.Action.ExecuteAsync(null);
        Assert.True(Directory.Exists(Path.Combine(folder, "node_modules", "vite")), workspace.Output.Text);
        var run = workspace.RunCommand.ExecuteAsync(null);
        try
        {
            await UiTestHelpers.WaitUntilAsync(() => workspace.WebPreview.IsRunning, because: "F5 opens the Capacitor web part in the preview");
            Assert.True(workspace.IsWebPreviewSelected);
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
